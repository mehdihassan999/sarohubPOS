// src/SaroHub.Domain/Common/Qty.cs
namespace SaroHub.Domain.Common;

/// <summary>Quantities are stored as integers scaled by 10,000 (4 decimal places).</summary>
public static class Qty
{
    public const long Scale = 10_000;

    public static long From(decimal value) => (long)Math.Round(value * Scale, MidpointRounding.AwayFromZero);
    public static decimal To(long raw) => (decimal)raw / Scale;

    public static string Format(long raw)
    {
        var d = To(raw);
        return d == Math.Truncate(d) ? d.ToString("0") : d.ToString("0.####");
    }
}