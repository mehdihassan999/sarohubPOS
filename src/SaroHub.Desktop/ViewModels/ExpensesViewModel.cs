// src/SaroHub.Desktop/ViewModels/ExpensesViewModel.cs
using System.Collections.ObjectModel;
using System.Windows.Input;
using SaroHub.Core.Services;
using SaroHub.Desktop.Mvvm;
using SaroHub.Domain;
using SaroHub.Domain.Common;
using SaroHub.Domain.Entities;

namespace SaroHub.Desktop.ViewModels;

public sealed class ExpensesViewModel : ViewModelBase, ILoadable
{
    private readonly ExpenseService _expenses;

    public ObservableCollection<Expense>         Items      { get; } = new();
    public ObservableCollection<ExpenseCategory> Categories { get; } = new();

    // ── New expense ────────────────────────────────────────────────────────
    private bool   _showNew;
    private ExpenseCategory? _category;
    private string _amount  = "";
    private string _note    = "";
    private PaymentMethod _method = PaymentMethod.Cash;

    public bool   ShowNew    { get => _showNew;   set => Set(ref _showNew, value); }
    public ExpenseCategory? Category { get => _category; set => Set(ref _category, value); }
    public string Amount     { get => _amount;    set => Set(ref _amount, value); }
    public string Note       { get => _note;      set => Set(ref _note, value); }
    public PaymentMethod Method { get => _method; set => Set(ref _method, value); }

    public IReadOnlyList<PaymentMethod> PayMethods { get; } =
        new[] { PaymentMethod.Cash, PaymentMethod.Bank, PaymentMethod.Easypaisa, PaymentMethod.JazzCash };

    public ICommand NewCommand    { get; }
    public ICommand SaveCommand   { get; }
    public ICommand CancelCommand { get; }

    public ExpensesViewModel(ExpenseService expenses)
    {
        _expenses = expenses;
        NewCommand    = Command(() => { ShowNew = true; Amount = ""; Note = ""; Category = null; });
        SaveCommand   = AsyncCommand(SaveAsync, () => !IsBusy);
        CancelCommand = Command(() => ShowNew = false);
    }

    public async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var cats = await _expenses.CategoriesAsync();
            Categories.Clear(); foreach (var c in cats) Categories.Add(c);
            if (Category is null && Categories.Count > 0) Category = Categories[0];

            var list = await _expenses.ListAsync(DateTime.Today.AddDays(-30), DateTime.Today);
            Items.Clear(); foreach (var e in list) Items.Add(e);
        });
    }

    private async Task SaveAsync()
    {
        if (Category is null) { ErrorMessage = "Select a category."; return; }
        if (!decimal.TryParse(Amount, out var v) || v <= 0) { ErrorMessage = "Enter a valid amount."; return; }

        await RunAsync(async () =>
        {
            var r = await _expenses.AddAsync(Category.Id, Money.From(v), Method, DateTime.Now, Note);
            if (!r.IsSuccess) { ErrorMessage = r.Error; return; }
            ShowNew = false; ErrorMessage = "";
            await LoadAsync();
        });
    }
}
