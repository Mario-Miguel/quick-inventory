using QuickInventory.Core.Models;

namespace QuickInventory.Core.Services;

/// <summary>Eventos y reservas de hora del calendario, y las salas que se pueden reservar.</summary>
public interface ICalendarService
{
    /// <summary>Salas ordenadas por nombre. Por defecto solo las activas.</summary>
    Task<List<Room>> GetRoomsAsync(bool includeInactive = false);

    /// <summary>Crea la sala (si <see cref="Room.Id"/> es 0) o guarda sus cambios. No cambia <see cref="Room.Active"/>.</summary>
    Task<Room> SaveRoomAsync(Room room);

    Task SetRoomActiveAsync(int roomId, bool active);

    /// <summary>Eventos que empiezan entre <paramref name="from"/> y <paramref name="to"/> (ambos incluidos), con su sala.</summary>
    Task<List<CalendarEvent>> GetEventsAsync(DateOnly from, DateOnly to);

    /// <summary>
    /// Crea el evento (si <see cref="CalendarEvent.Id"/> es 0) o guarda sus cambios. Si tiene sala,
    /// comprueba que no esté ya reservada a esa hora. Devuelve el evento guardado, con su sala.
    /// </summary>
    Task<CalendarEvent> SaveEventAsync(CalendarEvent calendarEvent);

    Task DeleteEventAsync(int eventId);
}
