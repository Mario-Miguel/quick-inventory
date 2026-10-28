using QuickInventory.Core.Models;

namespace QuickInventory.Core.Tests;

public class TaxIdTests
{
    [Theory]
    [InlineData("12345678Z")] // DNI
    [InlineData("X1234567L")] // NIE
    [InlineData("Y1234567X")] // NIE
    [InlineData("A58818501")] // Sociedad anónima: control numérico
    [InlineData("B12345674")] // Sociedad limitada: control numérico
    [InlineData("G33123456")] // Asociación o fundación: número o letra
    [InlineData("G3312345F")]
    [InlineData("P3304200C")] // Entidad local: control con letra
    [InlineData("Q2826000H")] // Organismo público: control con letra
    public void Acepta_nif_validos(string nif) => Assert.True(TaxId.IsValid(nif));

    [Theory]
    [InlineData("")]
    [InlineData("12345678")] // Falta la letra
    [InlineData("12345678A")] // Letra equivocada
    [InlineData("X1234567A")]
    [InlineData("A58818502")] // Control equivocado
    [InlineData("A5881850A")] // Sociedad anónima con letra de control
    [InlineData("P33042003")] // Entidad local con número de control
    [InlineData("I1234567A")] // Letra de entidad que no existe
    [InlineData("123456789Z")] // Demasiado largo
    public void Rechaza_nif_incorrectos(string nif) => Assert.False(TaxId.IsValid(nif));

    [Theory]
    [InlineData(" g-33.123.456 ", "G33123456")]
    [InlineData("12 345 678-z", "12345678Z")]
    [InlineData(null, "")]
    public void Normaliza_espacios_guiones_y_minusculas(string? escrito, string esperado) =>
        Assert.Equal(esperado, TaxId.Normalize(escrito));
}
