using QuickInventory.Core.Models;

namespace QuickInventory.Core.Services;

/// <summary>
/// Entradas vendidas en la tienda online (PrestaShop). Solo lee: no cambia nada en la tienda.
/// </summary>
public interface IOnlineTicketService
{
    /// <summary>Configuración guardada, o una vacía si todavía no se ha configurado.</summary>
    Task<OnlineShopSettings> GetSettingsAsync();
    Task SaveSettingsAsync(OnlineShopSettings settings);

    /// <summary>
    /// Prueba la conexión con <paramref name="settings"/> (sin guardarla) y devuelve las líneas de los
    /// últimos pedidos, pagados o no, para comprobar que la fecha y la hora de la visita se reconocen bien.
    /// </summary>
    Task<List<OnlineTicket>> TestConnectionAsync(OnlineShopSettings settings, int orders = 10);

    /// <summary>
    /// Entradas pagadas con visita entre <paramref name="from"/> y <paramref name="to"/> (ambos incluidos).
    /// También devuelve, con <see cref="OnlineTicket.VisitDay"/> a null, las compradas en esas fechas
    /// en las que no se ha encontrado el día de la visita.
    /// </summary>
    Task<List<OnlineTicket>> GetTicketsAsync(DateOnly from, DateOnly to);
}
