namespace QuickInventory.Verifactu.Tests;

/// <summary>Los tres ejemplos de «Especificaciones técnicas para generación de la huella» (v0.1.2), apartado 6.</summary>
public class FingerprintCalculatorTests
{
    private const string Huella1 = "3C464DAF61ACB827C65FDA19F352A4E3BDC2C640E9E9FC4CC058073F38F12F60";
    private const string Huella2 = "F7B94CFD8924EDFF273501B01EE5153E4CE8F259766F88CF6ACB8935802A2B97";
    private const string Huella3 = "177547C0D57AC74748561D054A9CEC14B4C4EA23D1BEFD6F2E69E3A388F90C68";

    [Fact]
    public void Caso_1_primer_registro_de_alta() => Assert.Equal(Huella1,
        FingerprintCalculator.ForRegistration("89890001K", "12345678/G33", "01-01-2024", "F1", "12.35", "123.45",
            null, "2024-01-01T19:20:30+01:00"));

    [Fact]
    public void Caso_2_alta_encadenada() => Assert.Equal(Huella2,
        FingerprintCalculator.ForRegistration("89890001K", "12345679/G34", "01-01-2024", "F1", "12.35", "123.45",
            Huella1, "2024-01-01T19:20:35+01:00"));

    [Fact]
    public void Caso_3_anulacion_encadenada() => Assert.Equal(Huella3,
        FingerprintCalculator.ForCancellation("89890001K", "12345679/G34", "01-01-2024",
            Huella2, "2024-01-01T19:20:40+01:00"));

    [Fact]
    public void La_cadena_es_la_del_documento() => Assert.Equal(Huella1, FingerprintCalculator.Compute(
        "IDEmisorFactura=89890001K&NumSerieFactura=12345678/G33&FechaExpedicionFactura=01-01-2024&TipoFactura=F1" +
        "&CuotaTotal=12.35&ImporteTotal=123.45&Huella=&FechaHoraHusoGenRegistro=2024-01-01T19:20:30+01:00"));

    [Fact]
    public void Quita_los_espacios_de_los_extremos() => Assert.Equal(Huella1,
        FingerprintCalculator.ForRegistration(" 89890001K", "12345678/G33 ", "01-01-2024", "F1", "12.35", "123.45",
            "", "2024-01-01T19:20:30+01:00"));
}
