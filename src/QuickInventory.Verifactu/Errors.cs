namespace QuickInventory.Verifactu;

/// <summary>Error de la librería VERI*FACTU. El mensaje se puede enseñar tal cual.</summary>
public class VerifactuException(string message, Exception? innerException = null) : Exception(message, innerException);

/// <summary>Los datos no cumplen las reglas de la AEAT, así que no se genera el registro.</summary>
public sealed class VerifactuValidationException(string message) : VerifactuException(message);

/// <summary>
/// La AEAT rechazó el envío entero con un SOAP Fault. Si es culpa del cliente (<see cref="IsClientError"/>),
/// el XML o la cabecera están mal y no tiene sentido reintentar sin corregirlos; si es del servidor, sí.
/// </summary>
public sealed class AeatFaultException(bool isClientError, int? errorCode, string faultString, string responseXml)
    : VerifactuException($"La AEAT ha rechazado el envío: {faultString}")
{
    public bool IsClientError { get; } = isClientError;
    /// <summary>Código de errores.properties, sacado de "Codigo[4104]..." si viene.</summary>
    public int? ErrorCode { get; } = errorCode;
    public string FaultString { get; } = faultString;
    public string ResponseXml { get; } = responseXml;
    public bool CanRetry => !IsClientError;
}

/// <summary>No se ha podido hablar con la AEAT: sin conexión, tiempo agotado o error HTTP sin respuesta SOAP. Se puede reintentar.</summary>
public sealed class AeatConnectionException(string message, Exception? innerException = null)
    : VerifactuException(message, innerException);
