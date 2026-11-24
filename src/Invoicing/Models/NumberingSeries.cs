namespace Invoicing.Models;

/// <summary>
/// Lo común a las series de numeración de un año: la de las facturas a clientes
/// (<see cref="CustomerInvoiceSeries"/>) y las que defina la aplicación (p. ej. tickets). Cada documento
/// toma el número siguiente a <see cref="LastNumber"/>. No es una tabla: cada serie tiene la suya.
/// </summary>
public abstract class NumberingSeries
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

    public void CopyFrom(NumberingSeries other)
    {
        Code = other.Code;
        Year = other.Year;
        LastNumber = other.LastNumber;
    }
}
