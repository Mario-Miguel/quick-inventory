namespace QuickInventory.Core.Models;

public enum PaymentMethod
{
    Cash,
    Card,
    BankTransfer
}

// Modelos preparados para los módulos de Sales, Ingresos y Pagos.
// Ya forman parte de la base de datos, aunque sus pantallas aún no existen.

public abstract class Sale
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    /// <summary>Perfil que hizo la venta, para separar lo de Taquilla y lo de Tienda.</summary>
    public SalesPoint SalesPoint { get; protected set; }
    public PaymentMethod PaymentMethod { get; set; }
    public decimal Total { get; set; }
    public bool Canceled { get; set; }

    public string? InvoiceSeries { get; set; }
    public int? InvoiceNumber { get; set; }
    public DateOnly? InvoiceDate { get; set; }
    public string? InvoiceId => InvoiceSeries is null || InvoiceNumber is null
        ? null
        : Models.InvoiceSeries.FormatInvoiceId(InvoiceSeries, InvoiceNumber.Value);

    public InvoiceType? InvoiceType { get; set; }
    public int? RectifiedSaleId { get; set; }

    public List<SalesLine> Lines { get; set; } = [];
}
