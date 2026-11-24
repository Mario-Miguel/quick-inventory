namespace Invoicing;

/// <summary>
/// Error esperado por una regla de negocio (p. ej. "falta la dirección del cliente").
/// Su mensaje está pensado para mostrarse tal cual al usuario.
/// </summary>
public class BusinessRuleException(string message) : Exception(message);
