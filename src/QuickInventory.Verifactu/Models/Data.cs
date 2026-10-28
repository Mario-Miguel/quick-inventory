namespace QuickInventory.Verifactu;

/// <summary>Obligado a expedir las facturas (el negocio que factura).</summary>
public sealed record InvoiceIssuer(string Nif, string LegalName);

/// <summary>Identificación de una factura (IDFactura): NIF del emisor, número con la serie y fecha de expedición.</summary>
public sealed record InvoiceId(string IssuerNif, string Number, DateOnly IssueDate);

/// <summary>
/// Registro inmediatamente anterior de la cadena, sea de alta o de anulación. En una anulación,
/// <see cref="Invoice"/> es la factura anulada.
/// </summary>
public sealed record PreviousRecord(InvoiceId Invoice, string Fingerprint);

/// <summary>Datos del sistema informático de facturación (SistemaInformatico). Van en todos los registros.</summary>
public sealed record SystemInfo
{
    /// <summary>Nombre o razón social del productor del software.</summary>
    public required string ProducerName { get; init; }
    public required string ProducerNif { get; init; }
    /// <summary>Nombre del sistema (máx. 30 caracteres).</summary>
    public required string SystemName { get; init; }
    /// <summary>Identificador del sistema: 2 letras mayúsculas (sin Ñ) o dígitos.</summary>
    public required string SystemId { get; init; }
    public required string Version { get; init; }
    /// <summary>Número de instalación (máx. 100): distinto en cada instalación que lleve su propia cadena.</summary>
    public required string InstallationNumber { get; init; }
    /// <summary>TipoUsoPosibleMultiOT: el sistema puede usarse para facturar por varios obligados tributarios.</summary>
    public bool SupportsMultipleTaxpayers { get; init; }
    /// <summary>IndicadorMultiplesOT: esta instalación factura por más de un obligado tributario.</summary>
    public bool HasMultipleTaxpayers { get; init; }
}

/// <summary>
/// Una línea del desglose (DetalleDesglose), siempre de IVA en régimen general: sujeta al tipo
/// <see cref="TaxRate"/> o exenta por <see cref="Exemption"/>. Use <see cref="Taxed"/> o <see cref="Exempt"/>.
/// </summary>
public sealed record TaxLine
{
    public required decimal TaxBase { get; init; }
    /// <summary>Tipo de IVA en tanto por ciento (21, 10, 4...). Null si está exenta.</summary>
    public decimal? TaxRate { get; init; }
    /// <summary>Cuota de IVA. Null si está exenta.</summary>
    public decimal? TaxAmount { get; init; }
    public TaxExemption? Exemption { get; init; }

    public static TaxLine Taxed(decimal taxBase, decimal taxRate, decimal taxAmount) =>
        new() { TaxBase = taxBase, TaxRate = taxRate, TaxAmount = taxAmount };

    public static TaxLine Exempt(decimal taxBase, TaxExemption exemption) =>
        new() { TaxBase = taxBase, Exemption = exemption };
}

/// <summary>Factura para la que se genera un registro de alta.</summary>
public sealed record InvoiceData
{
    public required InvoiceIssuer Issuer { get; init; }
    /// <summary>Serie y número (NumSerieFactura), p. ej. "T2027-000123".</summary>
    public required string Number { get; init; }
    public required DateOnly IssueDate { get; init; }
    public required InvoiceType Type { get; init; }
    /// <summary>Descripción de la operación (máx. 500), p. ej. "Venta de productos".</summary>
    public required string Description { get; init; }
    /// <summary>Desglose por tipo de IVA: de 1 a 12 líneas.</summary>
    public required IReadOnlyList<TaxLine> TaxLines { get; init; }

    /// <summary>Solo en rectificativas.</summary>
    public CorrectionType? CorrectionType { get; init; }
    /// <summary>Facturas que rectifica (opcional, solo en rectificativas).</summary>
    public IReadOnlyList<InvoiceId> RectifiedInvoices { get; init; } = [];
    /// <summary>Base rectificada: obligatoria en las rectificativas por sustitución.</summary>
    public decimal? RectifiedBase { get; init; }
    /// <summary>Cuota rectificada: obligatoria en las rectificativas por sustitución.</summary>
    public decimal? RectifiedTax { get; init; }

    /// <summary>Subsanacion: corrige un registro de esta misma factura generado antes.</summary>
    public bool IsCorrection { get; init; }
    public PreviousRejection PreviousRejection { get; init; }
    /// <summary>Referencia propia (RefExterna, máx. 60), p. ej. el id de la operación en el sistema que factura. La AEAT la devuelve en la respuesta.</summary>
    public string? ExternalRef { get; init; }

    public InvoiceId Id => new(Issuer.Nif, Number, IssueDate);
    /// <summary>CuotaTotal: suma de las cuotas.</summary>
    public decimal TotalTax => TaxLines.Sum(l => l.TaxAmount ?? 0);
    /// <summary>ImporteTotal: suma de bases y cuotas.</summary>
    public decimal TotalAmount => TaxLines.Sum(l => l.TaxBase + (l.TaxAmount ?? 0));
}

/// <summary>Factura expedida por error (no hubo operación) para la que se genera un registro de anulación.</summary>
public sealed record CancellationData
{
    public required InvoiceId Invoice { get; init; }
    /// <summary>SinRegistroPrevio: el alta de esa factura no llegó a la AEAT.</summary>
    public bool WithoutPriorRecord { get; init; }
    /// <summary>RechazoPrevio: la AEAT rechazó una anulación anterior de esta factura.</summary>
    public bool PreviouslyRejected { get; init; }
    public string? ExternalRef { get; init; }
}

/// <summary>
/// Registro generado. Se guarda tal cual y se envía siempre el mismo <see cref="Xml"/>: si se regenerase,
/// cambiarían la fecha de generación y la huella.
/// </summary>
/// <param name="Xml">El elemento RegistroAlta o RegistroAnulacion.</param>
/// <param name="GeneratedAt">FechaHoraHusoGenRegistro, sin fracciones de segundo.</param>
/// <param name="QrUrl">URL de cotejo para el QR de la factura. Solo en las altas.</param>
public sealed record GeneratedRecord(string Xml, string Fingerprint, DateTimeOffset GeneratedAt, string? QrUrl);
