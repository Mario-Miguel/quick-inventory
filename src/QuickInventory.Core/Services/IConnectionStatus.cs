namespace QuickInventory.Core.Services;

/// <summary>
/// Indica si hay conexión con la base de datos. Sin conexión no se puede trabajar:
/// la aplicación se bloquea hasta que vuelva.
/// </summary>
public interface IConnectionStatus
{
    bool Connected { get; }

    /// <summary>Avisa cuando se pierde o se recupera la conexión.</summary>
    event Action? Changed;

    /// <summary>Comprueba la conexión ahora mismo, sin esperar a la siguiente comprobación automática.</summary>
    Task CheckAsync();
}
