using QuickInventory.Core;

namespace QuickInventory.Data;

/// <summary>
/// El servidor rechaza el usuario o la contraseña. A diferencia de los demás errores de conexión,
/// se arregla escribiendo otra contraseña (ver la pantalla de conexión de la app de escritorio).
/// </summary>
public sealed class DatabaseLoginException(string message) : BusinessRuleException(message);
