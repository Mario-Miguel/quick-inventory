namespace QuickInventory.Core.Models;

/// <summary>
/// Línea de una factura de proveedor. Si es de un artículo de Tienda, al apuntar la factura suma
/// <see cref="PurchaseLine.Quantity"/> a su stock y actualiza su precio de coste; salvo que venga de un
/// albarán (<see cref="DeliveryNoteId"/>), porque esas unidades ya se sumaron al apuntar el albarán.
/// </summary>
public class PurchaseInvoiceLine : PurchaseLine
{
    public int PurchaseInvoiceId { get; set; }

    /// <summary>
    /// Albarán del que se copió la línea, o null. Estas líneas no suman stock (ya lo sumó el albarán);
    /// solo ponen el precio que cobra la factura.
    /// </summary>
    public int? DeliveryNoteId { get; set; }

    public bool IsFromDeliveryNote => DeliveryNoteId is not null;

    /// <summary>Línea nueva para un artículo: propone su coste actual y su IVA de venta (0 % si está exento).</summary>
    public static PurchaseInvoiceLine For(ShopProduct product) => ForProduct<PurchaseInvoiceLine>(product);

    /// <summary>Copia las líneas de un albarán para añadirlas a la factura (sin sumar stock).</summary>
    public static IEnumerable<PurchaseInvoiceLine> From(DeliveryNote note) => note.Lines.Select(l => new PurchaseInvoiceLine
    {
        DeliveryNoteId = note.Id,
        ProductId = l.ProductId,
        Product = l.Product,
        Description = l.Description,
        Quantity = l.Quantity,
        UnitPrice = l.UnitPrice,
        TaxRate = l.TaxRate,
    });
}
