namespace QuickInventory.Core.Models;

public enum PaymentMethod
{
    Cash,
    Card,
    Bizum,
    BankTransfer
}

// Modelos preparados para los módulos de Sales, Ingresos y Pagos.
// Ya forman parte de la base de datos, aunque sus pantallas aún no existen.

public class Sale
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    /// <summary>Perfil que hizo la venta, para separar lo de Taquilla y lo de Tienda.</summary>
    public SalesPoint SalesPoint { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public decimal Total { get; set; }
    public bool Canceled { get; set; }
    public List<SalesLine> Lineas { get; set; } = [];
}

public class SalesLine
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Copia del nombre en el momento de la venta, por si luego cambia.</summary>
    public string Description { get; set; } = "";
    public int Amount { get; set; }
    /// <summary>Copia del precio en el momento de la venta, por si luego cambia.</summary>
    public decimal UnitPrice { get; set; }

    public decimal Subtotal => Amount * UnitPrice;
}

public enum CashMovementType
{
    Income,
    Pay
}

/// <summary>
/// Input o salida de dinero que no es una venta de mostrador:
/// subvenciones, visitas concertadas, pagos a proveedores, facturas, etc.
/// </summary>
public class CashMovement
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public CashMovementType Type { get; set; }
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? Notes { get; set; }
}
