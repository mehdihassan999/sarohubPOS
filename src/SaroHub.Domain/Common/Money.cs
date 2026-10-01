// src/SaroHub.Domain/Common/Money.cs
using System.Globalization;

namespace SaroHub.Domain.Common;

/// <summary>All money is stored as integer minor units (paisa). Never use double/float.</summary>
public static class Money
{
    public const long Scale = 100;

    public static long From(decimal value) => (long)Math.Round(value * Scale, MidpointRounding.AwayFromZero);
    public static decimal To(long paisa) => (decimal)paisa / Scale;

    public static string Format(long paisa, string symbol = "Rs ")
        => symbol + To(paisa).ToString("N2", CultureInfo.InvariantCulture);

    /// <summary>Multiplies a money amount by a quantity and rounds to the nearest paisa.</summary>
    public static long Multiply(long paisa, decimal qty)
        => (long)Math.Round(paisa * qty, MidpointRounding.AwayFromZero);
}