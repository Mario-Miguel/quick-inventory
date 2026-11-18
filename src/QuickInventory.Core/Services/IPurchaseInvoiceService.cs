using QuickInventory.Core.Models;

namespace QuickInventory.Core.Services;

/// <summary>Facturas de compra: las que nos emiten los proveedores.</summary>
public interface IPurchaseInvoiceService
{
    /// <summary>Facturas con fecha de factura entre dos días (el último sin incluir), con su proveedor y sus líneas.</summary>
    Task<List<PurchaseInvoice>> GetInvoicesAsync(DateTime from, DateTime to);

    /// <summary>
    /// Facturas pagadas entre dos días (por la fecha de pago; el último sin incluir), sin las anuladas,
    /// con su proveedor y sus líneas. Es el dinero que ha salido para pagar a proveedores.
    /// </summary>
    Task<List<PurchaseInvoice>> GetPaidInvoicesAsync(DateTime from, DateTime to);

    /// <summary>Todas las facturas pendientes de pago (sin las anuladas), sea cual sea su fecha, con su proveedor y sus líneas.</summary>
    Task<List<PurchaseInvoice>> GetUnpaidInvoicesAsync();

    /// <summary>
    /// Apunta una factura. De <paramref name="invoice"/> se toman el proveedor, el número, la fecha, las
    /// líneas, el pago y las notas; los totales se calculan y la fecha de registro la pone el servicio.
    /// Cada línea de un artículo suma su cantidad al stock (con un movimiento de tipo
    /// <see cref="StockMovementType.Purchase"/>) y deja su precio como precio de coste del artículo.
    /// El archivo es obligatorio. No se puede apuntar dos veces el mismo número del mismo proveedor
    /// (salvo que la anterior esté anulada).
    /// </summary>
    Task<PurchaseInvoice> RegisterInvoiceAsync(PurchaseInvoice invoice, InvoiceFile file);

    /// <summary>
    /// Marca la factura como pagada en <paramref name="paymentDate"/> con <paramref name="paymentMethod"/>,
    /// o como pendiente si <paramref name="paymentDate"/> es null. Si se paga en efectivo,
    /// <paramref name="salesPoint"/> es la caja de la que sale el dinero, y se resta al cerrarla ese día.
    /// </summary>
    Task SetPaymentAsync(int invoiceId, DateTime? paymentDate, PaymentMethod? paymentMethod, SalesPoint? salesPoint);

    /// <summary>
    /// Facturas pagadas en efectivo con dinero de la caja de <paramref name="salesPoint"/> el día
    /// <paramref name="day"/>, sin las anuladas, con su proveedor. Es lo que se resta al cerrar la caja.
    /// </summary>
    Task<List<PurchaseInvoice>> GetCashPaymentsAsync(DateTime day, SalesPoint salesPoint);

    /// <summary>
    /// Anula una factura apuntada por error y resta del stock lo que sumó. Se conserva en el historial.
    /// Falla si alguno de sus artículos ya no tiene bastantes unidades (porque se han vendido).
    /// </summary>
    Task CancelInvoiceAsync(int invoiceId);

    /// <summary>Archivo de la factura, o null si ya no existe.</summary>
    Task<InvoiceFile?> GetInvoiceFileAsync(int invoiceId);

    /// <summary>Archivos de varias facturas a la vez (para exportarlos).</summary>
    Task<List<InvoiceFile>> GetInvoiceFilesAsync(IReadOnlyCollection<int> invoiceIds);
}
