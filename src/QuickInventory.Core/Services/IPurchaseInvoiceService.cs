using QuickInventory.Core.Models;

namespace QuickInventory.Core.Services;

/// <summary>Facturas de compra: las que nos emiten los proveedores.</summary>
public interface IPurchaseInvoiceService
{
    Task<List<PurchaseInvoice>> GetInvoicesAsync(DateTime from, DateTime to);
    Task<List<PurchaseInvoice>> GetPaidInvoicesAsync(DateTime from, DateTime to);
    Task<List<PurchaseInvoice>> GetUnpaidInvoicesAsync();
    Task<PurchaseInvoice> RegisterInvoiceAsync(PurchaseInvoice invoice, InvoiceFile file);
    Task SetPaymentAsync(int invoiceId, DateTime? paymentDate, PaymentMethod? paymentMethod, SalesPoint? salesPoint);
    Task<List<PurchaseInvoice>> GetCashPaymentsAsync(DateTime day, SalesPoint salesPoint);
    Task CancelInvoiceAsync(int invoiceId);
    Task<InvoiceFile?> GetInvoiceFileAsync(int invoiceId);
    Task<List<InvoiceFile>> GetInvoiceFilesAsync(IReadOnlyCollection<int> invoiceIds);
}
