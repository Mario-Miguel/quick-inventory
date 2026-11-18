using QuickInventory.Core.Models;

namespace QuickInventory.Core.Tests;

public class PurchaseInvoiceTests
{
    private static PurchaseInvoiceLine Line(decimal unitPrice, int quantity = 1, TaxRate? rate = null, string description = "Botellas") =>
        new() { Description = description, Quantity = quantity, UnitPrice = unitPrice, TaxRate = rate ?? TaxRate.TR2100 };

    private static PurchaseInvoice Valid() => new()
    {
        ProviderId = 1,
        Number = "A-1",
        InvoiceDate = DateTime.Today,
        Lines = [Line(10m, 10)],
    };

    [Fact]
    public void Normaliza_textos_fecha_y_calcula_totales()
    {
        var invoice = Valid();
        invoice.Number = "  2026/153 ";
        invoice.Notes = "   ";
        invoice.InvoiceDate = DateTime.Today.AddHours(15);
        invoice.Lines = [Line(1.005m, 3, description: "  Vasos ")];

        invoice.Normalize();
        invoice.Validate();

        Assert.Equal("2026/153", invoice.Number);
        Assert.Null(invoice.Notes);
        Assert.Equal(DateTime.Today, invoice.InvoiceDate);
        Assert.Equal("Vasos", invoice.Lines[0].Description);
        Assert.Equal(1.01m, invoice.Lines[0].UnitPrice);
        Assert.Equal(3.03m, invoice.BaseAmount);
        Assert.Equal(0.64m, invoice.TaxAmount); // 3,03 × 21 % = 0,6363
        Assert.Equal(3.67m, invoice.Amount);
    }

    [Fact]
    public void Desglosa_el_iva_por_tipo_de_mayor_a_menor()
    {
        var invoice = Valid();
        invoice.Lines =
        [
            Line(10m, 2, TaxRate.TR1000),
            Line(5m, 4, TaxRate.TR2100),
            Line(3m, 1, TaxRate.TR1000),
            Line(7m, 1, TaxRate.TR000),
        ];

        invoice.CalculateTotals();
        var breakdown = invoice.TaxBreakdown();

        Assert.Equal([TaxRate.TR2100, TaxRate.TR1000, TaxRate.TR000], breakdown.Select(i => i.TaxRate));
        Assert.Equal(new PurchaseTaxItem(TaxRate.TR2100, 20m, 4.20m), breakdown[0]);
        Assert.Equal(new PurchaseTaxItem(TaxRate.TR1000, 23m, 2.30m), breakdown[1]);
        Assert.Equal(new PurchaseTaxItem(TaxRate.TR000, 7m, 0m), breakdown[2]);
        Assert.Equal(50m, invoice.BaseAmount);
        Assert.Equal(6.50m, invoice.TaxAmount);
        Assert.Equal(56.50m, invoice.Amount);
    }

    [Fact]
    public void El_iva_se_redondea_por_tipo_y_no_por_linea()
    {
        // Tres líneas de 0,07 € al 21 %: línea a línea serían 3 × 0,01 = 0,03; sobre la suma (0,21 × 21 % = 0,0441)
        // es 0,04, que es lo que pone la factura.
        var invoice = Valid();
        invoice.Lines = [Line(0.07m), Line(0.07m), Line(0.07m)];

        invoice.CalculateTotals();

        Assert.Equal(0.04m, invoice.TaxAmount);
    }

    [Fact]
    public void Linea_de_articulo_propone_coste_e_iva_del_producto()
    {
        var withRate = new ShopProduct { Id = 5, Name = "Vaso", CostPrice = 1.5m, TaxRate = TaxRate.TR1000 };
        var exempt = new ShopProduct { Id = 6, Name = "Libro", CostPrice = 8m, TaxExemption = TaxExemption.E1 };

        var line = PurchaseInvoiceLine.For(withRate);
        Assert.Equal(5, line.ProductId);
        Assert.Equal("Vaso", line.Description);
        Assert.Equal(1.5m, line.UnitPrice);
        Assert.Equal(TaxRate.TR1000, line.TaxRate);
        Assert.False(line.IsFree);

        Assert.Equal(TaxRate.TR000, PurchaseInvoiceLine.For(exempt).TaxRate);
    }

