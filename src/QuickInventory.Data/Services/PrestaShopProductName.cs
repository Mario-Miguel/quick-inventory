using System.Text.RegularExpressions;

namespace QuickInventory.Data.Services;

/// <summary>
/// Saca el tipo de entrada, el día y la hora de la visita del nombre de una línea de pedido de PrestaShop.
/// </summary>
/// <remarks>
/// Cuando un producto tiene combinaciones, PrestaShop guarda en la línea del pedido el nombre del producto
/// seguido de las combinaciones elegidas: "Entrada general - Fecha: 25/10/2026, Hora: 11:00".
/// Se reconocen fechas como 25/10/2026, 25-10-26, 2026-10-25 o "25 de octubre de 2026", y horas como
/// 11:00, 11.30, 11h o 11:00h. Las combinaciones que no son fecha ni hora (por ejemplo "Tipo: Reducida")
/// forman parte del tipo de entrada.
/// </remarks>
internal static partial class PrestaShopProductName
{
    public static (string Type, DateOnly? Day, TimeOnly? Time) Parse(string productName, DateTime orderDate)
    {
        var separator = productName.IndexOf(" - ", StringComparison.Ordinal);
        var attributes = separator < 0 ? [] : Attribute().Matches(productName[(separator + 3)..]).ToList();

        // Sin combinaciones reconocibles: la fecha y la hora pueden estar en el propio nombre.
        if (attributes.Count == 0)
        {
            var (remainder, day, time) = Extract(productName, orderDate);
            return (Clean(remainder) is { Length: > 0 } cleaned ? cleaned : productName.Trim(), day, time);
        }

        DateOnly? visitDay = null;
        TimeOnly? visitTime = null;
        var type = new List<string> { productName[..separator].Trim() };
        foreach (var attribute in attributes)
        {
            var value = attribute.Groups["value"].Value;
            var (_, day, time) = Extract(value, orderDate);
            visitDay ??= day;
            visitTime ??= time;
            if (day is null && time is null) type.Add(value.Trim());
        }
        return (string.Join(" · ", type), visitDay, visitTime);
    }

    /// <summary>Busca una fecha y una hora en el texto y devuelve también lo que queda sin ellas.</summary>
    private static (string Remainder, DateOnly? Day, TimeOnly? Time) Extract(string text, DateTime orderDate)
    {
        DateOnly? day = null;
        foreach (var regex in (Regex[])[IsoDate(), NumericDate(), TextDate()])
        {
            var match = regex.Match(text);
            if (match.Success && ToDate(match, orderDate) is { } date)
            {
                day = date;
                // Se quita la fecha antes de buscar la hora, para que "25.10.2026" no se lea como las 25:10.
                text = text.Remove(match.Index, match.Length);
                break;
            }
        }

        TimeOnly? time = null;
        var hour = Time().Match(text);
        if (hour.Success)
        {
            var minutes = hour.Groups["min"].Success ? int.Parse(hour.Groups["min"].Value) : 0;
            time = new TimeOnly(int.Parse(hour.Groups["h"].Value), minutes);
            text = text.Remove(hour.Index, hour.Length);
        }

        return (text, day, time);
    }

    private static DateOnly? ToDate(Match match, DateTime orderDate)
    {
        var dayNumber = int.Parse(match.Groups["d"].Value);
        var month = match.Groups["m"].Success
            ? int.Parse(match.Groups["m"].Value)
            : Array.IndexOf(Months, match.Groups["month"].Value.ToLowerInvariant().Replace("setiembre", "septiembre")) + 1;

        int year;
        if (match.Groups["y"].Success)
        {
            year = int.Parse(match.Groups["y"].Value);
            if (year < 100) year += 2000;
        }
        else
        {
            // "25 de octubre" sin año: la próxima vez que llegue ese día desde que se hizo el pedido.
            year = orderDate.Year;
            if (month < orderDate.Month || (month == orderDate.Month && dayNumber < orderDate.Day)) year++;
        }

        if (month is < 1 or > 12 || dayNumber < 1 || dayNumber > DateTime.DaysInMonth(year, month)) return null;
        return new DateOnly(year, month, dayNumber);
    }

    /// <summary>Quita los separadores que quedan sueltos al sacar la fecha y la hora del nombre.</summary>
    private static string Clean(string text) => Spaces().Replace(text, " ").Trim(' ', '-', ',', '·', '|', '(', ')');

    private static readonly string[] Months =
        ["enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"];

    /// <summary>
    /// "Grupo: valor" de cada combinación. El valor llega hasta el siguiente "Grupo:" o el final, así que
    /// puede llevar comas ("Sábado, 25/10/2026"). Los dos puntos de "11:00" no cuentan: van sin espacio detrás.
    /// </summary>
    [GeneratedRegex(@"(?<group>[^,:]+?)\s*:\s+(?<value>.+?)(?=,\s*[^,:]+?\s*:\s|$)")]
    private static partial Regex Attribute();

    [GeneratedRegex(@"\b(?<y>\d{4})-(?<m>\d{1,2})-(?<d>\d{1,2})\b")]
    private static partial Regex IsoDate();

    [GeneratedRegex(@"\b(?<d>\d{1,2})[/.\-](?<m>\d{1,2})[/.\-](?<y>\d{4}|\d{2})\b")]
    private static partial Regex NumericDate();

    [GeneratedRegex(@"\b(?<d>\d{1,2})\s+(?:de\s+)?(?<month>enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|setiembre|octubre|noviembre|diciembre)(?:\s+(?:de\s+|del\s+)?(?<y>\d{4}))?\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex TextDate();

    [GeneratedRegex(@"\b(?<h>[01]?\d|2[0-3])\s*(?:[:.]\s*(?<min>[0-5]\d)\s*h?|h(?:oras?)?)(?![\w])", RegexOptions.IgnoreCase)]
    private static partial Regex Time();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
