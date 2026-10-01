namespace QuickInventory.Core.Models;

/// <summary>
/// Cualquier cosa que el museo vende: artículos de tienda, bebidas, entradas...
/// Las entradas y los servicios no controlan stock (StockControl = false).
/// </summary>
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string? BarCode { get; set; }

    public decimal SalePrice { get; set; }
    public decimal CostPrice { get; set; }

    public bool StockControl { get; set; } = true;

    /// <summary>
    /// Solo cambia mediante movimientos de stock (entradas, salidas, ventas),
    /// para que siempre quede registrado el description.
    /// </summary>
    public int Stock { get; set; }
    public int MinStock { get; set; }

    public bool Active { get; set; } = true;
    public DateTime RegistrationDate { get; set; }

    public bool LowStock => StockControl && Stock <= MinStock;

    public Product Clone() => (Product)MemberwiseClone();
}
