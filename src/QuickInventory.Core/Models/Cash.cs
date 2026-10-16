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
    /// <summary>Si es una visita guiada: cada entrada lleva el suplemento <see cref="GuidedVisitSupplement"/>.</summary>
    public bool GuidedVisit { get; set; }
    public bool GroupVisit { get; set; }
    public List<SalesLine> Lines { get; set; } = [];

    /// <summary>Euros que se suman a cada entrada en una visita guiada (no se descuentan).</summary>
    public const decimal GuidedVisitSupplement = 1m;
    public const decimal GroupVisitDiscount = 0.5m;
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
    /// <summary>Copia del precio del producto en el momento de la venta, sin descuento.</summary>
    public decimal BasePrice { get; set; }
    /// <summary>
    /// Precio cobrado por unidad, ya con el descuento aplicado. Es una copia del momento
    /// de la venta, por si luego cambia el precio del producto.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Copia del nombre del descuento aplicado, o null si no tiene.</summary>
    public string? DiscountName { get; set; }
    /// <summary>Copia del porcentaje de descuento aplicado (0 si no tiene).</summary>
    public decimal DiscountPercent { get; set; }

    /// <summary>Copia de <see cref="Product.CountsAs"/> en el momento de la venta, por si luego cambia.</summary>
    public int CountsAs { get; set; } = 1;

    /// <summary>Unidades que cuenta la línea en el recuento (por ejemplo, 3 cupones de 2 = 6 entradas).</summary>
    public int CountedUnits => Amount * CountsAs;

    public decimal Subtotal => Amount * UnitPrice;

    /// <summary>Euros descontados en esta línea.</summary>
    public decimal DiscountTotal => Amount * (BasePrice - UnitPrice);

    public ProductCategory Category { get; set; } = ProductCategory.Shop;
}

/// <summary>
/// Descuento que se aplica a cada unidad de una línea de venta (por ejemplo, a una entrada).
/// <see cref="Amount"/> es el porcentaje que se le debe restar.
/// </summary>
public sealed record SaleLineDiscount
{
    private SaleLineDiscount(string name, decimal amount)
    {
        Name = name;
        Amount = amount;
    }

    /// <summary>Texto que ve el usuario.</summary>
    public string Name { get; }

    /// <summary>Porcentaje que se descuenta del precio de cada unidad (30 = 30 %).</summary>
    public decimal Amount { get; }

    /// <summary>Precio por unidad con el descuento aplicado, redondeado a céntimos.</summary>
    public decimal Apply(decimal price) =>
        Math.Round(price * (100 - Amount) / 100, 2, MidpointRounding.AwayFromZero);

    public static readonly SaleLineDiscount Basic = new("Básico", 10);
    public static readonly SaleLineDiscount RGCC = new("RGCC/CNSO", 30);

    /// <summary>Descuentos fijos que se ofrecen en la pantalla de venta.</summary>
    public static IReadOnlyList<SaleLineDiscount> All { get; } = [Basic, RGCC];

    /// <summary>Descuento con un importe escrito a mano.</summary>
    public static SaleLineDiscount Custom(decimal amount) => new("Personalizado", amount);
}


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


/// <summary>Factura escaneada (PDF o foto) adjunta a un <see cref="CashMovement"/>.</summary>
public class InvoiceFile
{
    public int Id { get; set; }
    public int CashMovementId { get; set; }
    /// <summary>Nombre original del archivo, por ejemplo "factura-ferreteria.pdf".</summary>
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public byte[] Content { get; set; } = [];

    /// <summary>Tamaño máximo de una factura: 10 MB, de sobra para un PDF o una foto del móvil.</summary>
    public const long MaxSize = 10 * 1024 * 1024;

    /// <summary>Tipos de archivo que se aceptan como factura.</summary>
    public static IReadOnlyList<string> AllowedExtensions { get; } = [".pdf", ".jpg", ".jpeg", ".png"];

    /// <summary>Extensión del archivo en minúsculas, por ejemplo ".pdf".</summary>
    public string Extension => Path.GetExtension(FileName).ToLowerInvariant();
}
