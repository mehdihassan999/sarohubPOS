// src/SaroHub.Desktop/Services/NavigationService.cs
using System.Windows.Controls;

namespace SaroHub.Desktop.Services;

/// <summary>
/// Simple navigation service that manages a Frame's content.
/// ViewModels call Navigate(view) — no code-behind required in pages.
/// </summary>
public sealed class NavigationService
{
    private Frame? _frame;

    public void Attach(Frame frame) => _frame = frame;

    public void Navigate(UserControl view)
    {
        if (_frame is null) return;
        _frame.Navigate(view);
    }

    public void GoBack()
    {
        if (_frame?.CanGoBack == true) _frame.GoBack();
    }
}
