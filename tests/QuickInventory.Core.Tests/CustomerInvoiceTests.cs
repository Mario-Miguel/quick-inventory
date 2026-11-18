using QuickInventory.Core.Models;

namespace QuickInventory.Core.Tests;

public class CustomerInvoiceTests
{
    private static CustomerInvoiceLine Line(decimal unitPrice, int quantity = 1, TaxRate? rate = null, TaxExemption? exemption = null,
        string description = "Entrada escolar") =>
        new()
        {
            Description = description,
            Quantity = quantity,
            UnitPrice = unitPrice,
            TaxRate = exemption is null ? rate ?? TaxRate.TR2100 : null,
            TaxExemption = exemption,
        };

    private static CustomerInvoice Valid() => new()
    {
        CustomerId = 1,
        InvoiceDate = DateTime.Today,
        Lines = [Line(4m, 45)],
    };

    [Fact]
    public void Normaliza_y_calcula_totales_con_precios_sin_iva()
    {
        var invoice = Valid();
        invoice.Notes = "  ";
        invoice.InvoiceDate = DateTime.Today.AddHours(10);
        invoice.Lines = [Line(4.132m, 45, description: "  Entrada escolar ")];

        invoice.Normalize();
        invoice.Validate();

        Assert.Null(invoice.Notes);
        Assert.Equal(DateTime.Today, invoice.InvoiceDate);
        Assert.Equal("Entrada escolar", invoice.Lines[0].Description);
        Assert.Equal(4.13m, invoice.Lines[0].UnitPrice);
        Assert.Equal(185.85m, invoice.BaseAmount);
        Assert.Equal(39.03m, invoice.TaxAmount); // 185,85 × 21 % = 39,0285
        Assert.Equal(224.88m, invoice.Amount);
    }

    [Fact]
    public void Desglosa_tipos_y_exenciones_aparte()
    {
        var invoice = Valid();
        invoice.Lines =
        [
            Line(30m, 1, TaxRate.TR1000, description: "Taller"),
            Line(4m, 10),
            Line(2m, 10, exemption: TaxExemption.E1, description: "Visita guiada"),
            Line(1m, 5),
        ];

        invoice.CalculateTotals();
        var breakdown = invoice.TaxBreakdown();

        Assert.Equal(3, breakdown.Count);
        Assert.Equal((21m, 45m, 9.45m), (breakdown[0].TaxRate!.Value, breakdown[0].TaxBase!.Value, breakdown[0].TaxAmount!.Value));
        Assert.Equal((10m, 30m, 3m), (breakdown[1].TaxRate!.Value, breakdown[1].TaxBase!.Value, breakdown[1].TaxAmount!.Value));
        Assert.Null(breakdown[2].TaxRate);
        Assert.Equal(TaxExemption.E1, breakdown[2].TaxExemption);
        Assert.Equal(20m, breakdown[2].TaxBase);
        Assert.Equal(0m, breakdown[2].TaxAmount);
        Assert.Equal(95m, invoice.BaseAmount);
        Assert.Equal(12.45m, invoice.TaxAmount);
        Assert.Equal(107.45m, invoice.Amount);
    }

    [Fact]
    public void Con_exencion_se_quita_el_tipo()
    {
        var line = new CustomerInvoiceLine { Description = "Visita", UnitPrice = 1m, TaxRate = TaxRate.TR2100, TaxExemption = TaxExemption.E1 };

        line.Normalize();

        Assert.Null(line.TaxRate);
        Assert.Equal(TaxExemption.E1, line.TaxExemption);
    }

    [Fact]
    public void Rechaza_facturas_incompletas()
    {
        Action<CustomerInvoice>[] breakers =
        [
            i => i.CustomerId = 0,
            i => i.InvoiceDate = DateTime.Today.AddDays(1),
            i => i.Lines = [],
            i => i.Lines = [Line(0m)],
            i => i.Lines = [Line(1m, 0)],
            i => i.Lines = [Line(-1m)],
            i => i.Lines = [Line(1m, description: " ")],
            i => i.Lines = [new CustomerInvoiceLine { Description = "Sin IVA", UnitPrice = 1m, TaxRate = null }],
            i => i.Notes = new string('x', CustomerInvoice.MaxNotesLength + 1),
        ];

        foreach (var breaker in breakers)
        {
            var invoice = Valid();
            breaker(invoice);
            invoice.Normalize();
            Assert.Throws<BusinessRuleException>(invoice.Validate);
        }
    }

    [Fact]
    public void El_numero_completo_lleva_la_serie()
    {
        var invoice = new CustomerInvoice { SeriesCode = "FC2026", Number = 12 };

        Assert.Equal("FC2026-000012", invoice.InvoiceId);
        Assert.Equal("FC2026", CustomerInvoiceSeries.SuggestedCode(2026));
    }

    [Fact]
    public void El_cliente_necesita_direccion()
    {
        var customer = new Customer { LegalName = "Colegio Público La Corredoria", Nif = "B12345674" };
        customer.Normalize();
        var ex = Assert.Throws<BusinessRuleException>(customer.Validate);
        Assert.Contains("dirección", ex.Message);

        customer.Address = "C/ Ejemplo 1, 33011 Oviedo";
        customer.Normalize();
        customer.Validate();
    }
}
