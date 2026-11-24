namespace Invoicing.Models;

/// <summary>
/// Parte de una factura o una venta con el mismo IVA: su base imponible, su cuota y el total con IVA.
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

/// <summary>Desglose del IVA de una factura a un cliente (<see cref="CustomerInvoice.TaxBreakdown"/>).</summary>
public static class InvoiceTaxBreakdown
{
    /// <summary>
    /// Desglose de una factura cuyas líneas llevan el precio <b>sin</b> IVA: por cada tipo, base = suma de
    /// las bases de sus líneas y cuota = base × tipo / 100, redondeada a céntimos (por tipo, no por línea,
    /// que es como se hace en las facturas). Las exentas van aparte, con cuota 0. Orden: de mayor a menor
    /// tipo y después las exenciones.
    /// </summary>
    public static IReadOnlyList<TaxBreakdownItem> FromBases(IEnumerable<CustomerInvoiceLine> lines) =>
    [
        .. lines
            .GroupBy(l => (Rate: l.TaxRate?.Value, l.TaxExemption))
            .Select(g =>
            {
                var taxBase = g.Sum(l => l.BaseAmount);
                var tax = g.Key.Rate is { } rate ? Math.Round(taxBase * rate / 100m, 2, MidpointRounding.AwayFromZero) : 0m;
                return new TaxBreakdownItem(g.Key.Rate, g.Key.Rate is null ? g.Key.TaxExemption : null, taxBase, tax, taxBase + tax);
            })
            .OrderBy(i => i.TaxRate is not null ? 0 : 1)
            .ThenByDescending(i => i.TaxRate)
            .ThenBy(i => i.TaxExemption?.Value)
    ];
}
