using QuickInventory.Core.Models;

namespace QuickInventory.Core.Tests;

public class CalendarTests
{
    private static readonly DateTime Day = new(2026, 11, 20);

    private static DateTime At(int hour, int minute = 0) => Day.AddHours(hour).AddMinutes(minute);

    private static CalendarEvent Event(DateTime start, DateTime end) =>
        new() { Title = "Cata de sidra", Start = start, End = end, Color = "#2f6fb0" };

    [Fact]
    public void Normaliza_antes_de_validar()
    {
        var e = Event(At(10, 0).AddSeconds(42), At(11, 30));
        e.Title = "  Cata  ";
        e.Contact = "  ";
        e.RoomId = 0;

        e.Normalize();
        e.Validate();

        Assert.Equal("Cata", e.Title);
        Assert.Null(e.Contact);
        Assert.Null(e.RoomId);
        Assert.Equal("#2F6FB0", e.Color);
        Assert.Equal(At(10), e.Start);
    }

    [Fact]
    public void Rechaza_fin_antes_del_inicio_o_en_otro_dia()
    {
        var reverse = Event(At(12), At(11));
        var nextDay = Event(At(22), At(25));
        var noColor = Event(At(10), At(11));
        noColor.Color = "azul";

        Assert.Throws<BusinessRuleException>(reverse.Validate);
        Assert.Throws<BusinessRuleException>(nextDay.Validate);
        Assert.Throws<BusinessRuleException>(noColor.Validate);
    }

    [Fact]
    public void Puede_terminar_a_medianoche()
    {
        Event(At(22), At(24)).Validate();
    }

    [Fact]
    public void Dos_eventos_seguidos_no_se_solapan()
    {
        var e = Event(At(10), At(11));

        Assert.False(e.Overlaps(At(11), At(12)));
        Assert.False(e.Overlaps(At(9), At(10)));
        Assert.True(e.Overlaps(At(10, 30), At(12)));
        Assert.True(e.Overlaps(At(9), At(13)));
    }

    [Fact]
    public void Sala_sin_nombre_no_vale()
    {
        var room = new Room { Name = "   " };
        room.Normalize();

        Assert.Throws<BusinessRuleException>(room.Validate);
    }

    [Fact]
    public void Nombre_de_sala_sin_espacios_repetidos()
    {
        var room = new Room { Name = "  Sala   del  lagar " };
        room.Normalize();

        Assert.Equal("Sala del lagar", room.Name);
    }

    [Fact]
    public void Horas_libres_entre_eventos_desordenados_y_solapados()
    {
        var busy = new[] { (At(15), At(16)), (At(10), At(11)), (At(10, 30), At(12)) };

        var free = CalendarSlots.Free(busy, At(9), At(20));

        Assert.Equal([(At(9), At(10)), (At(12), At(15)), (At(16), At(20))], free);
    }

    [Fact]
    public void Horas_libres_ignora_eventos_fuera_del_horario_y_huecos_cortos()
    {
        var busy = new[] { (At(7), At(9, 15)), (At(9, 30), At(19, 50)), (At(21), At(22)) };

        var free = CalendarSlots.Free(busy, At(9), At(20), TimeSpan.FromMinutes(15));

        Assert.Equal([(At(9, 15), At(9, 30))], free);
    }

    [Fact]
    public void Sin_eventos_esta_libre_todo_el_dia()
    {
        Assert.Equal([(At(9), At(20))], CalendarSlots.Free([], At(9), At(20)));
    }
}
