namespace QuickInventory.Verifactu;

/// <summary>
/// Reglas de la AEAT («Validaciones y errores», v1.2.2) que se comprueban antes de generar un registro, para que
/// no lo rechace ni lo acepte con errores. Solo cubren lo que genera la librería: facturas simplificadas (F2) y sus
/// rectificativas (R5), con IVA en régimen general, sin destinatarios.
/// </summary>
internal static class RecordValidator
{
    /// <summary>Tipos de IVA que admite la AEAT con CalificacionOperacion S1.</summary>
    private static readonly decimal[] AllowedRates = [0m, 2m, 4m, 5m, 7.5m, 10m, 21m];
    /// <summary>Antes de esta fecha la AEAT no admite facturas.</summary>
    private static readonly DateOnly FirstIssueDate = new(2024, 10, 28);
    /// <summary>Límite de una factura simplificada (base + cuota).</summary>
    private const decimal SimplifiedLimit = 3000m;
    /// <summary>Diferencia admitida entre la cuota y base x tipo, por los redondeos. La AEAT admite hasta 10 €.</summary>
    private const decimal TaxTolerance = 0.01m;

    public static void Validate(SystemInfo system)
    {
        Check(Length(system.ProducerName) is >= 1 and <= 120, "Falta el nombre del productor del software, o es demasiado largo (máx. 120).");
        Check(IsNif(system.ProducerNif), "El NIF del productor del software tiene que tener 9 caracteres.");
        Check(Length(system.SystemName) is >= 1 and <= 30, "Falta el nombre del sistema informático, o es demasiado largo (máx. 30).");
        Check(system.SystemId.Length == 2 && system.SystemId.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9'),
            "El identificador del sistema informático tiene que ser de 2 letras mayúsculas o dígitos.");
        Check(Length(system.Version) is >= 1 and <= 50, "Falta la versión del sistema informático, o es demasiado larga (máx. 50).");
        Check(Length(system.InstallationNumber) is >= 1 and <= 100, "Falta el número de instalación, o es demasiado largo (máx. 100).");
        Check(!system.HasMultipleTaxpayers || system.SupportsMultipleTaxpayers,
            "Una instalación solo puede facturar por varios obligados tributarios si el sistema lo permite.");
    }

    public static void Validate(InvoiceData invoice, DateTimeOffset generatedAt)
    {
        Check(invoice.Type is InvoiceType.F2 or InvoiceType.R5,
            $"Las facturas de tipo {invoice.Type} todavía no se pueden emitir: solo simplificadas (F2) y sus rectificativas (R5).");
        Check(IsNif(invoice.Issuer.Nif), "El NIF del emisor tiene que tener 9 caracteres.");
        Check(Length(invoice.Issuer.LegalName) is >= 1 and <= 120, "Falta el nombre o razón social del emisor, o es demasiado largo (máx. 120).");
        ValidateId(invoice.Id, generatedAt);
        Check(Length(invoice.Description) is >= 1 and <= 500, "Falta la descripción de la operación, o es demasiado larga (máx. 500).");
        Check(Length(invoice.ExternalRef) <= 60, "La referencia externa es demasiado larga (máx. 60).");
        Check(invoice.PreviousRejection == PreviousRejection.None || invoice.IsCorrection,
            "Un registro con rechazo previo tiene que ser una subsanación.");

        Check(invoice.TaxLines.Count is >= 1 and <= 12, "La factura tiene que tener de 1 a 12 líneas de desglose de IVA.");
        var isDifferences = invoice.CorrectionType == CorrectionType.Differences;
        foreach (var line in invoice.TaxLines)
            ValidateLine(line, invoice.IssueDate, checkSign: !isDifferences);
        Check(VerifactuFormat.IsValidAmount(invoice.TotalAmount), "El importe total es demasiado grande.");

        if (invoice.Type == InvoiceType.F2)
        {
            Check(invoice.TotalAmount <= SimplifiedLimit,
                $"Una factura simplificada no puede pasar de {SimplifiedLimit:0} €: hay que hacer una factura completa.");
            Check(invoice.CorrectionType is null && invoice.RectifiedInvoices.Count == 0 && invoice.RectifiedBase is null && invoice.RectifiedTax is null,
                "Una factura simplificada no puede llevar datos de rectificación.");
        }
        else
        {
            Check(invoice.CorrectionType is not null, "Falta indicar si la rectificativa es por sustitución o por diferencias.");
            var hasAmounts = invoice.RectifiedBase is not null && invoice.RectifiedTax is not null;
            if (invoice.CorrectionType == CorrectionType.Substitution)
                Check(hasAmounts && VerifactuFormat.IsValidAmount(invoice.RectifiedBase!.Value) && VerifactuFormat.IsValidAmount(invoice.RectifiedTax!.Value),
                    "Una rectificativa por sustitución tiene que llevar la base y la cuota rectificadas.");
            else
                Check(invoice.RectifiedBase is null && invoice.RectifiedTax is null,
                    "La base y la cuota rectificadas solo van en las rectificativas por sustitución.");
            foreach (var rectified in invoice.RectifiedInvoices)
                Check(IsNif(rectified.IssuerNif) && VerifactuFormat.IsValidInvoiceNumber(rectified.Number),
                    $"La factura rectificada «{rectified.Number}» no tiene un NIF o un número válidos.");
            Check(invoice.RectifiedInvoices.Count <= 1000, "Una rectificativa puede rectificar como mucho 1.000 facturas.");
        }
    }

