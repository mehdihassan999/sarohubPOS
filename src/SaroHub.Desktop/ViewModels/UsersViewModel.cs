// src/SaroHub.Desktop/ViewModels/UsersViewModel.cs
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using SaroHub.Core.Abstractions;
using SaroHub.Desktop.Mvvm;
using SaroHub.Desktop.Services;
using SaroHub.Domain.Entities;
using SaroHub.Infrastructure.Persistence;

namespace SaroHub.Desktop.ViewModels;

public sealed class UsersViewModel : ViewModelBase, ILoadable
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IPasswordHasher                 _hasher;
    private readonly WpfDialogService                _dialog;

    public ObservableCollection<User> Users { get; } = new();
    public ObservableCollection<Role> Roles { get; } = new();

    private User? _selected;
    public  User? Selected { get => _selected; set => Set(ref _selected, value); }

    // ── Edit panel ────────────────────────────────────────────────────────
    private bool   _showEdit;
    private int    _editId;
    private string _editFullName = "";
    private string _editUsername = "";
    private string _editPassword = "";
    private Role?  _editRole;
    private bool   _editActive = true;

    public bool   ShowEdit      { get => _showEdit;      set => Set(ref _showEdit, value); }
    public string EditFullName  { get => _editFullName;  set => Set(ref _editFullName, value); }
    public string EditUsername  { get => _editUsername;  set => Set(ref _editUsername, value); }
    public string EditPassword  { get => _editPassword;  set => Set(ref _editPassword, value); }
    public Role?  EditRole      { get => _editRole;      set => Set(ref _editRole, value); }
    public bool   EditActive    { get => _editActive;    set => Set(ref _editActive, value); }

    public ICommand NewCommand    { get; }
    public ICommand EditCommand   { get; }
    public ICommand SaveCommand   { get; }
    public ICommand CancelCommand { get; }

    public UsersViewModel(IDbContextFactory<AppDbContext> factory, IPasswordHasher hasher, WpfDialogService dialog)
    {
        _factory = factory;
        _hasher  = hasher;
        _dialog  = dialog;

        NewCommand    = Command(OpenNew);
        EditCommand   = Command(OpenEdit, () => Selected is not null);
        SaveCommand   = AsyncCommand(SaveAsync, () => !IsBusy);
        CancelCommand = Command(() => ShowEdit = false);
    }

    public async Task LoadAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var users = await db.Users.AsNoTracking().Include(u => u.Role).OrderBy(u => u.FullName).ToListAsync();
        var roles = await db.Roles.AsNoTracking().ToListAsync();
        Users.Clear(); foreach (var u in users) Users.Add(u);
        Roles.Clear(); foreach (var r in roles) Roles.Add(r);
    }

    private void OpenNew()
    {
        _editId = 0;
        EditFullName = ""; EditUsername = ""; EditPassword = "";
        EditRole = Roles.FirstOrDefault(r => r.Name == "Cashier") ?? Roles.FirstOrDefault();
        EditActive = true;
        ShowEdit = true;
    }

    private void OpenEdit()
    {
        if (Selected is null) return;
        _editId      = Selected.Id;
        EditFullName = Selected.FullName;
        EditUsername = Selected.Username;
        EditPassword = ""; // don't show existing hash
        EditRole     = Roles.FirstOrDefault(r => r.Id == Selected.RoleId);
        EditActive   = Selected.IsActive;
        ShowEdit = true;
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditFullName)) { ErrorMessage = "Full name is required."; return; }
        if (string.IsNullOrWhiteSpace(EditUsername)) { ErrorMessage = "Username is required."; return; }
        if (_editId == 0 && EditPassword.Length < 4)  { ErrorMessage = "Password must be at least 4 characters."; return; }
        if (EditRole is null) { ErrorMessage = "Select a role."; return; }

        await RunAsync(async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();
            User entity;
            if (_editId == 0) { entity = new User(); db.Users.Add(entity); }
            else entity = await db.Users.FirstAsync(u => u.Id == _editId);

            entity.FullName = EditFullName.Trim();
            entity.Username = EditUsername.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(EditPassword))
                entity.PasswordHash = _hasher.Hash(EditPassword);
            entity.RoleId   = EditRole.Id;
            entity.IsActive = EditActive;

            await db.SaveChangesAsync();
            ShowEdit = false; ErrorMessage = "";
            await LoadAsync();
        });
    }
}
