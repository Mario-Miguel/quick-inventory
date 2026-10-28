namespace QuickInventory.UI;

/// <summary>
/// Conexión de este equipo con la base de datos del cliente, antes de poder usar la aplicación
/// (ver Components/ConnectionSetup.razor). Solo se pide la contraseña: el resto viene de client.json.
/// La implementa el anfitrión, que es quien sabe dónde se guarda la configuración del equipo.
/// </summary>
public interface IConnectionSetup
{
    /// <summary>Null la primera vez; si la contraseña guardada ya no sirve, el motivo para el usuario.</summary>
    string? Reason { get; }

    /// <summary>
    /// Prueba la conexión con <paramref name="password"/> y, si funciona, la guarda en este equipo
    /// y vuelve a abrir la aplicación.
    /// </summary>
    /// <exception cref="QuickInventory.Core.BusinessRuleException">Con un mensaje para el usuario si no se puede conectar.</exception>
    Task SaveAndRestartAsync(string password);
}
