namespace QuickInventory.Verifactu;

/// <summary>Huso horario oficial español en el que se generan los registros.</summary>
public enum SpanishTimeZone
{
    /// <summary>Península, Baleares, Ceuta y Melilla: +01:00 en invierno y +02:00 en verano.</summary>
    Peninsula,
    /// <summary>Canarias: +00:00 en invierno y +01:00 en verano.</summary>
    CanaryIslands,
}

/// <summary>Hora oficial española, con su huso (FechaHoraHusoGenRegistro lo lleva).</summary>
public static class SpainTime
{
    // Los nombres IANA funcionan en Linux y en Windows con ICU; los otros son los clásicos de Windows
    // ("GMT Standard Time" sigue las mismas reglas que Canarias).
    private static readonly TimeZoneInfo PeninsulaZone = Find("Europe/Madrid", "Romance Standard Time");
    private static readonly TimeZoneInfo CanaryZone = Find("Atlantic/Canary", "GMT Standard Time");

    /// <summary>Ahora mismo, sin fracciones de segundo (FechaHoraHusoGenRegistro no las lleva).</summary>
    public static DateTimeOffset Now(SpanishTimeZone zone = SpanishTimeZone.Peninsula) =>
        Truncate(From(DateTimeOffset.UtcNow, zone));

    /// <summary>Pasa un instante cualquiera a la hora oficial del huso indicado.</summary>
    public static DateTimeOffset From(DateTimeOffset value, SpanishTimeZone zone = SpanishTimeZone.Peninsula) =>
        TimeZoneInfo.ConvertTime(value, zone == SpanishTimeZone.CanaryIslands ? CanaryZone : PeninsulaZone);

    internal static DateTimeOffset Truncate(DateTimeOffset value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, value.Offset);

    private static TimeZoneInfo Find(string iana, string windows) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(iana, out var zone) ? zone : TimeZoneInfo.FindSystemTimeZoneById(windows);
}
