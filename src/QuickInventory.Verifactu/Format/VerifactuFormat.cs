using System.Globalization;

namespace QuickInventory.Verifactu;

/// <summary>
/// Formato de los valores tal como van en el XML. La huella se calcula con estos mismos textos,
/// así que todo lo que entra en ella tiene que pasar por aquí.
/// </summary>
public static class VerifactuFormat
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Importe con punto decimal y siempre 2 decimales: "123.40", "-5.00".</summary>
    public static string Amount(decimal value) => value.ToString("0.00", Invariant);

    /// <summary>Tipo de IVA sin ceros de sobra: "21", "7.5".</summary>
    public static string Rate(decimal value) => value.ToString("0.##", Invariant);

    /// <summary>Fecha "dd-MM-yyyy".</summary>
    public static string Date(DateOnly value) => value.ToString("dd-MM-yyyy", Invariant);

    /// <summary>Fecha y hora con huso: "2027-01-15T11:20:30+01:00".</summary>
    public static string DateTime(DateTimeOffset value) => value.ToString("yyyy-MM-dd'T'HH:mm:sszzz", Invariant);

    /// <summary>Texto sin espacios al principio ni al final.</summary>
    public static string Text(string? value) => (value ?? "").Trim();

    /// <summary>Lee una fecha "dd-MM-yyyy" de una respuesta de la AEAT.</summary>
    public static DateOnly ParseDate(string value) =>
        DateOnly.ParseExact(value.Trim(), "dd-MM-yyyy", Invariant);

    /// <summary>Lee un importe de una respuesta de la AEAT.</summary>
    public static decimal ParseAmount(string value) =>
        decimal.Parse(value.Trim(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, Invariant);

    /// <summary>
    /// NumSerieFactura: de 1 a 60 caracteres ASCII imprimibles (32 a 126), sin " ' &lt; &gt; =.
    /// </summary>
    public static bool IsValidInvoiceNumber(string value) =>
        value.Length is >= 1 and <= 60 && value.All(c => c is >= ' ' and <= '~' && !"\"'<>=".Contains(c));

    /// <summary>El importe tiene como mucho 2 decimales y 12 cifras enteras (ImporteSgn12.2Type).</summary>
    public static bool IsValidAmount(decimal value) =>
        decimal.Round(value, 2) == value && Math.Abs(value) < 1_000_000_000_000m;
}
