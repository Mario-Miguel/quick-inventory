namespace QuickInventory.Core.Models;

/// <summary>
/// Línea de una factura de proveedor. Si lleva <see cref="ProductId"/> es un artículo de Tienda y, al
/// apuntar la factura, suma <see cref="Quantity"/> a su stock y actualiza su precio de coste. Si no,
/// es una línea libre (luz, una reparación...) que no toca el inventario.
/// </summary>
public class PurchaseInvoiceLine
{
    public int Id { get; set; }
    public int PurchaseInvoiceId { get; set; }

    /// <summary>Artículo de Tienda, o null en una línea libre.</summary>
    public int? ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>
    /// Qué se compró. En los artículos es una copia del nombre del producto, para que la factura
    /// no cambie si después se renombra.
    /// </summary>
    public string Description { get; set; } = "";
    public int Quantity { get; set; } = 1;
    /// <summary>Precio por unidad sin IVA, tal como viene en la factura.</summary>
    public decimal UnitPrice { get; set; }
    /// <summary>IVA de la línea (0 % si está exenta).</summary>
    public TaxRate TaxRate { get; set; } = TaxRate.TR2100;

    /// <summary>Base imponible de la línea: <see cref="Quantity"/> × <see cref="UnitPrice"/>, redondeada.</summary>
    public decimal BaseAmount => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);

    public bool IsFree => ProductId is null;

    public const int MaxDescriptionLength = 200;
    public const int MaxQuantity = 100_000;

    /// <summary>Línea nueva para un artículo: propone su coste actual y su IVA de venta (0 % si está exento).</summary>
    public static PurchaseInvoiceLine For(ShopProduct product) => new()
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

/// <summary>Parte de una factura de proveedor con el mismo IVA.</summary>
public sealed record PurchaseTaxItem(TaxRate TaxRate, decimal BaseAmount, decimal TaxAmount)
{
    public decimal Total => BaseAmount + TaxAmount;
}
