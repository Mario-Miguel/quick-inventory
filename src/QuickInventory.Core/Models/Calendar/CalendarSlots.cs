namespace QuickInventory.Core.Models;

/// <summary>Cálculos de horas libres y ocupadas en el calendario.</summary>
public static class CalendarSlots
{
    /// <summary>
    /// Intervalos libres entre <paramref name="from"/> y <paramref name="to"/> que no coinciden con
    /// ninguno de <paramref name="busy"/> (pueden estar desordenados y solaparse entre sí).
    /// Se descartan los huecos de menos de <paramref name="minimum"/>.
    /// </summary>
    public static List<(DateTime From, DateTime To)> Free(
        IEnumerable<(DateTime Start, DateTime End)> busy, DateTime from, DateTime to, TimeSpan minimum = default)
    {
        var free = new List<(DateTime, DateTime)>();
        var cursor = from;
        foreach (var (start, end) in busy.Where(b => b.End > from && b.Start < to).OrderBy(b => b.Start))
        {
            if (start > cursor && start - cursor >= minimum)
                free.Add((cursor, start));
            if (end > cursor) cursor = end;
        }
        if (to > cursor && to - cursor >= minimum)
            free.Add((cursor, to));
        return free;
    }
}
