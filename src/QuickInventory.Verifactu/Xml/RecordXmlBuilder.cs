using System.Xml.Linq;

namespace QuickInventory.Verifactu;

/// <summary>
/// Genera los registros de alta (RegistroAlta) y de anulación (RegistroAnulacion): valida los datos,
/// calcula la huella encadenada y comprueba el XML resultante contra el esquema de la AEAT.
/// </summary>
/// <remarks>
/// Quien lo use tiene que generar los registros de uno en uno (con un bloqueo, si hay varios equipos) y
/// pasar siempre el último de la cadena como <c>previous</c>: la cadena es una sola para toda la instalación,
/// con altas y anulaciones mezcladas.
/// </remarks>
public sealed class RecordXmlBuilder
{
    private static readonly XNamespace Sf = AeatSchemas.Information;

    private readonly SystemInfo system;
    private readonly AeatEnvironment environment;

    public RecordXmlBuilder(SystemInfo system, AeatEnvironment environment)
    {
        RecordValidator.Validate(system);
        this.system = system;
        this.environment = environment;
    }

    /// <summary>Registro de alta de una factura.</summary>
    /// <param name="previous">Último registro de la cadena; null solo en el primero de toda la instalación.</param>
    /// <param name="generatedAt">Momento de generación con su huso (normalmente <see cref="SpainTime.Now"/>).</param>
    public GeneratedRecord BuildRegistration(InvoiceData invoice, PreviousRecord? previous, DateTimeOffset generatedAt)
    {
        generatedAt = SpainTime.Truncate(generatedAt);
        RecordValidator.Validate(invoice, generatedAt);
        RecordValidator.Validate(previous);

        var nif = VerifactuFormat.Text(invoice.Issuer.Nif);
        var number = VerifactuFormat.Text(invoice.Number);
        var issueDate = VerifactuFormat.Date(invoice.IssueDate);
        var type = invoice.Type.ToString();
        var totalTax = VerifactuFormat.Amount(invoice.TotalTax);
        var totalAmount = VerifactuFormat.Amount(invoice.TotalAmount);
        var generated = VerifactuFormat.DateTime(generatedAt);
        var fingerprint = FingerprintCalculator.ForRegistration(nif, number, issueDate, type, totalTax, totalAmount, previous?.Fingerprint, generated);

        var record = new XElement(Sf + "RegistroAlta",
            new XAttribute(XNamespace.Xmlns + "sf", Sf),
            El("IDVersion", "1.0"),
            El("IDFactura",
                El("IDEmisorFactura", nif),
                El("NumSerieFactura", number),
                El("FechaExpedicionFactura", issueDate)),
            Optional("RefExterna", invoice.ExternalRef),
            El("NombreRazonEmisor", VerifactuFormat.Text(invoice.Issuer.LegalName)),
            invoice.IsCorrection ? El("Subsanacion", "S") : null,
            invoice.PreviousRejection switch
            {
                PreviousRejection.Rejected => El("RechazoPrevio", "S"),
                PreviousRejection.NotInAeat => El("RechazoPrevio", "X"),
                _ => null,
            },
            El("TipoFactura", type),
            invoice.CorrectionType switch
            {
                CorrectionType.Substitution => El("TipoRectificativa", "S"),
                CorrectionType.Differences => El("TipoRectificativa", "I"),
                _ => null,
            },
            invoice.RectifiedInvoices.Count == 0 ? null : El("FacturasRectificadas",
                invoice.RectifiedInvoices.Select(id => El("IDFacturaRectificada",
                    El("IDEmisorFactura", VerifactuFormat.Text(id.IssuerNif)),
                    El("NumSerieFactura", VerifactuFormat.Text(id.Number)),
                    El("FechaExpedicionFactura", VerifactuFormat.Date(id.IssueDate))))),
            invoice.CorrectionType == CorrectionType.Substitution
                ? El("ImporteRectificacion",
                    El("BaseRectificada", VerifactuFormat.Amount(invoice.RectifiedBase!.Value)),
                    El("CuotaRectificada", VerifactuFormat.Amount(invoice.RectifiedTax!.Value)))
                : null,
            El("DescripcionOperacion", VerifactuFormat.Text(invoice.Description)),
            El("Desglose", invoice.TaxLines.Select(TaxLineElement)),
            El("CuotaTotal", totalTax),
            El("ImporteTotal", totalAmount),
            Chaining(previous),
            SystemElement(),
            El("FechaHoraHusoGenRegistro", generated),
            El("TipoHuella", "01"),
            El("Huella", fingerprint));

        var qrUrl = QrUrlBuilder.Build(environment, new InvoiceId(nif, number, invoice.IssueDate), invoice.TotalAmount);
        return Finish(record, fingerprint, generatedAt, qrUrl);
    }

