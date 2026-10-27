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
/// </summary>
public abstract class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool Active { get; set; } = true;
    public decimal SalePrice { get; set; }
    public DateTime RegistrationDate { get; set; }

    public TaxRate? TaxRate { get; set; }
    public TaxExemption? TaxExemption { get; set; }

    public abstract ProductCategory Category { get; }
    // public virtual bool StockControl => false;

    /// <summary>Producto nuevo y vacío de la clase que corresponde a la categoría.</summary>
    public static Product New(ProductCategory category) =>
        category == ProductCategory.Ticket ? new TicketProduct() : new ShopProduct();

    public Product Clone() => (Product)MemberwiseClone();

    public virtual void Normalize()
    {
        Name = Name?.Trim() ?? "";
    }

    public virtual void Validate()
    {
        if (Name.Length == 0)
            throw new BusinessRuleException("El producto necesita un nombre.");
        if (SalePrice < 0)
            throw new BusinessRuleException("Los precios no pueden ser negativos.");
        if (TaxRate is null && TaxExemption is null)
            throw new BusinessRuleException("Indica el IVA del producto o, si está exento, el motivo.");
        if (TaxRate is not null && TaxExemption is not null)
            throw new BusinessRuleException("Un producto exento no puede tener tipo de IVA.");
    }

    public virtual void CopyFrom(Product other)
    {
        Name = other.Name;
        SalePrice = other.SalePrice;
        TaxRate = other.TaxRate;
        TaxExemption = other.TaxExemption;
    }
}
