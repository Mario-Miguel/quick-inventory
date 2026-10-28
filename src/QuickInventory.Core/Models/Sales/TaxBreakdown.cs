namespace QuickInventory.Core.Models;

/// <summary>
/// Parte de una venta con el mismo IVA: su base imponible, su cuota y el total con IVA.
/// <see cref="TaxBase"/> y <see cref="TaxAmount"/> son null en ventas antiguas sin IVA indicado.
/// </summary>
public sealed record TaxBreakdownItem(
    decimal? TaxRate, TaxExemption? TaxExemption, decimal? TaxBase, decimal? TaxAmount, decimal Total)
{
    /// <summary>Texto para el usuario: "21 %", "Exento (E1)" o "Sin IVA indicado".</summary>
    public string Name =>
        TaxRate is { } rate ? $"{rate:0.##} %"
        : TaxExemption is { } exemption ? $"Exento ({exemption.Value})"
        : "Sin IVA indicado";
}

/// <summary>
/// Desglose del IVA de una venta. Es el mismo cálculo que irá en el registro VERI*FACTU.
/// </summary>
public static class TaxBreakdown
{
    /// <summary>
    /// Agrupa las líneas por tipo de IVA. Los precios ya llevan el IVA, así que para cada tipo
    /// base = total / (1 + tipo / 100), redondeada a céntimos, y cuota = total − base.
    /// Se redondea por tipo y no por línea, para que base + cuota sea exactamente lo cobrado.
    /// Orden: de mayor a menor tipo, después las exenciones y al final lo que no tiene IVA indicado.
    /// </summary>
    public static IReadOnlyList<TaxBreakdownItem> Calculate(IEnumerable<SalesLine> lines) =>
    [
        .. lines
            .GroupBy(l => (l.TaxRate, l.TaxExemption))
            .Select(g => Item(g.Key.TaxRate, g.Key.TaxExemption, g.Sum(l => l.Subtotal)))
            .OrderBy(i => i.TaxRate is not null ? 0 : i.TaxExemption is not null ? 1 : 2)
            .ThenByDescending(i => i.TaxRate)
            .ThenBy(i => i.TaxExemption?.Value)
    ];

    private static TaxBreakdownItem Item(decimal? taxRate, TaxExemption? taxExemption, decimal total)
    {
        if (taxRate is { } rate)
        {
            var taxBase = Math.Round(total / (1 + rate / 100m), 2, MidpointRounding.AwayFromZero);
            return new(rate, null, taxBase, total - taxBase, total);
        }
        if (taxExemption is not null)
            return new(null, taxExemption, total, 0, total);
        return new(null, null, null, null, total);
    }
}
