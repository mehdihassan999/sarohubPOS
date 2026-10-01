// src/SaroHub.Core/Services/CashSessionService.cs
using Microsoft.EntityFrameworkCore;
using SaroHub.Core.Abstractions;
using SaroHub.Core.Accounting;
using SaroHub.Core.Dtos;
using SaroHub.Domain;
using SaroHub.Domain.Accounting;
using SaroHub.Domain.Common;
using SaroHub.Domain.Entities;

namespace SaroHub.Core.Services;

public sealed class CashSessionService
{
    private readonly IAppDbFactory _factory;
    private readonly NumberService _numbers;
    private readonly PostingEngine _posting;
    private readonly AuditService _audit;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAppLogger _log;

    public CashSessionService(IAppDbFactory factory, NumberService numbers, PostingEngine posting,
                              AuditService audit, ICurrentUser user, IClock clock, IAppLogger log)
    { _factory = factory; _numbers = numbers; _posting = posting; _audit = audit; _user = user; _clock = clock; _log = log; }

    public async Task<CashSession> GetOrOpenAsync(CancellationToken ct = default)
    {
        using var db = _factory.Create();
        var open = await db.CashSessions.FirstOrDefaultAsync(s => s.Status == CashSessionStatus.Open, ct);
        if (open is not null) return open;

        var last = await db.CashSessions.OrderByDescending(s => s.Id).FirstOrDefaultAsync(ct);
        var openingCash = last?.CountedCashPaisa ?? 0;

        var session = new CashSession
        {
            SessionNo = await _numbers.NextAsync(db, "Session", ct),
            OpenedAtUtc = _clock.UtcNow,
            OpeningCashPaisa = openingCash,
            OpenedByUserId = _user.UserId,
            Status = CashSessionStatus.Open
        };
        db.CashSessions.Add(session);
        _audit.Add(db, _user, "DayOpened", nameof(CashSession), session.SessionNo);
        await db.SaveChangesAsync(ct);
        return session;
    }

    public async Task<CashSession?> GetOpenAsync(CancellationToken ct = default)
    {
        using var db = _factory.Create();
        return await db.CashSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Status == CashSessionStatus.Open, ct);
    }

    public async Task<DayCloseSummary?> SummaryAsync(CancellationToken ct = default)
    {
        using var db = _factory.Create();
        var session = await db.CashSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Status == CashSessionStatus.Open, ct);
        if (session is null) return null;

        var from = session.OpenedAtUtc;
        var to = _clock.UtcNow;

        var sales = await db.Sales.AsNoTracking()
            .Where(s => s.CashSessionId == session.Id && s.Status == SaleStatus.Posted).ToListAsync(ct);

        var salePayments = await db.SalePayments.AsNoTracking()
            .Where(p => sales.Select(s => s.Id).Contains(p.SaleId)).ToListAsync(ct);

        var khataReceipts = await db.PartyPayments.AsNoTracking()
            .Where(p => p.CashSessionId == session.Id && p.PartyType == PartyType.Customer && p.Method == PaymentMethod.Cash)
            .SumAsync(p => (long?)p.AmountPaisa, ct) ?? 0;

        var cashExpenses = await db.Expenses.AsNoTracking()
            .Where(e => e.CashSessionId == session.Id && e.Method == PaymentMethod.Cash)
            .SumAsync(e => (long?)e.AmountPaisa, ct) ?? 0;

        var cashPurchases = await db.StockIns.AsNoTracking()
            .Where(p => p.CashSessionId == session.Id && p.PaidMethod == PaymentMethod.Cash && !p.IsOpening)
            .SumAsync(p => (long?)p.PaidPaisa, ct) ?? 0;

        var cashSales = salePayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.AmountPaisa);
        var creditSales = sales.Sum(s => s.KhataPaisa);
        var totalSales = sales.Sum(s => s.TotalPaisa);

        // Expected cash comes from the Cash account movement, so it always reconciles with accounting.
        var cashAccountId = await db.Accounts.Where(a => a.Code == AccountCodes.CashInHand).Select(a => a.Id).FirstAsync(ct);
        var movement = await db.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.AccountId == cashAccountId
                     && l.JournalEntry!.EntryDateUtc >= from && l.JournalEntry.EntryDateUtc <= to)
            .SumAsync(l => (long?)(l.DebitPaisa - l.CreditPaisa), ct) ?? 0;

        var expected = session.OpeningCashPaisa + movement;

        var cogs = sales.Sum(s => s.CostPaisa);
        var expensesTotal = await db.Expenses.AsNoTracking()
            .Where(e => e.CashSessionId == session.Id).SumAsync(e => (long?)e.AmountPaisa, ct) ?? 0;
        var profit = totalSales - sales.Sum(s => s.TaxPaisa) - cogs - expensesTotal;

        return new DayCloseSummary(session.Id, session.OpenedAtUtc.ToLocalTime(), session.OpeningCashPaisa,
            cashSales, creditSales, khataReceipts, cashExpenses, cashPurchases, expected, totalSales, profit);
    }

    public async Task<Result> CloseAsync(long countedCashPaisa, string? note, CancellationToken ct = default)
    {
        if (!_user.Has(Domain.Security.Permissions.DayClose))
            return Result.Fail("You do not have permission to close the day.");

        var summary = await SummaryAsync(ct);
        if (summary is null) return Result.Fail("No open day.");

        using var db = _factory.Create();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var session = await db.CashSessions.FirstAsync(s => s.Id == summary.SessionId, ct);
            var difference = countedCashPaisa - summary.ExpectedCashPaisa;

            session.ExpectedCashPaisa = summary.ExpectedCashPaisa;
            session.CountedCashPaisa = countedCashPaisa;
            session.DifferencePaisa = difference;
            session.ClosedAtUtc = _clock.UtcNow;
            session.ClosedByUserId = _user.UserId;
            session.Status = CashSessionStatus.Closed;
            session.Note = note;

            // The difference is recorded, never hidden.
            if (difference != 0)
            {
                var draft = new JournalDraft
                {
                    EntryDateUtc = _clock.UtcNow,
                    Description = difference < 0 ? "Cash shortage at day close" : "Cash overage at day close",
                    SourceType = nameof(CashSession),
                    SourceId = session.Id,
                    DocumentNo = session.SessionNo
                };

                if (difference < 0)
                    draft.Debit(AccountCodes.CashShortage, -difference).Credit(AccountCodes.CashInHand, -difference);
                else
                    draft.Debit(AccountCodes.CashInHand, difference).Credit(AccountCodes.CashOverage, difference);

                await _posting.PostAsync(db, draft, _user.UserId, ct);
            }

            _audit.Add(db, _user, "DayClosed", nameof(CashSession), session.SessionNo,
                       $"Expected {Money.Format(summary.ExpectedCashPaisa)}",
                       $"Counted {Money.Format(countedCashPaisa)} / Diff {Money.Format(difference)}");

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _log.Error("Day close failed", ex);
            return Result.Fail("Could not close the day. Your data is safe.");
        }
    }
}