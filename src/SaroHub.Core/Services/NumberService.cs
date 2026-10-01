// src/SaroHub.Core/Services/NumberService.cs
using Microsoft.EntityFrameworkCore;
using SaroHub.Core.Abstractions;
using SaroHub.Domain.Entities;

namespace SaroHub.Core.Accounting;

/// <summary>Generates durable, unique document numbers inside the caller's transaction.</summary>
public sealed class NumberService
{
    public async Task<string> NextAsync(IAppDbContext db, string name, CancellationToken ct = default)
    {
        var seq = await db.NumberSequences.FirstOrDefaultAsync(s => s.Name == name, ct);
        if (seq is null)
        {
            seq = new NumberSequence { Name = name, Prefix = DefaultPrefix(name), Next = 1 };
            db.NumberSequences.Add(seq);
        }

        var value = seq.Next;
        seq.Next = value + 1;
        return $"{seq.Prefix}{value:0000}";
    }

    private static string DefaultPrefix(string name) => name switch
    {
        "Sale" => "INV-",
        "Journal" => "JV-",
        "StockIn" => "GRN-",
        "Payment" => "PV-",
        "Expense" => "EXP-",
        "Session" => "DAY-",
        "Adjustment" => "ADJ-",
        _ => name.ToUpperInvariant()[..Math.Min(3, name.Length)] + "-"
    };
}