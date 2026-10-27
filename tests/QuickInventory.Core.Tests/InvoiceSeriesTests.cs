using QuickInventory.Core.Models;

namespace QuickInventory.Core.Tests;

public class InvoiceSeriesTests
{
    [Fact]
    public void Normaliza_el_codigo_antes_de_validar()
    {
        var series = new InvoiceSeries { Code = "  f2027 ", Year = DateTime.Now.Year };

        series.Normalize();
        series.Validate();

        Assert.Equal("F2027", series.Code);
    }

    [Fact]
    public void El_proximo_numero_sigue_al_ultimo()
    {
        var series = new InvoiceSeries { Code = "F2027", LastNumber = 145 };

        Assert.Equal("F2027-000146", series.NextInvoiceId);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("F2027", -1)]
    [InlineData("F 2027", 0)]
    [InlineData("F<2027", 0)]
    [InlineData("FACTURAS-2027-NUEVA-SERIE", 0)]
    public void Rechaza_series_incorrectas(string code, int lastNumber)
    {
        var series = new InvoiceSeries { Code = code, Year = DateTime.Now.Year, LastNumber = lastNumber };
        series.Normalize();

        Assert.Throws<BusinessRuleException>(series.Validate);
    }

    [Fact]
    public void Rechaza_un_anio_futuro()
    {
        var series = new InvoiceSeries { Code = "F2099", Year = DateTime.Now.Year + 2 };

        Assert.Throws<BusinessRuleException>(series.Validate);
    }
}
