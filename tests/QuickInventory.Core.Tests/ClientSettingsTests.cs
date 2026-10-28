using QuickInventory.Core.Models;

namespace QuickInventory.Core.Tests;

public class ClientSettingsTests
{
    [Fact]
    public void Por_defecto_es_valido_y_sin_modulos_opcionales()
    {
        var settings = new ClientSettings();

        settings.Normalize();
        settings.Validate();

        Assert.False(settings.Features.IsEnabled(ClientFeature.Verifactu));
        Assert.False(settings.Features.IsEnabled(ClientFeature.PrestaShop));
    }

    [Theory]
    [InlineData("2F5D2A")]
    [InlineData("#2F5D2")]
    [InlineData("verde")]
    public void Rechaza_colores_que_no_son_RRGGBB(string color)
    {
        var settings = new ClientSettings { Colors = { Primary = color } };

        Assert.Throws<BusinessRuleException>(settings.Validate);
    }

    [Fact]
    public void Rechaza_nombre_vacio()
    {
        var settings = new ClientSettings { AppName = "   " };
        settings.Normalize();

        Assert.Throws<BusinessRuleException>(settings.Validate);
    }

    [Fact]
    public void Normaliza_y_valida_los_datos_fiscales_si_los_trae()
    {
        var settings = new ClientSettings { Issuer = new Issuer { Nif = "b-12345674", LegalName = " Bodega " } };

        settings.Normalize();
        settings.Validate();
        Assert.Equal("B12345674", settings.Issuer.Nif);

        settings.Issuer.Nif = "B12345675";
        Assert.Throws<BusinessRuleException>(settings.Validate);
    }

    [Fact]
    public void La_base_de_datos_es_opcional_pero_si_esta_necesita_nombre_y_usuario()
    {
        var settings = new ClientSettings
        {
            Database = new ClientDatabase { Host = " servidor.example.com ", Name = "museo", Username = "museo_app" }
        };

        settings.Normalize();
        settings.Validate();
        Assert.Equal("servidor.example.com", settings.Database.Host);

        // El servidor no va en client.json: lo pone el pipeline al compilar.
        settings.Database.Host = "";
        settings.Validate();

        settings.Database.Username = "";
        Assert.Throws<BusinessRuleException>(settings.Validate);

        settings.Database.Username = "museo_app";
        settings.Database.Port = 0;
        Assert.Throws<BusinessRuleException>(settings.Validate);
    }
}
