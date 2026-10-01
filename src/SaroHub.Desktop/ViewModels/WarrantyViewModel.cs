// src/SaroHub.Desktop/ViewModels/WarrantyViewModel.cs
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using SaroHub.Core.Abstractions;
using SaroHub.Desktop.Mvvm;
using SaroHub.Domain.Entities;

namespace SaroHub.Desktop.ViewModels;

public sealed class WarrantyDisplayVm
{
    public string SerialNumber { get; init; } = "";
    public string ProductName { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string InvoiceNo { get; init; } = "";
    public string PurchaseDate { get; init; } = "";
    public string ExpiryDate { get; init; } = "";
    public string Status { get; init; } = "";
}

public sealed class WarrantyViewModel : ViewModelBase, ILoadable
{
    private readonly Func<IAppDbContext> _dbFactory;

    private string _searchQuery = "";
    public string SearchQuery
    {
        get => _searchQuery;
        set { Set(ref _searchQuery, value); _ = LoadAsync(); }
    }

    public ObservableCollection<WarrantyDisplayVm> Records { get; } = new();

    public ICommand SearchCommand { get; }

    public WarrantyViewModel(Func<IAppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        SearchCommand = AsyncCommand(LoadAsync);
    }

    public async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            await using var db = _dbFactory();
            var query = db.WarrantyRecords.AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var q = SearchQuery.Trim().ToLower();
                query = query.Where(w => w.SerialNumber.ToLower().Contains(q) ||
                                         w.ProductName.ToLower().Contains(q) ||
                                         w.CustomerName.ToLower().Contains(q) ||
                                         w.InvoiceNo.ToLower().Contains(q));
            }

            var list = await query.OrderByDescending(w => w.PurchaseDateUtc).Take(50).ToListAsync();

            Records.Clear();
            foreach (var w in list)
            {
                Records.Add(new WarrantyDisplayVm
                {
                    SerialNumber = w.SerialNumber,
                    ProductName = w.ProductName,
                    CustomerName = w.CustomerName,
                    InvoiceNo = w.InvoiceNo,
                    PurchaseDate = w.PurchaseDateUtc.ToLocalTime().ToString("d"),
                    ExpiryDate = w.ExpiryDateUtc.ToLocalTime().ToString("d"),
                    Status = w.Status
                });
            }
        });
    }
}
