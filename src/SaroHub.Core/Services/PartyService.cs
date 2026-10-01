// src/SaroHub.Core/Services/PartyService.cs
using Microsoft.EntityFrameworkCore;
using SaroHub.Core.Abstractions;
using SaroHub.Core.Dtos;
using SaroHub.Domain;
using SaroHub.Domain.Accounting;
using SaroHub.Domain.Common;
using SaroHub.Domain.Entities;

namespace SaroHub.Core.Services;

public sealed class PartyService
{
    private readonly IAppDbFactory _factory;
    private readonly AuditService _audit;
    private readonly ICurrentUser _user;
    private readonly IAppLogger _log;

    public PartyService(IAppDbFactory factory, AuditService audit, ICurrentUser user, IAppLogger log)
    { _factory = factory; _audit = audit; _user = user; _log = log; }

    public async Task<List<Customer>> SearchCustomersAsync(string? term, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        var q = db.Customers.AsNoTracking().Where(c => c.IsActive);
        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.Trim();
            q = q.Where(c => c.Name.Contains(t) || (c.Phone != null && c.Phone.Contains(t)));
        }
        return await q.OrderByDescending(c => c.BalancePaisa).ThenBy(c => c.Name).Take(200).ToListAsync(ct);
    }

    public async Task<List<Supplier>> SearchSuppliersAsync(string? term, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        var q = db.Suppliers.AsNoTracking().Where(s => s.IsActive);
        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.Trim();
            q = q.Where(s => s.Name.Contains(t) || (s.Phone != null && s.Phone.Contains(t)));
        }
        return await q.OrderByDescending(s => s.BalancePaisa).ThenBy(s => s.Name).Take(200).ToListAsync(ct);
    }

    public async Task<Result<int>> SaveCustomerAsync(Customer edited, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(edited.Name)) return Result<int>.Fail("Customer name is required.");

        using var db = _factory.Create();
        Customer entity;
        if (edited.Id == 0) { entity = new Customer(); db.Customers.Add(entity); }
        else entity = await db.Customers.FirstAsync(c => c.Id == edited.Id, ct);

        entity.Name = edited.Name.Trim();
        entity.Phone = edited.Phone?.Trim();
        entity.Address = edited.Address;
        entity.Notes = edited.Notes;
        entity.CreditLimitPaisa = edited.CreditLimitPaisa;
        entity.IsActive = edited.IsActive;

        _audit.Add(db, _user, edited.Id == 0 ? "CustomerCreated" : "CustomerUpdated", nameof(Customer), entity.Name);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(entity.Id);
    }

    public async Task<Result<int>> SaveSupplierAsync(Supplier edited, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(edited.Name)) return Result<int>.Fail("Supplier name is required.");

        using var db = _factory.Create();
        Supplier entity;
        if (edited.Id == 0) { entity = new Supplier(); db.Suppliers.Add(entity); }
        else entity = await db.Suppliers.FirstAsync(s => s.Id == edited.Id, ct);

        entity.Name = edited.Name.Trim();
        entity.Phone = edited.Phone?.Trim();
        entity.Address = edited.Address;
        entity.Notes = edited.Notes;
        entity.IsActive = edited.IsActive;

        _audit.Add(db, _user, edited.Id == 0 ? "SupplierCreated" : "SupplierUpdated", nameof(Supplier), entity.Name);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(entity.Id);
    }

    /// <summary>Customer statement built from the general ledger, not from a separate khata table.</summary>
    public async Task<List<LedgerRow>> CustomerLedgerAsync(int customerId, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        var ar = await db.Accounts.FirstAsync(a => a.Code == AccountCodes.AccountsReceivable, ct);

        var lines = await db.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.AccountId == ar.Id && l.CustomerId == customerId)
            .OrderBy(l => l.JournalEntry!.EntryDateUtc).ThenBy(l => l.Id)
            .ToListAsync(ct);

        long running = 0;
        var rows = new List<LedgerRow>();
        foreach (var l in lines)
        {
            running += l.DebitPaisa - l.CreditPaisa;
            rows.Add(new LedgerRow(
                l.JournalEntry!.EntryDateUtc.ToLocalTime(),
                l.JournalEntry.DocumentNo ?? l.JournalEntry.EntryNo,
                l.Memo ?? l.JournalEntry.Description,
                l.DebitPaisa, l.CreditPaisa, running));
        }
        return rows;
    }

    public async Task<List<LedgerRow>> SupplierLedgerAsync(int supplierId, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        var ap = await db.Accounts.FirstAsync(a => a.Code == AccountCodes.AccountsPayable, ct);

        var lines = await db.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.AccountId == ap.Id && l.SupplierId == supplierId)
            .OrderBy(l => l.JournalEntry!.EntryDateUtc).ThenBy(l => l.Id)
            .ToListAsync(ct);

        long running = 0;
        var rows = new List<LedgerRow>();
        foreach (var l in lines)
        {
            running += l.CreditPaisa - l.DebitPaisa; // payable grows on credit
            rows.Add(new LedgerRow(
                l.JournalEntry!.EntryDateUtc.ToLocalTime(),
                l.JournalEntry.DocumentNo ?? l.JournalEntry.EntryNo,
                l.Memo ?? l.JournalEntry.Description,
                l.DebitPaisa, l.CreditPaisa, running));
        }
        return rows;
    }
}