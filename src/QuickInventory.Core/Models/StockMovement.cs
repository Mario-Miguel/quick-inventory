namespace QuickInventory.Core.Models;

public enum StockMovementType
{
    Input,
    Output,
    Sale,
    Cancelation,
    /// <summary>Artículos que llegan con una factura de proveedor.</summary>
    Purchase,
    /// <summary>Anulación de una factura de proveedor: se restan las unidades que sumó.</summary>
    PurchaseCancelation
}

/// <summary>
/// Historial de cada cambio de stock. Permite saber en todo momento
/// por qué un product tiene las unidades que tiene.
/// </summary>
public class StockMovement
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Positivo si entra mercancía, negativo si sale.</summary>
    public int Amount { get; set; }
    public StockMovementType Type { get; set; }
    public string? Description { get; set; }
    public DateTime Date { get; set; }

    /// <summary>Sale que originó el movimiento, si la hay.</summary>
    public int? SaleId { get; set; }
    public Sale? Sale { get; set; }

    /// <summary>Factura de proveedor que originó el movimiento, si la hay.</summary>
    public int? PurchaseInvoiceId { get; set; }
}
