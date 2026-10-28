using System.Text;

namespace QuickInventory.Verifactu;

/// <summary>Qué provoca un código de error de la AEAT.</summary>
public enum AeatErrorKind
{
    Unknown,
    /// <summary>Rechaza el envío entero.</summary>
    RejectsSubmission,
    /// <summary>Rechaza el registro (o el envío, si el error está en la cabecera).</summary>
    RejectsRecord,
    /// <summary>Acepta el registro, pero hay que subsanarlo.</summary>
    AcceptedWithErrors,
}

/// <summary>
/// Códigos de error de la AEAT, leídos de errores.properties (incrustado). Es el archivo de la AEAT pasado a UTF-8
/// (el original está en ISO-8859-1) y con las líneas de asteriscos renombradas como <see cref="AeatErrorKind"/>.
/// </summary>
public static class ErrorCatalog
{
    private static readonly Lazy<Dictionary<int, (AeatErrorKind Kind, string Description)>> Errors = new(Load);

    public static string? Describe(int code) => Errors.Value.TryGetValue(code, out var e) ? e.Description : null;

    public static AeatErrorKind KindOf(int code) => Errors.Value.TryGetValue(code, out var e) ? e.Kind : AeatErrorKind.Unknown;

    /// <summary>
    /// El archivo tiene tres listas, cada una tras una línea de asteriscos con su tipo: RejectsSubmission,
    /// RejectsRecord y AcceptedWithErrors. Las demás líneas son "1234 = Descripción".
    /// </summary>
    private static Dictionary<int, (AeatErrorKind, string)> Load()
    {
        using var stream = typeof(ErrorCatalog).Assembly.GetManifestResourceStream("Schemas.errores.properties")
            ?? throw new VerifactuException("Falta el catálogo de errores incrustado.");
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var errors = new Dictionary<int, (AeatErrorKind, string)>();
        var kind = AeatErrorKind.Unknown;
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith('*'))
            {
                kind = line.Contains("RejectsSubmission") ? AeatErrorKind.RejectsSubmission
                    : line.Contains("AcceptedWithErrors") ? AeatErrorKind.AcceptedWithErrors
                    : AeatErrorKind.RejectsRecord;
                continue;
            }
            var parts = line.Split('=', 2);
            if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out var code))
                errors[code] = (kind, parts[1].Trim());
        }
        return errors;
    }
}
