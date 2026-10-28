using QuickInventory.Core.Models;

namespace QuickInventory.Core.Tests;

public class TaxBreakdownTests
{
    private static SalesLine Line(int amount, decimal unitPrice, decimal? taxRate, TaxExemption? exemption = null) =>
        new() { Amount = amount, UnitPrice = unitPrice, BasePrice = unitPrice, TaxRate = taxRate, TaxExemption = exemption };

    [Fact]
    public void Redondea_por_tipo_y_no_por_linea()
    {
        // Por unidad: 3,50 / 1,21 = 2,89 → 3 × 2,89 = 8,67. Sobre el total: 10,50 / 1,21 = 8,677… → 8,68.
        var item = Assert.Single(TaxBreakdown.Calculate([Line(3, 3.50m, 21)]));

        Assert.Equal(8.68m, item.TaxBase);
        Assert.Equal(1.82m, item.TaxAmount);
        Assert.Equal(10.50m, item.Total);
    }

    [Fact]
    public void Junta_las_lineas_del_mismo_tipo()
    {
        var items = TaxBreakdown.Calculate([Line(1, 3.50m, 21), Line(2, 4.00m, 21), Line(1, 15.00m, 10)]);

        Assert.Equal(2, items.Count);
        Assert.Equal(21m, items[0].TaxRate);
        Assert.Equal(11.50m, items[0].Total);
        Assert.Equal(10m, items[1].TaxRate);
        Assert.Equal(15.00m, items[1].Total);
    }

    [Fact]
    public void Exenta_no_lleva_cuota()
    {
        var item = Assert.Single(TaxBreakdown.Calculate([Line(2, 5.00m, null, TaxExemption.E1)]));

        Assert.Equal(10.00m, item.TaxBase);
        Assert.Equal(0m, item.TaxAmount);
        Assert.Equal("Exento (E1)", item.Name);
    }

    [Fact]
    public void Sin_iva_indicado_no_inventa_base_ni_cuota()
    {
        var item = Assert.Single(TaxBreakdown.Calculate([Line(1, 5.00m, null)]));

        Assert.Null(item.TaxBase);
        Assert.Null(item.TaxAmount);
        Assert.Equal(5.00m, item.Total);
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(0.05)]
    [InlineData(1.00)]
    [InlineData(3.33)]
    [InlineData(99.99)]
    [InlineData(-10.50)]
    public void Base_mas_cuota_es_siempre_el_total(decimal price)
    {
        foreach (var rate in new[] { 21m, 10m, 4m, 0m })
        {
            var item = Assert.Single(TaxBreakdown.Calculate([Line(1, price, rate)]));
            Assert.Equal(price, item.TaxBase + item.TaxAmount);
        }
    }
}