    [Fact]
    public void Rechaza_facturas_incompletas()
    {
        Action<PurchaseInvoice>[] breakers =
        [
            i => i.ProviderId = 0,
            i => i.Number = "  ",
            i => i.InvoiceDate = DateTime.Today.AddDays(1),
            i => i.Lines = [],
            i => i.Lines = [Line(10m, description: " ")],
            i => i.Lines = [Line(10m, quantity: 0)],
            i => i.Lines = [Line(-1m)],
            i => i.Lines = [Line(0m)], // total 0
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
    public void Resumen_de_lineas()
    {
        var invoice = Valid();
        invoice.Lines = [Line(1m, 24, description: "Botella"), Line(1m, 1, description: "Portes")];

        Assert.Equal("Botella × 24, Portes", invoice.Summary);
    }

    [Fact]
    public void Sin_fecha_de_pago_se_quita_la_forma_de_pago()
    {
        var invoice = Valid();
        invoice.PaymentMethod = PaymentMethod.Cash;

        invoice.Normalize();
        invoice.Validate();

        Assert.False(invoice.IsPaid);
        Assert.Null(invoice.PaymentMethod);
    }

    [Fact]
    public void Pagada_exige_forma_de_pago_y_fecha_no_futura()
    {
        var withoutMethod = Valid();
        withoutMethod.PaymentDate = DateTime.Today;
        withoutMethod.Normalize();
        Assert.Throws<BusinessRuleException>(withoutMethod.Validate);

        var future = Valid();
        future.PaymentDate = DateTime.Today.AddDays(1);
        future.PaymentMethod = PaymentMethod.BankTransfer;
        future.Normalize();
        Assert.Throws<BusinessRuleException>(future.Validate);
    }

    [Theory]
    [InlineData(PaymentMethod.Cash, SalesPoint.TicketOffice, true)]
    [InlineData(PaymentMethod.Cash, SalesPoint.Shop, true)]
    [InlineData(PaymentMethod.Cash, null, false)]          // en efectivo hay que decir de qué caja sale
    [InlineData(PaymentMethod.Cash, SalesPoint.Admin, false)]
    [InlineData(PaymentMethod.Card, null, true)]
    [InlineData(PaymentMethod.BankTransfer, null, true)]
    [InlineData(PaymentMethod.Card, SalesPoint.Shop, false)] // tarjeta o transferencia no salen de una caja
    [InlineData(PaymentMethod.BankTransfer, SalesPoint.TicketOffice, false)]
    public void Pago_en_efectivo_sale_de_una_caja(PaymentMethod method, SalesPoint? salesPoint, bool valid)
    {
        var invoice = Valid();
        invoice.PaymentDate = DateTime.Today;
        invoice.PaymentMethod = method;
        invoice.PaymentSalesPoint = salesPoint;
        invoice.Normalize();

        if (valid)
            invoice.Validate();
        else
            Assert.Throws<BusinessRuleException>(invoice.Validate);
    }

    [Fact]
    public void Pendiente_de_pago_no_tiene_caja()
    {
        var invoice = Valid();
        invoice.PaymentMethod = PaymentMethod.Cash;
        invoice.PaymentSalesPoint = SalesPoint.Shop;

        invoice.Normalize();

        Assert.Null(invoice.PaymentMethod);
        Assert.Null(invoice.PaymentSalesPoint);
    }

    [Theory]
    [InlineData("factura.exe", 10)]
    [InlineData("factura.pdf", 0)]
    public void Rechaza_archivos_no_validos(string fileName, int size)
    {
        var file = new InvoiceFile { FileName = fileName, Content = new byte[size] };

        Assert.Throws<BusinessRuleException>(file.Validate);
    }
}
