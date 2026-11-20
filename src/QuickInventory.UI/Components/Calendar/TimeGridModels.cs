namespace QuickInventory.UI.Components.Calendar;

/// <summary>
/// Columna de <see cref="TimeGrid"/>: un día (vista semanal) o una sala de un día (vista diaria).
/// </summary>
/// <param name="Header">Texto de la cabecera.</param>
/// <param name="Day">Día al que corresponden las horas de la columna.</param>
/// <param name="Items">Lo que se dibuja en la columna.</param>
/// <param name="SubHeader">Segunda línea de la cabecera (opcional).</param>
/// <param name="RoomId">Sala de la columna: al pulsar una hora libre se reserva en ella.</param>
/// <param name="AllowsNew">Si se puede pulsar una hora libre para crear un evento.</param>
public sealed record TimeGridColumn(string Header, DateOnly Day, IReadOnlyList<TimeGridItem> Items,
    string? SubHeader = null, int? RoomId = null, bool AllowsNew = true);

/// <summary>Bloque de <see cref="TimeGrid"/>: un evento o las entradas online de una hora.</summary>
/// <param name="Source">Objeto original (p. ej. el <c>CalendarEvent</c>), para saber qué se ha pulsado.</param>
public sealed record TimeGridItem(DateTime Start, DateTime End, string Title, string? Detail, string Color, object? Source);

/// <summary>Hora libre pulsada en <see cref="TimeGrid"/>.</summary>
public sealed record TimeGridSlot(TimeGridColumn Column, DateTime Start);
