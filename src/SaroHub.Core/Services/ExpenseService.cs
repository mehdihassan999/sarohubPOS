// src/SaroHub.Core/Services/ExpenseService.cs
using Microsoft.EntityFrameworkCore;
using SaroHub.Core.Abstractions;
using SaroHub.Core.Accounting;
using SaroHub.Domain;
using SaroHub.Domain.Common;
using SaroHub.Domain.Entities;

namespace SaroHub.Core.Services;

public sealed class ExpenseService
{
    private readonly IAppDbFactory _factory;
    private readonly NumberService _numbers;
    private readonly PostingEngine _posting;
    private readonly AuditService _audit;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAppLogger _log;
    private readonly CashSessionService _sessions;

    public ExpenseService(IAppDbFactory factory, NumberService numbers, PostingEngine posting, AuditService audit,
                          ICurrentUser user, IClock clock, IAppLogger log, CashSessionService sessions)
    { _factory = factory; _numbers = numbers; _posting = posting; _audit = audit; _user = user; _clock = clock; _log = log; _sessions = sessions; }

    public async Task<List<ExpenseCategory>> CategoriesAsync(CancellationToken ct = default)
    {
        using var db = _factory.Create();
        return await db.ExpenseCategories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
    }

    public async Task<List<Expense>> ListAsync(DateTime fromLocal, DateTime toLocal, CancellationToken ct = default)
    {
        var from = fromLocal.Date.ToUniversalTime();
        var to = toLocal.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
        using var db = _factory.Create();
        return await db.Expenses.AsNoTracking().Include(e => e.ExpenseCategory)
            .Where(e => e.AtUtc >= from && e.AtUtc <= to)
            .OrderByDescending(e => e.Id).ToListAsync(ct);
    }

    public async Task<Result<string>> AddAsync(int categoryId, long amountPaisa, PaymentMethod method,
                                               DateTime atLocal, string? note, CancellationToken ct = default)
    {
        if (!_user.Has(Domain.Security.Permissions.ExpenseCreate))
            return Result<string>.Fail("You do not have permission to add expenses.");
        if (amountPaisa <= 0) return Result<string>.Fail("Enter an amount.");
        if (method == PaymentMethod.Khata) return Result<string>.Fail("Choose a real payment method.");

        var session = await _sessions.GetOrOpenAsync(ct);

        using var db = _factory.Create();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var category = await db.ExpenseCategories.FirstAsync(c => c.Id == categoryId, ct);

            var expense = new Expense
            {
                VoucherNo = await _numbers.NextAsync(db, "Expense", ct),
                AtUtc = atLocal.ToUniversalTime(),
                ExpenseCategoryId = categoryId,
                AmountPaisa = amountPaisa,
                Method = method,
                Note = note,
                UserId = _user.UserId,
                CashSessionId = session.Id
            };
            db.Expenses.Add(expense);
            await db.SaveChangesAsync(ct);

            var draft = new JournalDraft
            {
                EntryDateUtc = expense.AtUtc,
                Description = $"{category.Name} expense",
                SourceType = nameof(Expense),
                SourceId = expense.Id,
                DocumentNo = expense.VoucherNo
            };
            draft.Debit(category.AccountCode, amountPaisa, memo: note)
                 .Credit(PostingEngine.AccountCodeForPaymentMethod(method), amountPaisa);

            await _posting.PostAsync(db, draft, _user.UserId, ct);
            _audit.Add(db, _user, "ExpenseCreated", nameof(Expense), expense.VoucherNo, null,
                       $"{category.Name} {Money.Format(amountPaisa)}");

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result<string>.Ok(expense.VoucherNo);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _log.Error("Expense failed", ex);
            return Result<string>.Fail("Could not save the expense. Your data is safe.");
        }
    }
}