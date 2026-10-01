// src/SaroHub.Domain/Enums.cs
namespace SaroHub.Domain;

public enum AccountType { Asset = 1, Liability = 2, Equity = 3, Income = 4, Expense = 5 }

public enum PaymentMethod { Cash = 1, Card = 2, Bank = 3, Easypaisa = 4, JazzCash = 5, Raast = 6, Khata = 7 }

public enum PartyType { Customer = 1, Supplier = 2 }

public enum StockMovementType
{
    Opening = 1,
    PurchaseIn = 2,
    SaleOut = 3,
    AdjustmentIn = 4,
    AdjustmentOut = 5,
    Damaged = 6,
    Lost = 7
}

public enum SaleStatus { Posted = 1, Reversed = 2 }

public enum CashSessionStatus { Open = 1, Closed = 2 }