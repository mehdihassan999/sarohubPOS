// src/SaroHub.Domain/Security/Permissions.cs
namespace SaroHub.Domain.Security;

public static class Permissions
{
    public const string SaleCreate = "sale.create";
    public const string SaleViewAll = "sale.view.all";
    public const string PaymentReceive = "payment.receive";
    public const string PaymentSend = "payment.send";
    public const string ProductView = "product.view";
    public const string ProductEdit = "product.edit";
    public const string ProductViewCost = "product.view.cost";
    public const string StockEdit = "stock.edit";
    public const string CustomerView = "customer.view";
    public const string CustomerEdit = "customer.edit";
    public const string SupplierView = "supplier.view";
    public const string SupplierEdit = "supplier.edit";
    public const string ExpenseCreate = "expense.create";
    public const string ReportsView = "reports.view";
    public const string ProfitView = "profit.view";
    public const string AccountingView = "accounting.view";
    public const string DayClose = "day.close";
    public const string DayReopen = "day.reopen";
    public const string BackupRun = "backup.run";
    public const string BackupRestore = "backup.restore";
    public const string UsersManage = "users.manage";
    public const string SettingsManage = "settings.manage";
    public const string AuditView = "audit.view";

    public static readonly string[] All =
    {
        SaleCreate, SaleViewAll, PaymentReceive, PaymentSend, ProductView, ProductEdit,
        ProductViewCost, StockEdit, CustomerView, CustomerEdit, SupplierView, SupplierEdit,
        ExpenseCreate, ReportsView, ProfitView, AccountingView, DayClose, DayReopen,
        BackupRun, BackupRestore, UsersManage, SettingsManage, AuditView
    };

    public static readonly string[] Cashier =
    {
        SaleCreate, PaymentReceive, ProductView, CustomerView, CustomerEdit
    };

    public static readonly string[] Manager =
    {
        SaleCreate, SaleViewAll, PaymentReceive, PaymentSend, ProductView, ProductEdit,
        ProductViewCost, StockEdit, CustomerView, CustomerEdit, SupplierView, SupplierEdit,
        ExpenseCreate, ReportsView, ProfitView, DayClose, BackupRun
    };

    public static readonly string[] Accountant =
    {
        SaleViewAll, ProductView, ProductViewCost, CustomerView, SupplierView,
        ReportsView, ProfitView, AccountingView, ExpenseCreate, AuditView
    };
}