using QuickInventory.Core.Models;

namespace QuickInventory.UI;

/// <summary>
/// Dato propio de un tipo de venta que sale como columna en el resumen y en las exportaciones
/// (por ejemplo, "Visita guiada"). <see cref="Text"/> devuelve "" si no aplica a la venta.
/// </summary>
public sealed record SaleField(string Header, Func<Sale, string> Text)
{
    /// <summary>Campo Sí/No que solo tienen las ventas de tipo <typeparamref name="TSale"/>.</summary>
    public static SaleField YesNo<TSale>(string header, Func<TSale, bool> value) where TSale : Sale =>
        new(header, sale => sale is TSale typed ? (value(typed) ? "Sí" : "No") : "");
}

/// <summary>
/// Lo que cambia entre tipos de venta a la hora de enseñarlas: sus columnas propias, cómo se
/// llaman las unidades que se cuentan y los ajustes que se aplican a cada línea.
/// </summary>
/// <param name="Adjustments">
/// Ajustes de la venta que afectan al precio de cada línea, ya en texto
/// (por ejemplo "Visita grupal (-0,50 €)"). Vacío si no hay ninguno.
/// </param>
public sealed record SaleType(
    SalesPoint SalesPoint,
    string CountedUnitsName,
    IReadOnlyList<SaleField> Fields,
    Func<Sale, IEnumerable<string>> Adjustments);

/// <summary>
/// Tipos de venta que existen. Para añadir uno nuevo basta con describirlo aquí:
/// el resumen y las exportaciones lo recogen solos.
/// </summary>
public static class SaleTypes
{
    public static readonly SaleType Ticket = new(
        SalesPoint.TicketOffice,
        "Entradas",
        [
            SaleField.YesNo<TicketSale>("Visita guiada", s => s.GuidedVisit),
            SaleField.YesNo<TicketSale>("Visita grupal", s => s.GroupVisit),
        ],
        sale => sale is TicketSale { GroupVisit: true }
            ? [$"Visita grupal (-{TicketSale.GroupVisitDiscount:C})"]
            : []);

    public static readonly SaleType Shop = new(SalesPoint.Shop, "Unidades contadas", [], _ => []);

    /// <summary>Todos los tipos, en el orden en que salen (por ejemplo, las hojas del Excel).</summary>
    public static IReadOnlyList<SaleType> All { get; } = [Ticket, Shop];

    /// <summary>Columnas de todos los tipos, para listados que mezclan ventas de varios tipos.</summary>
    public static IReadOnlyList<SaleField> AllFields { get; } = [.. All.SelectMany(t => t.Fields)];

    /// <summary>Tipo de venta de un punto de venta, o null si ese punto no vende (Admin).</summary>
    public static SaleType? For(SalesPoint salesPoint) => All.FirstOrDefault(t => t.SalesPoint == salesPoint);

    /// <summary>Ajustes de la venta en texto; vacío si no tiene o su tipo no se conoce.</summary>
    public static IEnumerable<string> Adjustments(Sale sale) => For(sale.SalesPoint)?.Adjustments(sale) ?? [];
}
