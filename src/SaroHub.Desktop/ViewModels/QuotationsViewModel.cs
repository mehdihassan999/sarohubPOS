// src/SaroHub.Desktop/ViewModels/QuotationsViewModel.cs
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using SaroHub.Core.Abstractions;
using SaroHub.Core.Services;
using SaroHub.Desktop.Mvvm;
using SaroHub.Domain.Common;
using SaroHub.Domain.Entities;

namespace SaroHub.Desktop.ViewModels;

public sealed class QuotationDisplayVm
{
    public string QuotationNo { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string Date { get; init; } = "";
    public string ExpiryDate { get; init; } = "";
    public string Total { get; init; } = "";
    public string Status { get; init; } = "";
}

public sealed class QuotationsViewModel : ViewModelBase, ILoadable
{
    private readonly Func<IAppDbContext> _dbFactory;

    public ObservableCollection<QuotationDisplayVm> Quotations { get; } = new();

    public ICommand RefreshCommand { get; }

    public QuotationsViewModel(Func<IAppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        RefreshCommand = AsyncCommand(LoadAsync);
    }

    public async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            await using var db = _dbFactory();
            var list = await db.Quotations
                .OrderByDescending(q => q.DateUtc)
                .Take(50)
                .ToListAsync();

            Quotations.Clear();
            foreach (var q in list)
            {
                Quotations.Add(new QuotationDisplayVm
                {
                    QuotationNo = q.QuotationNo,
                    CustomerName = string.IsNullOrWhiteSpace(q.CustomerName) ? "Walk-in" : q.CustomerName,
                    Date = q.DateUtc.ToLocalTime().ToString("d"),
                    ExpiryDate = q.ExpiryDateUtc.ToLocalTime().ToString("d"),
                    Total = Money.Format(q.TotalPaisa),
                    Status = q.Status
                });
            }
        });
    }
}
