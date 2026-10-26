using System.Security.Cryptography;
using System.Text;

namespace QuickInventory.Verifactu;

/// <summary>
/// Huella (hash) de los registros, según «Detalle de las especificaciones técnicas para generación de la huella»
/// de la AEAT (v0.1.2): "campo=valor&amp;..." con los valores tal como van en el XML, en UTF-8, SHA-256 y
/// hexadecimal en mayúsculas.
/// </summary>
public static class FingerprintCalculator
{
    /// <summary>Huella de un registro de alta. <paramref name="previousFingerprint"/> es null en el primer registro.</summary>
    public static string ForRegistration(string issuerNif, string number, string issueDate, string invoiceType,
        string totalTax, string totalAmount, string? previousFingerprint, string generatedAt) =>
        Compute(Join(
            ("IDEmisorFactura", issuerNif),
            ("NumSerieFactura", number),
            ("FechaExpedicionFactura", issueDate),
            ("TipoFactura", invoiceType),
            ("CuotaTotal", totalTax),
            ("ImporteTotal", totalAmount),
            ("Huella", previousFingerprint),
            ("FechaHoraHusoGenRegistro", generatedAt)));

    /// <summary>Huella de un registro de anulación.</summary>
    public static string ForCancellation(string issuerNif, string number, string issueDate,
        string? previousFingerprint, string generatedAt) =>
        Compute(Join(
            ("IDEmisorFacturaAnulada", issuerNif),
            ("NumSerieFacturaAnulada", number),
            ("FechaExpedicionFacturaAnulada", issueDate),
            ("Huella", previousFingerprint),
            ("FechaHoraHusoGenRegistro", generatedAt)));

    /// <summary>SHA-256 de la cadena en UTF-8, en hexadecimal y mayúsculas (64 caracteres).</summary>
    public static string Compute(string input) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));

    /// <summary>Un campo sin valor se queda en "Nombre=".</summary>
    private static string Join(params (string Name, string? Value)[] fields) =>
        string.Join("&", fields.Select(f => $"{f.Name}={f.Value?.Trim()}"));

    /// <summary>64 caracteres hexadecimales en mayúsculas.</summary>
    internal static bool IsValid(string fingerprint) =>
        fingerprint.Length == 64 && fingerprint.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
}
