using System.Globalization;
using QuickInventory.Core.Models;

namespace QuickInventory.UI;

/// <summary>
/// Colores que se pueden elegir para un evento del calendario, para distinguir de un vistazo qué tipo de
/// evento es. Todos son lo bastante oscuros para leer texto blanco encima.
/// </summary>
public static class EventColors
{
    public sealed record EventColor(string Name, string Hex);

    public static readonly EventColor[] All =
    [
        new("Azul", CalendarEvent.DefaultColor),
        new("Verde", "#2E7D32"),
        new("Morado", "#7B3FA0"),
        new("Rojo", "#C62828"),
        new("Naranja", "#D9620F"),
        new("Rosa", "#C2185B"),
        new("Turquesa", "#00838F"),
        new("Marrón", "#6D4C41"),
        new("Gris", "#5F6368"),
    ];

    /// <summary>Nombre del color, o el código si no es uno de la lista (p. ej. uno que se quitó después).</summary>
    public static string NameOf(string hex) =>
        All.FirstOrDefault(c => string.Equals(c.Hex, hex, StringComparison.OrdinalIgnoreCase))?.Name ?? hex;

    /// <summary>Blanco o casi negro, el que mejor se lea sobre <paramref name="hex"/>.</summary>
    public static string TextOn(string hex)
    {
        if (!CalendarEvent.IsHexColor(hex)) return "#ffffff";
        var r = int.Parse(hex[1..3], NumberStyles.HexNumber);
        var g = int.Parse(hex[3..5], NumberStyles.HexNumber);
        var b = int.Parse(hex[5..7], NumberStyles.HexNumber);
        // Luminancia percibida (YIQ).
        return (r * 299 + g * 587 + b * 114) / 1000 >= 150 ? "#1f2937" : "#ffffff";
    }
}
