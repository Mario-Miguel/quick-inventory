namespace QuickInventory.Core.Models;

public enum CashMovementType
{
    Income,
    Pay
}

/// <summary>
/// Input o salida de dinero que no es una venta de mostrador:
/// subvenciones, visitas concertadas, pagos a proveedores, facturas, reembolsos, etc.
/// </summary>
public class CashMovement
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public CashMovementType Type { get; set; }
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    /// <summary>Importe siempre en positivo: si entra o sale dinero lo dice <see cref="Type"/>.</summary>
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// Caja de la que sale (o a la que entra) el dinero, o null si no pasa por ninguna caja.
    /// Los movimientos en efectivo de una caja se tienen en cuenta al cerrarla.
    /// </summary>
    public SalesPoint? SalesPoint { get; set; }
    /// <summary>Persona a la que se paga o que paga (por ejemplo, el trabajador al que se le reembolsa).</summary>
    public string? Person { get; set; }
    public string? InvoiceNumber { get; set; }
    /// <summary>
    /// Nombre del archivo de la factura adjunta, o null si no tiene. El contenido está en
    /// <see cref="InvoiceFile"/>, aparte, para no cargarlo cada vez que se listan los movimientos.
    /// </summary>
    public string? InvoiceFileName { get; set; }
    /// <summary>Anulado por error. No se borra para que quede el historial.</summary>
    public bool Canceled { get; set; }

    /// <summary>
    /// Categoría de los reembolsos: dinero que se devuelve a un trabajador que pagó
    /// una compra del museo y trae la factura.
    /// </summary>
    public const string RefundCategory = "Reembolso";
}

