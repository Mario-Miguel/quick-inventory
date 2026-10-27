namespace QuickInventory.Core.Models;

public sealed record TaxExemption
{
    private TaxExemption(string value, string description)
    {
        Value = value;
        Description = description;
    }

    public string Value { get; }
    public string Description { get; }

    public static readonly TaxExemption E1 = new("E1", "Exenta por el artículo 20");
    public static readonly TaxExemption E2 = new("E2", "Exenta por el artículo 21");
    public static readonly TaxExemption E3 = new("E3", "Exenta por el artículo 22");
    public static readonly TaxExemption E4 = new("E4", "Exenta por el artículo 23 y 24");
    public static readonly TaxExemption E5 = new("E5", "Exenta por el artículo 25");
    public static readonly TaxExemption E6 = new("E6", "Exenta por otros");
    // public static readonly TaxExemption E7 = new("E7", "Exenta por ???");
    // public static readonly TaxExemption E8 = new("E8", "Exenta por ???");

    public static IReadOnlyList<TaxExemption> All { get; } = [E1, E2, E3, E4, E5, E6];

    public static TaxExemption FromValue(string value) =>
        All.FirstOrDefault(t => t.Value == value)
        ?? throw new InvalidOperationException($"Tipo de exención desconocido: {value}");

    public override string ToString() => Value;
}

public sealed record TaxRate
{
    private TaxRate(string name, decimal value)
    {
        Name = name;
        Value = value;
    }

    public decimal Value { get; }

    /// <summary>Texto que ve el usuario.</summary>
    public string Name { get; }

    public static readonly TaxRate TR2100 = new("21%", 21);
    public static readonly TaxRate TR1000 = new("10%", 10);
    // public static readonly TaxRate TR750 = new("7,5%", 7.5m);
    // public static readonly TaxRate TR500 = new("5%", 5);
    public static readonly TaxRate TR400 = new("4%", 4);
    // public static readonly TaxRate TR200 = new("2%", 2);
    public static readonly TaxRate TR000 = new("0%", 0);

    /// <summary>Todos los tipos, en el orden en que salen en el desplegable.</summary>
    public static IReadOnlyList<TaxRate> All { get; } = [TR2100, TR1000, TR400, TR000,];

    /// <summary>
    /// Tipo guardado en la base de datos. Si ya no está en <see cref="All"/> falla en vez de
    /// suponer otro, para no facturar con un IVA equivocado.
    /// </summary>
    public static TaxRate FromValue(decimal value) =>
        All.FirstOrDefault(t => t.Value == value)
        ?? throw new InvalidOperationException($"Tipo de IVA desconocido: {value}");

    public override string ToString() => Name;

}