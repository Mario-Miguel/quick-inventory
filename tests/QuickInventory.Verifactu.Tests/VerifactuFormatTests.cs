namespace QuickInventory.Verifactu.Tests;

public class VerifactuFormatTests
{
    [Theory]
    [InlineData("123.45", "123.45")]
    [InlineData("123.1", "123.10")]
    [InlineData("-5", "-5.00")]
    [InlineData("0.5", "0.50")]
    [InlineData("1234567.89", "1234567.89")] // Sin separador de miles
    public void Importes_con_punto_y_dos_decimales(string importe, string esperado) =>
        Assert.Equal(esperado, VerifactuFormat.Amount(decimal.Parse(importe, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("21", "21")]
    [InlineData("7.5", "7.5")]
    [InlineData("10.00", "10")]
    public void Tipos_sin_ceros_de_sobra(string tipo, string esperado) =>
        Assert.Equal(esperado, VerifactuFormat.Rate(decimal.Parse(tipo, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Fechas() => Assert.Equal("05-03-2027", VerifactuFormat.Date(new DateOnly(2027, 3, 5)));

    [Fact]
    public void Fecha_y_hora_con_huso()
    {
        Assert.Equal("2027-01-15T11:20:30+01:00", VerifactuFormat.DateTime(Ejemplos.Ahora));
        Assert.Equal("2027-07-01T09:05:00+02:00", VerifactuFormat.DateTime(new DateTimeOffset(2027, 7, 1, 9, 5, 0, TimeSpan.FromHours(2))));
    }

    [Fact]
    public void La_hora_de_la_peninsula_cambia_de_huso_en_verano()
    {
        Assert.Equal(TimeSpan.FromHours(1), SpainTime.From(new DateTimeOffset(2027, 1, 15, 10, 0, 0, TimeSpan.Zero)).Offset);
        Assert.Equal(TimeSpan.FromHours(2), SpainTime.From(new DateTimeOffset(2027, 7, 15, 10, 0, 0, TimeSpan.Zero)).Offset);
        Assert.Equal(0, SpainTime.Now().Millisecond);
    }

    [Fact]
    public void La_hora_de_canarias_va_una_hora_por_detras()
    {
        Assert.Equal(TimeSpan.Zero, SpainTime.From(new DateTimeOffset(2027, 1, 15, 10, 0, 0, TimeSpan.Zero), SpanishTimeZone.CanaryIslands).Offset);
        Assert.Equal(TimeSpan.FromHours(1), SpainTime.From(new DateTimeOffset(2027, 7, 15, 10, 0, 0, TimeSpan.Zero), SpanishTimeZone.CanaryIslands).Offset);
        Assert.Equal(0, SpainTime.Now(SpanishTimeZone.CanaryIslands).Millisecond);
    }

    [Theory]
    [InlineData("T2027-000123", true)]
    [InlineData("12345678/G33", true)]
    [InlineData("12345678&G33", true)]
    [InlineData("", false)]
    [InlineData("T2027<1", false)]
    [InlineData("T2027=1", false)]
    [InlineData("T2027'1", false)]
    [InlineData("AÑO2027-1", false)] // Solo ASCII
    public void Numero_de_factura(string numero, bool valido) => Assert.Equal(valido, VerifactuFormat.IsValidInvoiceNumber(numero));

    [Fact]
    public void Numero_de_factura_de_mas_de_60_caracteres() => Assert.False(VerifactuFormat.IsValidInvoiceNumber(new string('1', 61)));

    [Theory]
    [InlineData("12.34", true)]
    [InlineData("12.345", false)]
    [InlineData("999999999999.99", true)]
    [InlineData("1000000000000", false)]
    public void Importes_validos(string importe, bool valido) =>
        Assert.Equal(valido, VerifactuFormat.IsValidAmount(decimal.Parse(importe, System.Globalization.CultureInfo.InvariantCulture)));
}
