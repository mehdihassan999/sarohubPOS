// src/SaroHub.Core/Services/ReportService.cs
using Microsoft.EntityFrameworkCore;
using SaroHub.Core.Abstractions;
using SaroHub.Core.Dtos;
using SaroHub.Domain;
using SaroHub.Domain.Accounting;
using SaroHub.Domain.Entities;

namespace SaroHub.Core.Services;

public sealed class ReportService
{
    private readonly IAppDbFactory _factory;
    public ReportService(IAppDbFactory factory) => _factory = factory;

    private static (DateTime from, DateTime to) Range(DateTime fromLocal, DateTime toLocal)
        => (fromLocal.Date.ToUniversalTime(), toLocal.Date.AddDays(1).AddTicks(-1).ToUniversalTime());

    public async Task<List<TrialBalanceRow>> TrialBalanceAsync(DateTime asOfLocal, CancellationToken ct = default)
    {
        var to = asOfLocal.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
        using var db = _factory.Create();

        var data = await db.JournalEntryLines.AsNoTracking()
            .Include(l => l.Account).Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.EntryDateUtc <= to && l.JournalEntry.IsPosted)
            .GroupBy(l => new { l.Account!.Code, l.Account.Name, l.Account.Type })
            .Select(g => new
            {
                g.Key.Code,
                g.Key.Name,
                g.Key.Type,
                Debit = g.Sum(x => x.DebitPaisa),
                Credit = g.Sum(x => x.CreditPaisa)
            })
            .ToListAsync(ct);

