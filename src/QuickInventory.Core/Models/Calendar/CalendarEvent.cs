namespace QuickInventory.Core.Models;

/// <summary>
/// Evento o reserva de hora en el calendario: un evento en una sala, una visita con entrada especial
/// que necesita reserva... Solo guarda la hora; no tiene relación con las ventas ni con las entradas.
/// </summary>
public class CalendarEvent
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    /// <summary>Hora local del museo en la que empieza.</summary>
    public DateTime Start { get; set; }
    /// <summary>Hora local en la que termina; siempre después de <see cref="Start"/> y el mismo día.</summary>
    public DateTime End { get; set; }
    /// <summary>Sala reservada, o null si no ocupa ninguna (p. ej. una visita reservada).</summary>
    public int? RoomId { get; set; }
    public Room? Room { get; set; }
    /// <summary>Color con el que se ve en el calendario, en formato #RRGGBB. Sirve para distinguir el tipo de evento.</summary>
    public string Color { get; set; } = DefaultColor;
    /// <summary>Número de personas (opcional).</summary>
    public int? People { get; set; }
    /// <summary>Quién ha reservado: nombre, teléfono... (opcional).</summary>
    public string? Contact { get; set; }
    public string? Notes { get; set; }

    public const string DefaultColor = "#2F6FB0";
    public const int MaxTitleLength = 200;
    public const int MaxContactLength = 200;
    public const int MaxNotesLength = 1000;

    public DateOnly Day => DateOnly.FromDateTime(Start);

    /// <summary>Si coincide en algún momento con el intervalo [<paramref name="start"/>, <paramref name="end"/>).</summary>
    public bool Overlaps(DateTime start, DateTime end) => Start < end && start < End;

    public void Normalize()
    {
        Title = Title?.Trim() ?? "";
        Contact = Clean(Contact);
        Notes = Clean(Notes);
        Color = (Color ?? "").Trim().ToUpperInvariant();
        // Sin segundos: el calendario trabaja con horas y minutos.
        Start = TruncateToMinute(Start);
        End = TruncateToMinute(End);
        if (RoomId is 0) RoomId = null;
    }

    public void Validate()
    {
        if (Title.Length == 0)
            throw new BusinessRuleException("Escribe qué es el evento.");
        if (Title.Length > MaxTitleLength)
            throw new BusinessRuleException($"El nombre del evento no puede tener más de {MaxTitleLength} caracteres.");
        if (End <= Start)
            throw new BusinessRuleException("La hora de fin tiene que ser después de la hora de inicio.");
        if (DateOnly.FromDateTime(End) != Day && End != Day.AddDays(1).ToDateTime(TimeOnly.MinValue))
            throw new BusinessRuleException("El evento tiene que empezar y terminar el mismo día.");
        if (!IsHexColor(Color))
            throw new BusinessRuleException("Elige un color para el evento.");
        if (People is <= 0)
            throw new BusinessRuleException("El número de personas tiene que ser mayor que cero.");
        if (Contact?.Length > MaxContactLength)
            throw new BusinessRuleException($"Los datos de quién reserva no pueden tener más de {MaxContactLength} caracteres.");
        if (Notes?.Length > MaxNotesLength)
            throw new BusinessRuleException($"Las notas no pueden tener más de {MaxNotesLength} caracteres.");
    }

    /// <summary>Copia los datos editables (no el Id ni la sala cargada).</summary>
    public void CopyFrom(CalendarEvent other)
    {
        Title = other.Title;
        Start = other.Start;
        End = other.End;
        RoomId = other.RoomId;
        Color = other.Color;
        People = other.People;
        Contact = other.Contact;
        Notes = other.Notes;
    }

    public CalendarEvent Clone() => (CalendarEvent)MemberwiseClone();

    public static bool IsHexColor(string? color) =>
        color is { Length: 7 } && color[0] == '#' && color.Skip(1).All(char.IsAsciiHexDigit);

    private static DateTime TruncateToMinute(DateTime date) =>
        new(date.Year, date.Month, date.Day, date.Hour, date.Minute, 0, date.Kind);

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
