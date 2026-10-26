namespace QuickInventory.Verifactu;

/// <summary>EstadoEnvio: el resultado del envío en conjunto.</summary>
public enum SubmissionStatus { Correct, PartiallyCorrect, Incorrect }

/// <summary>EstadoRegistro de cada registro enviado.</summary>
public enum RecordStatus { Correct, AcceptedWithErrors, Incorrect }

/// <summary>Estado de un registro guardado en la AEAT, según la consulta.</summary>
public enum StoredRecordStatus { Correct, AcceptedWithErrors, Cancelled }

/// <summary>Respuesta a un envío (RespuestaRegFactuSistemaFacturacion).</summary>
/// <param name="Csv">Código seguro de verificación del envío. Hay que guardarlo: no se puede recuperar después.</param>
/// <param name="WaitSeconds">TiempoEsperaEnvio: segundos que hay que esperar antes del siguiente envío.</param>
/// <param name="ResponseXml">Respuesta completa, para guardarla.</param>
public sealed record SubmissionResult(
    string? Csv,
    DateTimeOffset? SubmittedAt,
    int WaitSeconds,
    SubmissionStatus Status,
    IReadOnlyList<SubmissionLine> Lines,
    string ResponseXml);

/// <summary>Resultado de un registro del envío (RespuestaLinea).</summary>
public sealed record SubmissionLine(
    InvoiceId Invoice,
    bool IsCancellation,
    string? ExternalRef,
    RecordStatus Status,
    int? ErrorCode,
    string? ErrorDescription,
    DuplicateRecord? Duplicate)
{
    /// <summary>
    /// La AEAT ya tenía este registro (p. ej. se cortó la conexión después de enviarlo). Aunque venga como
    /// incorrecto, hay que tratarlo como enviado, con el estado de <see cref="Duplicate"/>.
    /// </summary>
    public bool IsAlreadyRegistered => Duplicate is not null;
}

/// <summary>Registro que la AEAT ya tenía (RegistroDuplicado).</summary>
public sealed record DuplicateRecord(string RequestId, StoredRecordStatus Status, int? ErrorCode, string? ErrorDescription);

/// <summary>Una página de la consulta (RespuestaConsultaFactuSistemaFacturacion).</summary>
/// <param name="NextPageKey">Si <see cref="HasMore"/>, la clave para pedir la página siguiente.</param>
public sealed record QueryResult(bool HasMore, IReadOnlyList<StoredRecord> Records, InvoiceId? NextPageKey);

/// <summary>Registro de facturación guardado en la AEAT.</summary>
public sealed record StoredRecord(
    InvoiceId Invoice,
    InvoiceType? Type,
    decimal? TotalAmount,
    string? Fingerprint,
    DateTimeOffset? GeneratedAt,
    string? RequestId,
    StoredRecordStatus Status,
    int? ErrorCode,
    string? ErrorDescription);
