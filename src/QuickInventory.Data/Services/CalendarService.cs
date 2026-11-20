using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuickInventory.Core;
using QuickInventory.Core.Models;
using QuickInventory.Core.Services;

namespace QuickInventory.Data.Services;

public sealed class CalendarService(IDbContextFactory<BaseDbContext> factory) : ICalendarService
{
    public async Task<List<Room>> GetRoomsAsync(bool includeInactive = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Rooms
            .AsNoTracking()
            .Where(r => includeInactive || r.Active)
            .OrderBy(r => r.Name)
            .ToListAsync();
    }

    public async Task<Room> SaveRoomAsync(Room room)
    {
        room.Normalize();
        room.Validate();

        await using var db = await factory.CreateDbContextAsync();

        // El índice único lo impediría igualmente, pero así el mensaje es claro. Sin distinguir mayúsculas.
        var other = await db.Rooms
            .Where(r => r.Name.ToLower() == room.Name.ToLower() && r.Id != room.Id)
            .Select(r => new { r.Name, r.Active })
            .FirstOrDefaultAsync();
        if (other is not null)
            throw new BusinessRuleException(other.Active
                ? $"Ya hay una sala que se llama {other.Name}."
                : $"Ya hay una sala que se llama {other.Name}, pero está desactivada. Actívala en vez de crearla de nuevo.");

        Room saved;
        if (room.Id == 0)
        {
            saved = new Room();
            saved.CopyFrom(room);
            db.Rooms.Add(saved);
        }
        else
        {
            saved = await db.Rooms.FindAsync(room.Id)
                ?? throw new BusinessRuleException("La sala no existe.");
            saved.CopyFrom(room);
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new BusinessRuleException("Ya hay una sala con ese nombre.");
        }
        return saved;
    }

    public async Task SetRoomActiveAsync(int roomId, bool active)
    {
        await using var db = await factory.CreateDbContextAsync();
        var room = await db.Rooms.FindAsync(roomId)
            ?? throw new BusinessRuleException("La sala no existe.");

        room.Active = active;
        await db.SaveChangesAsync();
    }

    public async Task<List<CalendarEvent>> GetEventsAsync(DateOnly from, DateOnly to)
    {
        var start = from.ToDateTime(TimeOnly.MinValue);
        var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);

        await using var db = await factory.CreateDbContextAsync();
        return await db.CalendarEvents
            .AsNoTracking()
            .Include(e => e.Room)
            .Where(e => e.Start >= start && e.Start < end)
            .OrderBy(e => e.Start).ThenBy(e => e.End)
            .ToListAsync();
    }

    public async Task<CalendarEvent> SaveEventAsync(CalendarEvent calendarEvent)
    {
        calendarEvent.Normalize();
        calendarEvent.Validate();

        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        CalendarEvent saved;
        if (calendarEvent.Id == 0)
        {
            saved = new CalendarEvent();
            db.CalendarEvents.Add(saved);
        }
        else
        {
            saved = await db.CalendarEvents.FindAsync(calendarEvent.Id)
                ?? throw new BusinessRuleException("El evento ya no existe: puede que lo haya borrado otra persona.");
        }

        if (calendarEvent.RoomId is { } roomId)
        {
            // Mientras se comprueba y se guarda, nadie más puede reservar: dos equipos no reservan la misma sala a la vez.
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({AdvisoryLocks.Calendar})");

            var room = await db.Rooms.FindAsync(roomId)
                ?? throw new BusinessRuleException("La sala no existe.");
            // Se puede dejar un evento antiguo en una sala que luego se desactivó, pero no llevarlo a ella.
            if (!room.Active && saved.RoomId != roomId)
                throw new BusinessRuleException($"«{room.Name}» está desactivada: elige otra sala.");

            var clash = await db.CalendarEvents
                .AsNoTracking()
                .Where(e => e.RoomId == roomId && e.Id != calendarEvent.Id
                            && e.Start < calendarEvent.End && calendarEvent.Start < e.End)
                .OrderBy(e => e.Start)
                .FirstOrDefaultAsync();
            if (clash is not null)
                throw new BusinessRuleException(
                    $"«{room.Name}» ya está reservada de {clash.Start:HH:mm} a {clash.End:HH:mm} para «{clash.Title}».");
        }

        saved.CopyFrom(calendarEvent);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        if (saved.RoomId is not null)
            await db.Entry(saved).Reference(e => e.Room).LoadAsync();
        return saved;
    }

    public async Task DeleteEventAsync(int eventId)
    {
        await using var db = await factory.CreateDbContextAsync();
        // Si ya no está (lo borró otro equipo), no pasa nada: el resultado es el mismo.
        await db.CalendarEvents.Where(e => e.Id == eventId).ExecuteDeleteAsync();
    }
}