    /// <summary>Registro de anulación de una factura expedida por error.</summary>
    public GeneratedRecord BuildCancellation(CancellationData cancellation, PreviousRecord? previous, DateTimeOffset generatedAt)
    {
        generatedAt = SpainTime.Truncate(generatedAt);
        RecordValidator.Validate(cancellation, generatedAt);
        RecordValidator.Validate(previous);

        var nif = VerifactuFormat.Text(cancellation.Invoice.IssuerNif);
        var number = VerifactuFormat.Text(cancellation.Invoice.Number);
        var issueDate = VerifactuFormat.Date(cancellation.Invoice.IssueDate);
        var generated = VerifactuFormat.DateTime(generatedAt);
        var fingerprint = FingerprintCalculator.ForCancellation(nif, number, issueDate, previous?.Fingerprint, generated);

        var record = new XElement(Sf + "RegistroAnulacion",
            new XAttribute(XNamespace.Xmlns + "sf", Sf),
            El("IDVersion", "1.0"),
            El("IDFactura",
                El("IDEmisorFacturaAnulada", nif),
                El("NumSerieFacturaAnulada", number),
                El("FechaExpedicionFacturaAnulada", issueDate)),
            Optional("RefExterna", cancellation.ExternalRef),
            cancellation.WithoutPriorRecord ? El("SinRegistroPrevio", "S") : null,
            cancellation.PreviouslyRejected ? El("RechazoPrevio", "S") : null,
            Chaining(previous),
            SystemElement(),
            El("FechaHoraHusoGenRegistro", generated),
            El("TipoHuella", "01"),
            El("Huella", fingerprint));

        return Finish(record, fingerprint, generatedAt, qrUrl: null);
    }

    private static GeneratedRecord Finish(XElement record, string fingerprint, DateTimeOffset generatedAt, string? qrUrl)
    {
        // Las validaciones anteriores deberían haberlo evitado; si falla, es un fallo de la librería.
        var errors = AeatSchemas.Validate(record);
        if (errors.Count > 0)
            throw new VerifactuException($"El registro generado no cumple el esquema de la AEAT: {string.Join(" ", errors)}");

        return new GeneratedRecord(record.ToString(SaveOptions.DisableFormatting), fingerprint, generatedAt, qrUrl);
    }

    /// <summary>IVA (Impuesto 01) en régimen general (ClaveRegimen 01): sujeta y no exenta (S1) o exenta.</summary>
    private static XElement TaxLineElement(TaxLine line) => El("DetalleDesglose",
        El("Impuesto", "01"),
        El("ClaveRegimen", "01"),
        line.Exemption is { } exemption
            ? El("OperacionExenta", exemption.ToString())
            : El("CalificacionOperacion", "S1"),
        line.TaxRate is { } rate ? El("TipoImpositivo", VerifactuFormat.Rate(rate)) : null,
        El("BaseImponibleOimporteNoSujeto", VerifactuFormat.Amount(line.TaxBase)),
        line.TaxAmount is { } tax ? El("CuotaRepercutida", VerifactuFormat.Amount(tax)) : null);

    private static XElement Chaining(PreviousRecord? previous) => El("Encadenamiento",
        previous is null
            ? El("PrimerRegistro", "S")
            : El("RegistroAnterior",
                El("IDEmisorFactura", VerifactuFormat.Text(previous.Invoice.IssuerNif)),
                El("NumSerieFactura", VerifactuFormat.Text(previous.Invoice.Number)),
                El("FechaExpedicionFactura", VerifactuFormat.Date(previous.Invoice.IssueDate)),
                El("Huella", previous.Fingerprint)));

    /// <summary>
    /// Solo VERI*FACTU: la librería no admite el modo «No VERI*FACTU».
    /// </summary>
    private XElement SystemElement() => El("SistemaInformatico",
        El("NombreRazon", VerifactuFormat.Text(system.ProducerName)),
        El("NIF", VerifactuFormat.Text(system.ProducerNif)),
        El("NombreSistemaInformatico", VerifactuFormat.Text(system.SystemName)),
        El("IdSistemaInformatico", system.SystemId),
        El("Version", VerifactuFormat.Text(system.Version)),
        El("NumeroInstalacion", VerifactuFormat.Text(system.InstallationNumber)),
        El("TipoUsoPosibleSoloVerifactu", "S"),
        El("TipoUsoPosibleMultiOT", system.SupportsMultipleTaxpayers ? "S" : "N"),
        El("IndicadorMultiplesOT", system.HasMultipleTaxpayers ? "S" : "N"));

    private static XElement El(string name, params object?[] content) => new(Sf + name, content);

    private static XElement? Optional(string name, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : El(name, VerifactuFormat.Text(value));
}
