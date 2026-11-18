using QuickInventory.Core.Models;

namespace QuickInventory.Core.Tests;

public class ProviderTests
{
    [Fact]
    public void Normaliza_antes_de_validar()
    {
        var provider = new Provider { Nif = "b-1234567 4", LegalName = "  Ferretería López  ", Email = "  ", Notes = " llamar por la mañana " };

        provider.Normalize();
        provider.Validate();

        Assert.Equal("B12345674", provider.Nif);
        Assert.Equal("Ferretería López", provider.LegalName);
        Assert.Null(provider.Email);
        Assert.Equal("llamar por la mañana", provider.Notes);
    }

    [Theory]
    [InlineData("", "Ferretería")]
    [InlineData("B12345675", "Ferretería")]
    [InlineData("B12345674", "")]
    public void Rechaza_datos_incompletos_o_nif_incorrecto(string nif, string legalName)
    {
        var provider = new Provider { Nif = nif, LegalName = legalName };
        provider.Normalize();

        Assert.Throws<BusinessRuleException>(provider.Validate);
    }

    [Fact]
    public void CopyFrom_no_cambia_si_esta_activo()
    {
        var saved = new Provider { Id = 7, Active = false };
        saved.CopyFrom(new Provider { Id = 99, Active = true, LegalName = "Nuevo nombre", Email = "a@b.es" });

        Assert.Equal(7, saved.Id);
        Assert.False(saved.Active);
        Assert.Equal("Nuevo nombre", saved.LegalName);
        Assert.Equal("a@b.es", saved.Email);
    }
}

public class ProviderPhoneNumberTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("985 12 34 56")]
    [InlineData("985123456")]
    [InlineData("+34 985 12 34 56")]
    [InlineData("(+351) 21-123.4567")]
    public void Acepta_telefonos_validos_o_vacios(string? phone) =>
        Assert.Null(Provider.PhoneNumberError(phone));

    [Theory]
    [InlineData("12345")]              // pocas cifras
    [InlineData("1234567890123456")]   // más de 15 cifras
    [InlineData("985 12 34 5a")]       // letras
    [InlineData("985+123456")]         // "+" en medio
    [InlineData("++34 985 12 34 56")]  // dos "+"
    [InlineData("985 123 456 ext. 12")]
    public void Rechaza_telefonos_no_validos(string phone) =>
        Assert.NotNull(Provider.PhoneNumberError(phone));

    [Fact]
    public void Normaliza_espacios_y_valida_al_guardar()
    {
        var provider = new Provider { Nif = "B12345674", LegalName = "Ferretería", PhoneNumber = "  985   12 34 56 " };
        provider.Normalize();
        provider.Validate();
        Assert.Equal("985 12 34 56", provider.PhoneNumber);

        provider.PhoneNumber = "abc";
        provider.Normalize();
        Assert.Throws<BusinessRuleException>(provider.Validate);
    }
}
