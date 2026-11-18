namespace QuickInventory.Core.Models;

/// <summary>
/// Serie de los tickets (facturas simplificadas) de un año (p. ej. "F2027"). Es una sola para Taquilla y
/// Tienda: comparten la base de datos, así que no hay riesgo de que dos equipos emitan el mismo número.
/// </summary>
public class InvoiceSeries : NumberingSeries
{
    /// <summary>Código propuesto para la serie de un año: "F2027".</summary>
    public static string SuggestedCode(int year) => "F" + year;

    public InvoiceSeries Clone() => (InvoiceSeries)MemberwiseClone();
}
