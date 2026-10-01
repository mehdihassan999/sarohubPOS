// src/SaroHub.Core/Dtos/Dtos.cs
using SaroHub.Domain;

namespace SaroHub.Core.Dtos;

public record ProductSearchItem(
    int Id, string Name, string? Sku, string? BrandName, string UnitSymbol,
    long SalePricePaisa, long StockQtyRaw, long MinStockQtyRaw);

public record NewSaleLine(
    int ProductId, int UnitId, decimal Qty, long UnitPricePaisa, long DiscountPaisa);

public record NewSalePayment(PaymentMethod Method, long AmountPaisa, string? Reference);

public record NewSaleRequest(
    Guid ClientOperationId,
    int? CustomerId,
    IReadOnlyList<NewSaleLine> Lines,
    IReadOnlyList<NewSalePayment> Payments,
    long InvoiceDiscountPaisa,
    long TaxPaisa,
    string? Note);

public record SaleResult(int SaleId, string InvoiceNo, long TotalPaisa, long ChangePaisa, long KhataPaisa);

public record NewStockInLine(int ProductId, int UnitId, decimal Qty, long UnitCostPaisa);

public record NewStockInRequest(
    Guid ClientOperationId,
    int? SupplierId,
    string? SupplierInvoiceNo,
    IReadOnlyList<NewStockInLine> Lines,
    long DiscountPaisa,
    long PaidPaisa,
    PaymentMethod PaidMethod,
    bool IsOpening);

public record TrialBalanceRow(string Code, string Name, long DebitPaisa, long CreditPaisa);

public record ProfitAndLossReport(
    long SalesPaisa, long DiscountPaisa, long CogsPaisa, long GrossProfitPaisa,
    IReadOnlyList<(string Name, long AmountPaisa)> Expenses, long TotalExpensesPaisa, long NetProfitPaisa);

public record BalanceSheetReport(
    IReadOnlyList<(string Name, long AmountPaisa)> Assets, long TotalAssets,
    IReadOnlyList<(string Name, long AmountPaisa)> Liabilities, long TotalLiabilities,
    IReadOnlyList<(string Name, long AmountPaisa)> Equity, long TotalEquity,
    long PeriodProfitPaisa);

public record DashboardSnapshot(
    long SalesPaisa, long ProfitPaisa, long CashPaisa, long ReceivablePaisa,
    long PayablePaisa, long ExpensesPaisa, int BillCount);

public record LowStockRow(int ProductId, string Name, long StockQtyRaw, long MinStockQtyRaw, string UnitSymbol);

public record RecentSaleRow(int SaleId, string InvoiceNo, DateTime AtLocal, string Customer, long TotalPaisa);

public record KhataRow(int PartyId, string Name, string? Phone, long BalancePaisa);

public record LedgerRow(DateTime AtLocal, string DocumentNo, string Description, long DebitPaisa, long CreditPaisa, long RunningPaisa);

public record DayCloseSummary(
    int SessionId, DateTime OpenedAtLocal, long OpeningCashPaisa, long CashSalesPaisa,
    long CreditSalesPaisa, long KhataReceiptsPaisa, long CashExpensesPaisa,
    long CashPurchasesPaisa, long ExpectedCashPaisa, long TotalSalesPaisa, long ProfitPaisa);