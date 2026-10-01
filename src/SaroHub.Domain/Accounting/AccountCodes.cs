// src/SaroHub.Domain/Accounting/AccountCodes.cs
namespace SaroHub.Domain.Accounting;

/// <summary>Fixed account codes used by the automatic posting engine.</summary>
public static class AccountCodes
{
    public const string CashInHand = "1010";
    public const string BankAccount = "1020";
    public const string MobileWallet = "1030";
    public const string CardClearing = "1040";
    public const string AccountsReceivable = "1100";
    public const string Inventory = "1200";

    public const string AccountsPayable = "2100";
    public const string CustomerAdvances = "2200";
    public const string TaxPayable = "2300";

    public const string OwnerCapital = "3100";
    public const string OwnerDrawings = "3200";
    public const string OpeningBalanceEq = "3900";

    public const string Sales = "4100";
    public const string SalesDiscount = "4200"; // contra income (debit balance)
    public const string CashOverage = "4900";

    public const string CostOfGoodsSold = "5100";
    public const string ExpenseRoot = "5200";
    public const string StockWriteOff = "5900";
    public const string CashShortage = "5910";
}