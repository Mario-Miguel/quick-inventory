using QuickInventory.Core.Models;

namespace QuickInventory.Core.Services;

/// <summary>
/// Entradas vendidas en la tienda online (PrestaShop). No cambia nada en la tienda: solo la lee, y guarda
/// en la base de datos local qué entradas ya han venido al museo.
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
    /// en las que no se ha encontrado el día de la visita. Cada una trae <see cref="OnlineTicket.CheckedInAt"/>.
    /// </summary>
    Task<List<OnlineTicket>> GetTicketsAsync(DateOnly from, DateOnly to);

    /// <summary>
    /// Marca que las entradas de <paramref name="tickets"/> han venido al museo (o lo desmarca si
    /// <paramref name="checkedIn"/> es false) y actualiza su <see cref="OnlineTicket.CheckedInAt"/>.
    /// </summary>
    Task SetCheckedInAsync(IReadOnlyCollection<OnlineTicket> tickets, bool checkedIn);
}
