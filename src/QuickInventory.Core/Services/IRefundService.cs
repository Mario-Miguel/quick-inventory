using QuickInventory.Core.Models;

namespace QuickInventory.Core.Services;

/// <summary>
/// Reembolsos: dinero que se devuelve a un trabajador que pagó una compra del museo y
/// trae la factura. Se guardan como <see cref="CashMovement"/> de tipo pago.
/// </summary>
public interface IRefundService
{
    /// <summary>
    /// Apunta un reembolso. De <paramref name="refund"/> se toman la persona, el concepto, la
    /// factura, el importe, las notas y de dónde sale el dinero: en efectivo de la caja de
    /// Taquilla o de Tienda (<see cref="CashMovement.SalesPoint"/>), o por transferencia
    /// (sin caja). La fecha y el tipo los pone el servicio. La factura es opcional: también
    /// se puede adjuntar después con <see cref="AttachInvoiceAsync"/>.
    /// </summary>
    Task<CashMovement> RegisterRefundAsync(CashMovement refund, InvoiceFile? invoice = null);
    Task CancelRefundAsync(int refundId);

    /// <summary>Adjunta la factura a un reembolso. Si ya tenía una, la sustituye.</summary>
    Task AttachInvoiceAsync(int refundId, InvoiceFile invoice);
    Task RemoveInvoiceAsync(int refundId);
    /// <summary>Factura de un reembolso, o null si no tiene.</summary>
    Task<InvoiceFile?> GetInvoiceAsync(int refundId);
    /// <summary>Facturas de varios reembolsos a la vez (para exportarlas). Los que no tienen no salen.</summary>
    Task<List<InvoiceFile>> GetInvoicesAsync(IReadOnlyCollection<int> refundIds);
    /// <summary>Reembolsos entre dos fechas; con <paramref name="salesPoint"/>, solo los pagados desde esa caja.</summary>
    Task<List<CashMovement>> GetRefundsAsync(DateTime from, DateTime to, SalesPoint? salesPoint = null);
}
