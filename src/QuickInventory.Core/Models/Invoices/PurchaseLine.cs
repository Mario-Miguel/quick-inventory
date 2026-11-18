namespace QuickInventory.Core.Models;

/// <summary>
/// Lo común a las líneas de lo que nos mandan los proveedores (<see cref="PurchaseInvoiceLine"/> y
/// <see cref="DeliveryNoteLine"/>). Si lleva <see cref="ProductId"/> es un artículo de Tienda; si no,
/// es una línea libre (luz, una reparación...) que no toca el inventario.
/// </summary>
public abstract class PurchaseLine
{
    public int Id { get; set; }

    /// <summary>Artículo de Tienda, o null en una línea libre.</summary>
    public int? ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>
    /// Qué se compró. En los artículos es una copia del nombre del producto, para que la factura
    /// o el albarán no cambien si después se renombra.
    /// </summary>
    public string Description { get; set; } = "";
    public int Quantity { get; set; } = 1;
    /// <summary>Precio por unidad sin IVA, tal como viene en el papel (0 si no viene).</summary>
    public decimal UnitPrice { get; set; }
    /// <summary>IVA de la línea (0 % si está exenta).</summary>
    public TaxRate TaxRate { get; set; } = TaxRate.TR2100;

    /// <summary>Base imponible de la línea: <see cref="Quantity"/> × <see cref="UnitPrice"/>, redondeada.</summary>
    public decimal BaseAmount => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);

    public bool IsFree => ProductId is null;

    public const int MaxDescriptionLength = 200;
    public const int MaxQuantity = 100_000;

    /// <summary>Línea nueva para un artículo: propone su coste actual y su IVA de venta (0 % si está exento).</summary>
    protected static TLine ForProduct<TLine>(ShopProduct product) where TLine : PurchaseLine, new() => new()
    {
        ProductId = product.Id,
        Product = product,
        Description = product.Name,
        UnitPrice = product.CostPrice,
        TaxRate = product.TaxRate ?? TaxRate.TR000,
    };

    public void Normalize()
    {
        Description = Description?.Trim() ?? "";
        UnitPrice = Math.Round(UnitPrice, 2, MidpointRounding.AwayFromZero);
    }

    public void Validate()
    {
        if (Description.Length == 0)
            throw new BusinessRuleException("Escribe qué se compró en todas las líneas.");
        if (Description.Length > MaxDescriptionLength)
            throw new BusinessRuleException($"El concepto de una línea no puede tener más de {MaxDescriptionLength} caracteres.");
        if (Quantity < 1)
            throw new BusinessRuleException($"La cantidad de «{Description}» tiene que ser al menos 1.");
        if (Quantity > MaxQuantity)
            throw new BusinessRuleException($"La cantidad de «{Description}» es demasiado grande.");
        if (UnitPrice < 0)
            throw new BusinessRuleException($"El precio de «{Description}» no puede ser negativo.");
    }
}

/// <summary>Parte de una factura o un albarán de proveedor con el mismo IVA.</summary>
public sealed record PurchaseTaxItem(TaxRate TaxRate, decimal BaseAmount, decimal TaxAmount)
{
    public decimal Total => BaseAmount + TaxAmount;

    /// <summary>
    /// Desglose del IVA: las líneas se agrupan por tipo y la cuota se calcula sobre la suma de sus bases
    /// (no línea a línea), que es como lo hacen las facturas. De mayor a menor tipo.
    /// </summary>
    public static IReadOnlyList<PurchaseTaxItem> Breakdown(IEnumerable<PurchaseLine> lines) =>
    [
        .. lines
            .GroupBy(l => l.TaxRate)
            .Select(g =>
            {
                var baseAmount = g.Sum(l => l.BaseAmount);
                var tax = Math.Round(baseAmount * g.Key.Value / 100m, 2, MidpointRounding.AwayFromZero);
                return new PurchaseTaxItem(g.Key, baseAmount, tax);
            })
            .OrderByDescending(i => i.TaxRate.Value)
    ];
}
