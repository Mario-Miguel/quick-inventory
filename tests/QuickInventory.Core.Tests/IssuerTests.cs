using QuickInventory.Core.Models;

namespace QuickInventory.Core.Tests;

public class IssuerTests
{
    [Fact]
    public void Normaliza_antes_de_validar()
    {
        var issuer = new Issuer { Nif = "g-33123456", LegalName = "  Fundación  ", Address = "   " };

        issuer.Normalize();
        issuer.Validate();

        Assert.Equal("G33123456", issuer.Nif);
        Assert.Equal("Fundación", issuer.LegalName);
        Assert.Null(issuer.Address);
    }

    [Theory]
    [InlineData("", "Fundación")]
    [InlineData("G33123457", "Fundación")]
    [InlineData("G33123456", "")]
    public void Rechaza_datos_incompletos_o_nif_incorrecto(string nif, string legalName)
    {
        var issuer = new Issuer { Nif = nif, LegalName = legalName };
        issuer.Normalize();

        Assert.Throws<BusinessRuleException>(issuer.Validate);
    }
}
