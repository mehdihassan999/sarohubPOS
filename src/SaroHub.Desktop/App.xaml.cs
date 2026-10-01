// src/SaroHub.Desktop/App.xaml.cs
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SaroHub.Core.Services;
using SaroHub.Desktop.Services;
using SaroHub.Desktop.ViewModels;
using SaroHub.Desktop.Views;
using SaroHub.Infrastructure.Persistence;

namespace SaroHub.Desktop;

public partial class App : Application
{
    private IServiceProvider _services = null!;
    private static readonly string StartupTracePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SaroHub POS", "Logs", "startup-trace.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        LogStartup("OnStartup invoked");
        base.OnStartup(e);
        LogStartup("WPF startup completed");

        AppDomain.CurrentDomain.UnhandledException += (s, eArgs) =>
        {
            var ex = eArgs.ExceptionObject as Exception;
            string? errorLogPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SaroHub POS", "Logs", "unhandled-errors.log");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(errorLogPath)!);
                File.AppendAllText(errorLogPath,
                    $"--- FATAL DOMAIN EXCEPTION {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
            }
            catch { }

            MessageBox.Show($"Fatal startup error:\n\n{ex?.Message}\n\n{ex?.StackTrace}", "SaroHub POS - Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (_, ex) =>
        {
            string? errorLogPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SaroHub POS", "Logs", "unhandled-errors.log");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(errorLogPath)!);
                File.AppendAllText(errorLogPath,
                    $"--- {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---{Environment.NewLine}{ex.Exception}{Environment.NewLine}{Environment.NewLine}");
            }
            catch { }

            var message = $"An unexpected error occurred:\n\n{ex.Exception.Message}";
            if (errorLogPath is not null)
                message += $"\n\nDetails saved to:\n{errorLogPath}";

            MessageBox.Show(message, "SaroHub POS - Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            ex.Handled = true;
        };

        try
        {
            LogStartup("Building services");
            _services = Composition.BuildServices();
            LogStartup("Services built");

            // Run database initialization synchronously on startup
            LogStartup("Initializing database");
            var init = _services.GetRequiredService<DatabaseInitializer>();
            LogStartup("Database initializer resolved");
            Task.Run(async () => await init.InitializeAsync()).GetAwaiter().GetResult();
            LogStartup("Database initialized");

            LogStartup("Loading settings");
            var settings = _services.GetRequiredService<SettingsService>();
            Task.Run(async () => await settings.LoadAsync()).GetAwaiter().GetResult();
            LogStartup("Settings loaded");

            LogStartup("Checking setup completion");
            var setup = _services.GetRequiredService<SetupService>();
            bool isSetupComplete = Task.Run(async () => await setup.IsSetupCompleteAsync()).GetAwaiter().GetResult();
            LogStartup($"Setup check completed: {isSetupComplete}");

            if (!isSetupComplete)
            {
                var firstRunVm = _services.GetRequiredService<FirstRunViewModel>();
                var firstRunWin = new FirstRunView { DataContext = firstRunVm };
                MainWindow = firstRunWin;
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                firstRunVm.Completed += async () =>
                {
                    await settings.LoadAsync();
                    ShowMainWindow();
                    firstRunWin.Close();
                };
                firstRunWin.Show();
            }
            else
            {
                ShowLoginWindow();
            }
        }
        catch (Exception ex)
        {
            LogStartup($"Startup error: {ex}");
            MessageBox.Show(
                $"SaroHub POS Startup Error:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                "SaroHub POS - Fatal Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static void LogStartup(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StartupTracePath)!);
            File.AppendAllText(StartupTracePath, $"{DateTime.Now:O} {message}{Environment.NewLine}");
        }
        catch { }
    }

    private void ShowLoginWindow()
    {
        var vm = _services.GetRequiredService<LoginViewModel>();
        var win = new LoginView { DataContext = vm };
        MainWindow = win;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        vm.LoginSucceeded += () =>
        {
            ShowMainWindow();
            win.Close();
        };
        win.Show();
    }

    private void ShowMainWindow()
    {
        var vm = _services.GetRequiredService<MainViewModel>();
        var win = new MainWindow { DataContext = vm };
        MainWindow = win;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        win.Show();
        _ = vm.InitAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        (_services as IDisposable)?.Dispose();
        base.OnExit(e);
    }
}
