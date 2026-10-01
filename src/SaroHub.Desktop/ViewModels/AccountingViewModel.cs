// src/SaroHub.Desktop/ViewModels/AccountingViewModel.cs
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using SaroHub.Core.Abstractions;
using SaroHub.Core.Services;
using SaroHub.Desktop.Mvvm;
using SaroHub.Domain;
using SaroHub.Domain.Common;
using SaroHub.Domain.Entities;

namespace SaroHub.Desktop.ViewModels;

public sealed class AccountDisplayVm
{
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public string Balance { get; init; } = "";
}

public sealed class JournalEntryDisplayVm
{
    public string EntryNo { get; init; } = "";
    public string Date { get; init; } = "";
    public string Description { get; init; } = "";
    public string DocumentNo { get; init; } = "";
    public string Debit { get; init; } = "";
    public string Credit { get; init; } = "";
}

public sealed class AccountingViewModel : ViewModelBase, ILoadable
{
    private readonly Func<IAppDbContext> _dbFactory;

    public ObservableCollection<AccountDisplayVm> Accounts { get; } = new();
    public ObservableCollection<JournalEntryDisplayVm> RecentEntries { get; } = new();

    private string _totalAssetsFmt = "";
    private string _totalLiabilitiesFmt = "";
    private string _totalEquityFmt = "";
    private string _totalRevenueFmt = "";
    private string _totalExpenseFmt = "";

    public string TotalAssetsFmt      { get => _totalAssetsFmt;      private set => Set(ref _totalAssetsFmt, value); }
    public string TotalLiabilitiesFmt { get => _totalLiabilitiesFmt; private set => Set(ref _totalLiabilitiesFmt, value); }
    public string TotalEquityFmt      { get => _totalEquityFmt;      private set => Set(ref _totalEquityFmt, value); }
    public string TotalRevenueFmt     { get => _totalRevenueFmt;     private set => Set(ref _totalRevenueFmt, value); }
    public string TotalExpenseFmt     { get => _totalExpenseFmt;     private set => Set(ref _totalExpenseFmt, value); }

    public ICommand RefreshCommand { get; }

    public AccountingViewModel(Func<IAppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        RefreshCommand = AsyncCommand(LoadAsync);
    }

    public async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            await using var db = _dbFactory();
            var accountsList = await db.Accounts.OrderBy(a => a.Code).ToListAsync();
            
            // Fetch balances
            var lines = await db.JournalEntryLines.ToListAsync();

            Accounts.Clear();
            long assets = 0, liab = 0, equity = 0, rev = 0, exp = 0;

            foreach (var a in accountsList)
            {
                var accLines = lines.Where(l => l.AccountId == a.Id).ToList();
                long debits = accLines.Sum(l => l.DebitPaisa);
                long credits = accLines.Sum(l => l.CreditPaisa);
                long balPaisa = a.IsDebitNormal ? (debits - credits) : (credits - debits);

                Accounts.Add(new AccountDisplayVm
                {
                    Code = a.Code,
                    Name = a.Name,
                    Type = a.Type.ToString(),
                    Balance = Money.Format(balPaisa)
                });

                switch (a.Type)
                {
                    case AccountType.Asset: assets += balPaisa; break;
                    case AccountType.Liability: liab += balPaisa; break;
                    case AccountType.Equity: equity += balPaisa; break;
                    case AccountType.Income: rev += balPaisa; break;
                    case AccountType.Expense: exp += balPaisa; break;
                }
            }

            TotalAssetsFmt      = Money.Format(assets);
            TotalLiabilitiesFmt = Money.Format(liab);
            TotalEquityFmt      = Money.Format(equity);
            TotalRevenueFmt     = Money.Format(rev);
            TotalExpenseFmt     = Money.Format(exp);

            // Recent journal entries
            var entries = await db.JournalEntries
                .Include(e => e.Lines)
                .OrderByDescending(e => e.EntryDateUtc)
                .Take(25)
                .ToListAsync();

            RecentEntries.Clear();
            foreach (var e in entries)
            {
                RecentEntries.Add(new JournalEntryDisplayVm
                {
                    EntryNo = e.EntryNo,
                    Date = e.EntryDateUtc.ToLocalTime().ToString("g"),
                    Description = e.Description,
                    DocumentNo = e.DocumentNo ?? "-",
                    Debit = Money.Format(e.TotalDebit),
                    Credit = Money.Format(e.TotalCredit)
                });
            }
        });
    }
}
