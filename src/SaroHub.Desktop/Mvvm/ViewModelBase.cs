// src/SaroHub.Desktop/Mvvm/ViewModelBase.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace SaroHub.Desktop.Mvvm;

/// <summary>
/// Base class for all ViewModels.
/// Provides INPC, IsBusy/ErrorMessage helpers, and a lightweight async-command factory.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    // ── Busy / Error ──────────────────────────────────────────────────────────
    private bool   _isBusy;
    private string _errorMessage = "";

    public bool IsBusy
    {
        get => _isBusy;
        protected set => Set(ref _isBusy, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        protected set => Set(ref _errorMessage, value);
    }

    protected void ClearError() => ErrorMessage = "";

    /// <summary>Runs an async action: sets IsBusy, catches exceptions as ErrorMessage.</summary>
    protected async Task RunAsync(Func<Task> action, string? busyMessage = null)
    {
        ErrorMessage = "";
        IsBusy = true;
        try   { await action(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    // ── Command factory ───────────────────────────────────────────────────────
    protected static ICommand Command(Action execute, Func<bool>? canExecute = null)
        => new RelayCommand(execute, canExecute);

    protected static ICommand AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null)
        => new AsyncRelayCommand(execute, canExecute);
}

// ─────────────────────────────────────────────────────────────────────────────

public sealed class RelayCommand : ICommand
{
    private readonly Action      _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    { _execute = execute; _canExecute = canExecute; }

    public event EventHandler? CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? _) => _canExecute?.Invoke() ?? true;
    public void Execute(object? _)    => _execute();
}

public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?>      _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    { _execute = execute; _canExecute = canExecute; }

    public event EventHandler? CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? p) => _canExecute?.Invoke((T?)p) ?? true;
    public void Execute(object? p)    => _execute((T?)p);
}

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _running;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    { _execute = execute; _canExecute = canExecute; }

    public event EventHandler? CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? _) => !_running && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? _)
    {
        _running = true;
        CommandManager.InvalidateRequerySuggested();
        try   { await _execute(); }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _running = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
