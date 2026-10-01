namespace QuickInventory.Core;

/// <summary>
/// Error esperado por una regla de negocio (p. ej. "no hay stock suficiente").
/// Su message está pensado para mostrarse tal cual al usuario.
/// </summary>
public class BusinessRuleException(string message) : Exception(message);