        var rows = new List<TrialBalanceRow>();
        foreach (var d in data.OrderBy(d => d.Code))
        {
            var net = d.Debit - d.Credit;
            rows.Add(net >= 0
                ? new TrialBalanceRow(d.Code, d.Name, net, 0)
                : new TrialBalanceRow(d.Code, d.Name, 0, -net));
        }
        return rows;
    }

    public async Task<ProfitAndLossReport> ProfitAndLossAsync(DateTime fromLocal, DateTime toLocal, CancellationToken ct = default)
    {
        var (from, to) = Range(fromLocal, toLocal);
        using var db = _factory.Create();

        var lines = await db.JournalEntryLines.AsNoTracking()
            .Include(l => l.Account).Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.EntryDateUtc >= from && l.JournalEntry.EntryDateUtc <= to && l.JournalEntry.IsPosted)
            .Where(l => l.Account!.Type == AccountType.Income || l.Account.Type == AccountType.Expense)
            .ToListAsync(ct);

        long Bal(string code, bool debitNormal)
        {
            var g = lines.Where(l => l.Account!.Code == code);
            return debitNormal ? g.Sum(l => l.DebitPaisa - l.CreditPaisa) : g.Sum(l => l.CreditPaisa - l.DebitPaisa);
        }

        var sales = Bal(AccountCodes.Sales, false);
        var discount = Bal(AccountCodes.SalesDiscount, true);
        var cogs = Bal(AccountCodes.CostOfGoodsSold, true);
        var gross = sales - discount - cogs;

        var expenseRows = lines
            .Where(l => l.Account!.Type == AccountType.Expense && l.Account.Code != AccountCodes.CostOfGoodsSold)
            .GroupBy(l => l.Account!.Name)
            .Select(g => (Name: g.Key, Amount: g.Sum(x => x.DebitPaisa - x.CreditPaisa)))
            .Where(x => x.Amount != 0)
            .OrderByDescending(x => x.Amount)
            .ToList();

        var otherIncome = lines
            .Where(l => l.Account!.Type == AccountType.Income
                     && l.Account.Code != AccountCodes.Sales && l.Account.Code != AccountCodes.SalesDiscount)
            .Sum(l => l.CreditPaisa - l.DebitPaisa);

        var totalExpenses = expenseRows.Sum(x => x.Amount);
        var net = gross + otherIncome - totalExpenses;

        return new ProfitAndLossReport(sales, discount, cogs, gross,
            expenseRows.Select(x => (x.Name, x.Amount)).ToList(), totalExpenses, net);
    }

    public async Task<BalanceSheetReport> BalanceSheetAsync(DateTime asOfLocal, CancellationToken ct = default)
    {
        var to = asOfLocal.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
        using var db = _factory.Create();

        var lines = await db.JournalEntryLines.AsNoTracking()
            .Include(l => l.Account).Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.EntryDateUtc <= to && l.JournalEntry.IsPosted)
            .ToListAsync(ct);

        List<(string, long)> Group(AccountType type, bool debitNormal) => lines
            .Where(l => l.Account!.Type == type)
            .GroupBy(l => l.Account!.Name)
            .Select(g => (g.Key, debitNormal
                ? g.Sum(x => x.DebitPaisa - x.CreditPaisa)
                : g.Sum(x => x.CreditPaisa - x.DebitPaisa)))
            .Where(x => x.Item2 != 0)
            .OrderBy(x => x.Key)
            .ToList();

        var assets = Group(AccountType.Asset, true);
        var liabilities = Group(AccountType.Liability, false);
        var equity = Group(AccountType.Equity, false);

        var income = lines.Where(l => l.Account!.Type == AccountType.Income).Sum(l => l.CreditPaisa - l.DebitPaisa);
        var expense = lines.Where(l => l.Account!.Type == AccountType.Expense).Sum(l => l.DebitPaisa - l.CreditPaisa);
        var profit = income - expense;

        return new BalanceSheetReport(
            assets, assets.Sum(a => a.Item2),
            liabilities, liabilities.Sum(l => l.Item2),
            equity, equity.Sum(e => e.Item2),
            profit);
    }

    public async Task<long> AccountBalanceAsync(string code, DateTime? asOfLocal = null, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        var to = (asOfLocal?.Date.AddDays(1).AddTicks(-1) ?? DateTime.Now.AddYears(50)).ToUniversalTime();

        var account = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Code == code, ct);
        if (account is null) return 0;

        var sum = await db.JournalEntryLines.AsNoTracking().Include(l => l.JournalEntry)
            .Where(l => l.AccountId == account.Id && l.JournalEntry!.EntryDateUtc <= to && l.JournalEntry.IsPosted)
            .SumAsync(l => (long?)(l.DebitPaisa - l.CreditPaisa), ct) ?? 0;

        return account.IsDebitNormal ? sum : -sum;
    }

    public async Task<DashboardSnapshot> DashboardAsync(DateTime fromLocal, DateTime toLocal, CancellationToken ct = default)
    {
        var (from, to) = Range(fromLocal, toLocal);
        using var db = _factory.Create();

        var sales = await db.Sales.AsNoTracking()
            .Where(s => s.SaleAtUtc >= from && s.SaleAtUtc <= to && s.Status == SaleStatus.Posted)
            .ToListAsync(ct);

        var expenses = await db.Expenses.AsNoTracking()
            .Where(e => e.AtUtc >= from && e.AtUtc <= to).SumAsync(e => (long?)e.AmountPaisa, ct) ?? 0;

        var salesTotal = sales.Sum(s => s.TotalPaisa - s.TaxPaisa);
        var cogs = sales.Sum(s => s.CostPaisa);
        var profit = salesTotal - cogs - expenses;

        var cash = await AccountBalanceAsync(AccountCodes.CashInHand, toLocal, ct);
        var receivable = await AccountBalanceAsync(AccountCodes.AccountsReceivable, toLocal, ct);
        var payable = await AccountBalanceAsync(AccountCodes.AccountsPayable, toLocal, ct);

        return new DashboardSnapshot(sales.Sum(s => s.TotalPaisa), profit, cash, receivable, payable, expenses, sales.Count);
    }

    public async Task<List<(DateTime Day, long Total)>> SalesByDayAsync(DateTime fromLocal, DateTime toLocal, CancellationToken ct = default)
    {
        var (from, to) = Range(fromLocal, toLocal);
        using var db = _factory.Create();

        var rows = await db.Sales.AsNoTracking()
            .Where(s => s.SaleAtUtc >= from && s.SaleAtUtc <= to && s.Status == SaleStatus.Posted)
            .Select(s => new { s.SaleAtUtc, s.TotalPaisa })
            .ToListAsync(ct);

        return rows.GroupBy(r => r.SaleAtUtc.ToLocalTime().Date)
                   .OrderBy(g => g.Key)
                   .Select(g => (g.Key, g.Sum(x => x.TotalPaisa)))
                   .ToList();
    }

    public async Task<List<KhataRow>> CustomersWhoOweAsync(CancellationToken ct = default)
    {
        using var db = _factory.Create();
        return await db.Customers.AsNoTracking().Where(c => c.BalancePaisa > 0)
            .OrderByDescending(c => c.BalancePaisa)
            .Select(c => new KhataRow(c.Id, c.Name, c.Phone, c.BalancePaisa)).ToListAsync(ct);
    }

    public async Task<List<KhataRow>> SuppliersToPayAsync(CancellationToken ct = default)
    {
        using var db = _factory.Create();
        return await db.Suppliers.AsNoTracking().Where(s => s.BalancePaisa > 0)
            .OrderByDescending(s => s.BalancePaisa)
            .Select(s => new KhataRow(s.Id, s.Name, s.Phone, s.BalancePaisa)).ToListAsync(ct);
    }

    public async Task<List<(string Product, long QtyRaw, long Amount)>> TopProductsAsync(
        DateTime fromLocal, DateTime toLocal, int take = 10, CancellationToken ct = default)
    {
        var (from, to) = Range(fromLocal, toLocal);
        using var db = _factory.Create();

        return await db.SaleItems.AsNoTracking().Include(i => i.Sale)
            .Where(i => i.Sale!.SaleAtUtc >= from && i.Sale.SaleAtUtc <= to)
            .GroupBy(i => i.ProductName)
            .Select(g => new ValueTuple<string, long, long>(
                g.Key, g.Sum(x => x.QtyBaseRaw), g.Sum(x => x.LineTotalPaisa)))
            .OrderByDescending(x => x.Item3).Take(take).ToListAsync(ct);
    }
}