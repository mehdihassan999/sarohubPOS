// src/SaroHub.Desktop/Converters/Converters.cs
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SaroHub.Domain.Common;

namespace SaroHub.Desktop.Converters;

/// <summary>Formats a long paisa value as "Rs 1,234.56".</summary>
[ValueConversion(typeof(long), typeof(string))]
public sealed class MoneyConverter : IValueConverter
{
    public static readonly MoneyConverter Instance = new();
    public object Convert(object v, Type _, object __, CultureInfo ___) =>
        v is long p ? Money.Format(p) : "Rs 0.00";
    public object ConvertBack(object v, Type _, object __, CultureInfo ___) =>
        v is string s && decimal.TryParse(s.Replace("Rs", "").Trim().Replace(",", ""),
                                          NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
            ? Money.From(d) : 0L;
}

/// <summary>Formats a long Qty raw value as "1" or "1.25".</summary>
[ValueConversion(typeof(long), typeof(string))]
public sealed class QtyConverter : IValueConverter
{
    public static readonly QtyConverter Instance = new();
    public object Convert(object v, Type _, object __, CultureInfo ___) =>
        v is long q ? Qty.Format(q) : "0";
    public object ConvertBack(object v, Type _, object __, CultureInfo ___) =>
        v is string s && decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
            ? Qty.From(d) : 0L;
}

/// <summary>true → Visible, false → Collapsed.</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibility : IValueConverter
{
    public static readonly BoolToVisibility Instance = new();
    public object Convert(object v, Type _, object __, CultureInfo ___) =>
        v is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type _, object __, CultureInfo ___) =>
        v is Visibility.Visible;
}

/// <summary>true → Collapsed (inverted).</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class InverseBoolToVisibility : IValueConverter
{
    public static readonly InverseBoolToVisibility Instance = new();
    public object Convert(object v, Type _, object __, CultureInfo ___) =>
        v is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type _, object __, CultureInfo ___) =>
        v is Visibility.Collapsed;
}

/// <summary>null → Collapsed, non-null → Visible.</summary>
public sealed class NullToVisibility : IValueConverter
{
    public static readonly NullToVisibility Instance = new();
    public object Convert(object v, Type _, object __, CultureInfo ___) =>
        v is null ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type _, object __, CultureInfo ___) =>
        throw new NotSupportedException();
}

/// <summary>Inverted: null → Visible, non-null → Collapsed.</summary>
public sealed class NotNullToVisibility : IValueConverter
{
    public static readonly NotNullToVisibility Instance = new();
    public object Convert(object v, Type _, object __, CultureInfo ___) =>
        v is null ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type _, object __, CultureInfo ___) =>
        throw new NotSupportedException();
}

/// <summary>Formats a DateTime as a short local string.</summary>
[ValueConversion(typeof(DateTime), typeof(string))]
public sealed class DateTimeConverter : IValueConverter
{
    public static readonly DateTimeConverter Instance = new();
    public object Convert(object v, Type _, object __, CultureInfo ___) =>
        v is DateTime d ? d.ToString("dd/MM/yyyy HH:mm") : "";
    public object ConvertBack(object v, Type _, object __, CultureInfo ___) =>
        throw new NotSupportedException();
}

/// <summary>Formats a DateTime as date-only.</summary>
[ValueConversion(typeof(DateTime), typeof(string))]
public sealed class DateConverter : IValueConverter
{
    public static readonly DateConverter Instance = new();
    public object Convert(object v, Type _, object __, CultureInfo ___) =>
        v is DateTime d ? d.ToString("dd/MM/yyyy") : "";
    public object ConvertBack(object v, Type _, object __, CultureInfo ___) =>
        throw new NotSupportedException();
}

/// <summary>Zero → Collapsed, non-zero → Visible.</summary>
[ValueConversion(typeof(long), typeof(Visibility))]
public sealed class NonZeroToVisibility : IValueConverter
{
    public static readonly NonZeroToVisibility Instance = new();
    public object Convert(object v, Type _, object __, CultureInfo ___) =>
        v is long n && n != 0 ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type _, object __, CultureInfo ___) =>
        throw new NotSupportedException();
}

/// <summary>string.IsNullOrEmpty → Collapsed.</summary>
public sealed class StringToVisibility : IValueConverter
{
    public static readonly StringToVisibility Instance = new();
    public object Convert(object v, Type _, object __, CultureInfo ___) =>
        v is string s && !string.IsNullOrEmpty(s) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type _, object __, CultureInfo ___) =>
        throw new NotSupportedException();
}
