namespace QuickInventory.Core.Models;

/// <summary>
/// Línea de un albarán de proveedor. Si es de un artículo de Tienda, al apuntar el albarán suma
/// <see cref="PurchaseLine.Quantity"/> a su stock (y, si viene valorado, su precio pasa a ser el de coste).
/// </summary>
public class DeliveryNoteLine : PurchaseLine
{
    public int DeliveryNoteId { get; set; }

    /// <summary>Línea nueva para un artículo: propone su coste actual y su IVA de venta (0 % si está exento).</summary>
    public static DeliveryNoteLine For(ShopProduct product) => ForProduct<DeliveryNoteLine>(product);
}
