using QuickInventory.Core.Models;

namespace QuickInventory.Core.Tests;

public class DeliveryNoteTests
{
    private static DeliveryNoteLine Line(decimal unitPrice, int quantity = 1, TaxRate? rate = null, string description = "Botellas") =>
        new() { Description = description, Quantity = quantity, UnitPrice = unitPrice, TaxRate = rate ?? TaxRate.TR2100 };

    private static DeliveryNote Valid() => new()
    {
        Id = 7,
        ProviderId = 1,
        Number = "AL-1",
        DeliveryDate = DateTime.Today,
        Lines = [Line(10m, 10)],
    };

    [Fact]
    public void Normaliza_textos_fecha_y_calcula_totales()
    {
        var note = Valid();
        note.Number = "  153 ";
        note.Notes = "   ";
        note.DeliveryDate = DateTime.Today.AddHours(9);
        note.Lines = [Line(2m, 3, description: "  Vasos ")];

        note.Normalize();
        note.Validate();

        Assert.Equal("153", note.Number);
        Assert.Null(note.Notes);
        Assert.Equal(DateTime.Today, note.DeliveryDate);
        Assert.Equal("Vasos", note.Lines[0].Description);
        Assert.Equal(6m, note.BaseAmount);
        Assert.Equal(1.26m, note.TaxAmount);
        Assert.Equal(7.26m, note.Amount);
    }

    [Fact]
    public void Acepta_albaranes_sin_valorar()
    {
        var note = Valid();
        note.Lines = [Line(0m, 24)];

        note.Normalize();
        note.Validate();

        Assert.Equal(0m, note.Amount);
    }

    [Fact]
    public void Rechaza_albaranes_incompletos()
    {
        Action<DeliveryNote>[] breakers =
        [
            n => n.ProviderId = 0,
            n => n.Number = "  ",
            n => n.Number = new string('1', DeliveryNote.MaxNumberLength + 1),
            n => n.DeliveryDate = DateTime.Today.AddDays(1),
            n => n.Lines = [],
            n => n.Lines = [Line(1m, 0)],
            n => n.Lines = [Line(-1m)],
            n => n.Lines = [Line(1m, description: " ")],
            n => n.Notes = new string('x', DeliveryNote.MaxNotesLength + 1),
        ];

        foreach (var breaker in breakers)
        {
            var note = Valid();
            breaker(note);
            note.Normalize();
            Assert.Throws<BusinessRuleException>(note.Validate);
        }
    }

    [Fact]
    public void Linea_de_articulo_propone_coste_e_iva_del_producto()
    {
        var product = new ShopProduct { Id = 5, Name = "Vaso", CostPrice = 1.5m, TaxRate = TaxRate.TR1000 };

        var line = DeliveryNoteLine.For(product);

        Assert.Equal(5, line.ProductId);
        Assert.Equal("Vaso", line.Description);
        Assert.Equal(1.5m, line.UnitPrice);
        Assert.Equal(TaxRate.TR1000, line.TaxRate);
    }

    [Fact]
    public void Las_lineas_del_albaran_se_copian_a_la_factura_marcadas()
    {
        var note = Valid();
        note.Lines = [new DeliveryNoteLine { ProductId = 5, Description = "Vaso", Quantity = 12, UnitPrice = 1.5m, TaxRate = TaxRate.TR1000 }, Line(0m, 1, description: "Portes")];

        var lines = PurchaseInvoiceLine.From(note).ToList();

        Assert.Equal(2, lines.Count);
        Assert.All(lines, l => Assert.Equal(7, l.DeliveryNoteId));
        Assert.All(lines, l => Assert.True(l.IsFromDeliveryNote));
        Assert.Equal((5, "Vaso", 12, 1.5m, TaxRate.TR1000),
            (lines[0].ProductId!.Value, lines[0].Description, lines[0].Quantity, lines[0].UnitPrice, lines[0].TaxRate));
        Assert.True(lines[1].IsFree);
    }

    [Fact]
    public void El_resumen_de_la_factura_nombra_los_albaranes_sin_repetir_sus_lineas()
    {
        var note = Valid();
        note.Number = "153";
        var invoice = new PurchaseInvoice
        {
            Lines = [new PurchaseInvoiceLine { Description = "Portes", Quantity = 1, UnitPrice = 5m }, .. PurchaseInvoiceLine.From(note)],
            DeliveryNotes = [note],
        };

        Assert.Equal("Portes, Albarán 153", invoice.Summary);
    }
}
