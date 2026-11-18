namespace QuickInventory.Core.Models;

/// <summary>
/// Proveedor habitual del museo: quien nos emite las facturas de compra (<see cref="PurchaseInvoice"/>)
/// y los albaranes (<see cref="DeliveryNote"/>).
/// </summary>
public class Provider : Contact
{
    protected override string OfWhom => "del proveedor";

    public Provider Clone() => (Provider)MemberwiseClone();
}
