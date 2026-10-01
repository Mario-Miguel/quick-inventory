namespace QuickInventory.Core.Models;

/// <summary>
/// Categorías fijas de producto. Es un record para que dos categorías con el mismo
/// valor sean iguales (lo necesita el desplegable para marcar la opción elegida).
/// </summary>
public sealed record ProductCategory
{
    private ProductCategory(string value, string name, SalesPoint salesPoint)
    {
        Value = value;
        Name = name;
        SalesPoint = salesPoint;
    }

    /// <summary>Lo que se guarda en la base de datos. No cambiarlo una vez en uso.</summary>
    public string Value { get; }

    /// <summary>Texto que ve el usuario.</summary>
    public string Name { get; }

    /// <summary>Perfil que vende los productos de esta categoría.</summary>
    public SalesPoint SalesPoint { get; }

    public static readonly ProductCategory Ticket = new("Ticket", "Entradas", SalesPoint.TicketOffice);
    public static readonly ProductCategory Shop = new("Shop", "Tienda", SalesPoint.Shop);

    /// <summary>Todas las categorías, en el orden en que salen en el desplegable.</summary>
    public static IReadOnlyList<ProductCategory> All { get; } = [Ticket, Shop];

    public static ProductCategory FromValue(string value) =>
        All.FirstOrDefault(c => c.Value == value) ?? Shop;

    public override string ToString() => Name;
}


/// <summary>
/// Cualquier cosa que el museo vende: artículos de tienda, bebidas, entradas...
/// Las entradas y los servicios no controlan stock (StockControl = false).
/// </summary>
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    
    public ProductCategory Category { get; set; } = ProductCategory.Shop;
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
