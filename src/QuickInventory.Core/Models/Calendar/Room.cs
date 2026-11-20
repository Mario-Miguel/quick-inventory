namespace QuickInventory.Core.Models;

/// <summary>
/// Sala del museo que se puede reservar para un evento (<see cref="CalendarEvent"/>). No se borran:
/// se desactivan con <see cref="Active"/> para que los eventos pasados la sigan mostrando.
/// </summary>
public class Room
{
    public int Id { get; set; }
    /// <summary>Nombre de la sala. No se puede repetir.</summary>
    public string Name { get; set; } = "";
    /// <summary>Personas que caben (opcional, solo informativo).</summary>
    public int? Capacity { get; set; }
    public bool Active { get; set; } = true;

    public const int MaxNameLength = 100;

    public void Normalize()
    {
        Name = string.Join(' ', (Name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public void Validate()
    {
        if (Name.Length == 0)
            throw new BusinessRuleException("Escribe el nombre de la sala.");
        if (Name.Length > MaxNameLength)
            throw new BusinessRuleException($"El nombre de la sala no puede tener más de {MaxNameLength} caracteres.");
        if (Capacity is <= 0)
            throw new BusinessRuleException("El aforo de la sala tiene que ser mayor que cero.");
    }

    /// <summary>Copia los datos editables (no el Id ni <see cref="Active"/>).</summary>
    public void CopyFrom(Room other)
    {
        Name = other.Name;
        Capacity = other.Capacity;
    }

    public Room Clone() => (Room)MemberwiseClone();
}
