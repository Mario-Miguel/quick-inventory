namespace QuickInventory.Core.Models;

/// <summary>
/// Serie de las facturas a clientes (<see cref="CustomerInvoice"/>) de un año (p. ej. "FC2027"). Va
/// aparte de la de los tickets: la ley permite series distintas para tipos de factura distintos, y así
/// cada una es correlativa sin huecos.
/// </summary>
public class CustomerInvoiceSeries : NumberingSeries
{
    /// <summary>Código propuesto para la serie de un año: "FC2027".</summary>
    public static string SuggestedCode(int year) => "FC" + year;

    public CustomerInvoiceSeries Clone() => (CustomerInvoiceSeries)MemberwiseClone();
}