    public static void Validate(CancellationData cancellation, DateTimeOffset generatedAt)
    {
        ValidateId(cancellation.Invoice, generatedAt);
        Check(Length(cancellation.ExternalRef) <= 60, "La referencia externa es demasiado larga (máx. 60).");
    }

    public static void Validate(PreviousRecord? previous)
    {
        if (previous is null) return;
        Check(IsNif(previous.Invoice.IssuerNif) && VerifactuFormat.IsValidInvoiceNumber(previous.Invoice.Number),
            "El registro anterior de la cadena no tiene un NIF o un número de factura válidos.");
        Check(FingerprintCalculator.IsValid(previous.Fingerprint),
            "La huella del registro anterior tiene que tener 64 caracteres hexadecimales en mayúsculas.");
    }

    private static void ValidateId(InvoiceId id, DateTimeOffset generatedAt)
    {
        Check(IsNif(id.IssuerNif), "El NIF del emisor de la factura tiene que tener 9 caracteres.");
        Check(VerifactuFormat.IsValidInvoiceNumber(id.Number),
            $"El número de factura «{id.Number}» no es válido: de 1 a 60 caracteres, sin acentos, ñ, comillas, < > ni =.");
        Check(id.IssueDate >= FirstIssueDate, "La fecha de la factura es anterior al 28-10-2024, que la AEAT no admite.");
        Check(id.IssueDate <= DateOnly.FromDateTime(generatedAt.DateTime), "La fecha de la factura no puede ser posterior a hoy.");
    }

    private static void ValidateLine(TaxLine line, DateOnly issueDate, bool checkSign)
    {
        Check(VerifactuFormat.IsValidAmount(line.TaxBase), "La base imponible tiene que tener como mucho 2 decimales.");

        if (line.Exemption is not null)
        {
            Check(line.TaxRate is null && line.TaxAmount is null, "Una línea exenta de IVA no puede llevar tipo ni cuota.");
            return;
        }

        Check(line.TaxRate is not null && line.TaxAmount is not null, "Cada línea tiene que llevar el tipo y la cuota de IVA, o la causa de exención.");
        var rate = line.TaxRate!.Value;
        var tax = line.TaxAmount!.Value;
        Check(IsAllowedRate(rate, issueDate), $"La AEAT no admite un IVA del {VerifactuFormat.Rate(rate)} % en esa fecha.");
        Check(VerifactuFormat.IsValidAmount(tax), "La cuota de IVA tiene que tener como mucho 2 decimales.");
        Check(Math.Abs(tax - line.TaxBase * rate / 100m) <= TaxTolerance,
            $"La cuota de IVA ({VerifactuFormat.Amount(tax)}) no corresponde a la base ({VerifactuFormat.Amount(line.TaxBase)}) al {VerifactuFormat.Rate(rate)} %.");
        if (checkSign)
            Check(tax == 0 || line.TaxBase == 0 || Math.Sign(tax) == Math.Sign(line.TaxBase), "La cuota y la base de IVA tienen que tener el mismo signo.");
    }

    /// <summary>El 5 % solo se admitió del 1-7-2022 al 30-9-2024 y el 7,5 %, del 1-10-2024 al 31-12-2024.</summary>
    private static bool IsAllowedRate(decimal rate, DateOnly date) => rate switch
    {
        5m => date >= new DateOnly(2022, 7, 1) && date <= new DateOnly(2024, 9, 30),
        7.5m => date >= new DateOnly(2024, 10, 1) && date <= new DateOnly(2024, 12, 31),
        _ => AllowedRates.Contains(rate),
    };

    private static bool IsNif(string? value) => value?.Trim().Length == 9;

    private static int Length(string? value) => VerifactuFormat.Text(value).Length;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new VerifactuValidationException(message);
    }
}
