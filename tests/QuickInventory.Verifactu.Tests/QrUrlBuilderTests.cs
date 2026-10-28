namespace QuickInventory.Verifactu.Tests;

public class QrUrlBuilderTests
{
    /// <summary>
    /// Ejemplo de la especificación del QR (v0.5.0). El documento pone "importe=241.4"; aquí sale "241.40"
    /// porque todos los importes llevan 2 decimales. La AEAT admite los dos.
    /// </summary>
    [Fact]
    public void Ejemplo_de_la_aeat() => Assert.Equal(
        "https://prewww2.aeat.es/wlpl/TIKE-CONT/ValidarQR?nif=89890001K&numserie=12345678%26G33&fecha=01-01-2024&importe=241.40",
        QrUrlBuilder.Build(AeatEnvironment.Test, new InvoiceId("89890001K", "12345678&G33", new DateOnly(2024, 1, 1)), 241.4m));

    [Fact]
    public void Produccion() => Assert.StartsWith(
        "https://www2.agenciatributaria.gob.es/wlpl/TIKE-CONT/ValidarQR?nif=",
        QrUrlBuilder.Build(AeatEnvironment.Production, new InvoiceId("89890001K", "T2027-1", new DateOnly(2027, 1, 1)), 1m));

    [Fact]
    public void Codifica_espacios_y_barras() => Assert.Contains(
        "numserie=T%202027%2F1&",
        QrUrlBuilder.Build(AeatEnvironment.Test, new InvoiceId("89890001K", "T 2027/1", new DateOnly(2027, 1, 1)), 1m));
}
