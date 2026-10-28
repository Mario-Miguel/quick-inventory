namespace QuickInventory.Core.Models;

/// <summary>
/// Serie de facturas de un año (p. ej. "F2027"). Es una sola para Taquilla y Tienda: comparten la
/// base de datos, así que no hay riesgo de que dos equipos emitan el mismo número.
/// Cada venta toma el número siguiente a <see cref="LastNumber"/>.
/// </summary>
public class InvoiceSeries
{
    public int Id { get; set; }

    /// <summary>Lo que va delante del número en cada factura (p. ej. "F2027" → "F2027-000146").</summary>
    public string Code { get; set; } = "";
    public int Year { get; set; } = DateTime.Now.Year;
    /// <summary>
    /// Número de la última factura emitida con esta serie. Al crearla puede no ser 0, si ese año ya
    /// se hicieron facturas con otro programa.
    /// </summary>
    public int LastNumber { get; set; }

    public const int MaxCodeLength = 20;

    /// <summary>Número de factura tal como se imprime y se envía a la AEAT (NumSerieFactura).</summary>
    public static string FormatInvoiceId(string code, int number) => $"{code}-{number:D6}";

    /// <summary>Número que llevará la próxima factura de esta serie.</summary>
    public string NextInvoiceId => FormatInvoiceId(Code, LastNumber + 1);

    /// <summary>Código propuesto para la serie de un año: "F2027".</summary>
    public static string SuggestedCode(int year) => "F" + year;

    public InvoiceSeries Clone() => (InvoiceSeries)MemberwiseClone();

    public void Normalize()
    {
        Code = Code?.Trim().ToUpperInvariant() ?? "";
    }

    public void Validate()
    {
        if (Year < 2000 || Year > DateTime.Now.Year + 1)
            throw new BusinessRuleException("El año de la serie no es correcto.");
        if (Code.Length == 0)
            throw new BusinessRuleException("Escribe el código de la serie.");
        if (Code.Length > MaxCodeLength)
            throw new BusinessRuleException($"El código de la serie no puede tener más de {MaxCodeLength} caracteres.");
        // La AEAT solo admite ASCII imprimible sin " ' < > =. Se limita a letras, números, "-" y "/"
        // para que se lea bien en el ticket.
        if (!Code.All(c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-' or '/'))
            throw new BusinessRuleException("El código de la serie solo puede llevar letras sin tilde, números, «-» y «/».");
        if (LastNumber < 0)
            throw new BusinessRuleException("El último número no puede ser negativo.");
    }

    public void CopyFrom(InvoiceSeries other)
    {
        Code = other.Code;
        Year = other.Year;
        LastNumber = other.LastNumber;
    }
}
