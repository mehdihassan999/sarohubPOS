// src/SaroHub.Desktop/Services/WpfDialogService.cs
using System.Windows;
using Microsoft.Win32;

namespace SaroHub.Desktop.Services;

/// <summary>Thin wrapper around WPF dialogs so ViewModels stay testable.</summary>
public sealed class WpfDialogService
{
    /// <summary>Returns true if the user confirmed.</summary>
    public bool Confirm(string message, string title = "Confirm")
        => MessageBox.Show(message, title,
               MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void Alert(string message, string title = "SaroHub POS")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void Error(string message, string title = "Error")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    /// <summary>Opens a folder picker and returns the chosen path, or null.</summary>
    public string? PickFolder(string description = "Select folder")
    {
        var dlg = new OpenFolderDialog { Title = description };
        return dlg.ShowDialog() == true ? dlg.FolderName : null;
    }

    /// <summary>Opens a file picker for .db files and returns the chosen path, or null.</summary>
    public string? PickBackupFile()
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Select Backup File",
            Filter = "SaroHub Backup (*.db)|*.db|All Files (*.*)|*.*"
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
