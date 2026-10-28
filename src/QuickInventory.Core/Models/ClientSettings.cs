namespace QuickInventory.Core.Models;

/// <summary>
/// Personalización de la aplicación para un cliente: nombre, colores, módulos y, si se conocen,
/// los datos fiscales. Sale de <c>clientes/&lt;id&gt;/cliente.json</c> y va dentro del ejecutable
/// (se elige al compilar con <c>-p:Cliente=&lt;id&gt;</c>), así que el cliente no puede cambiarla.
/// </summary>
/// <remarks>No lleva la conexión a la base de datos: tiene contraseña y va en appsettings.json.</remarks>
public sealed class ClientSettings
{
    /// <summary>Nombre que sale en la barra superior y en el título de la ventana.</summary>
    public string AppName { get; set; } = "Quick Inventory";

    public ClientColors Colors { get; set; } = new();

    public ClientFeatures Features { get; set; } = new();

    /// <summary>
    /// Datos fiscales del titular (opcional). Si están, el asistente de la primera instalación
    /// aparece ya relleno y solo hay que comprobarlos.
    /// </summary>
    public Issuer? Issuer { get; set; }

    public void Normalize()
    {
        AppName = AppName?.Trim() ?? "";
        Issuer?.Normalize();
    }

    /// <exception cref="BusinessRuleException">Si falta algo o un color no es válido.</exception>
    public void Validate()
    {
        if (AppName.Length == 0)
            throw new BusinessRuleException("Falta el nombre de la aplicación (AppName).");
        CheckColor(Colors.Primary, nameof(ClientColors.Primary));
        CheckColor(Colors.Secondary, nameof(ClientColors.Secondary));
        CheckColor(Colors.Background, nameof(ClientColors.Background));
        Issuer?.Validate();
    }

    private static void CheckColor(string? color, string name)
    {
        if (!IsHexColor(color))
            throw new BusinessRuleException($"El color {name} tiene que ir en formato #RRGGBB (p. ej. \"#2F5D2A\").");
    }

    private static bool IsHexColor(string? color) =>
        color is { Length: 7 } && color[0] == '#' && color.Skip(1).All(char.IsAsciiHexDigit);
}

public sealed class ClientColors
{
    /// <summary>Botones, enlaces y barra superior.</summary>
    public string Primary { get; set; } = "#2F5D2A";
    public string Secondary { get; set; } = "#C8922A";
    /// <summary>Fondo de las pantallas.</summary>
    public string Background { get; set; } = "#FAF7F2";
}

/// <summary>Módulos opcionales. Los que están desactivados no aparecen en la pantalla principal.</summary>
public sealed class ClientFeatures
{
    /// <summary>Envío de facturas a la AEAT. Todavía no lo usa la app (ver docs/verifactu.md).</summary>
    public bool Verifactu { get; set; }

    /// <summary>Entradas vendidas en la tienda online (PrestaShop).</summary>
    public bool PrestaShop { get; set; }

    public bool IsEnabled(ClientFeature feature) => feature switch
    {
        ClientFeature.Verifactu => Verifactu,
        ClientFeature.PrestaShop => PrestaShop,
        _ => false
    };
}

public enum ClientFeature
{
    Verifactu,
    PrestaShop,
}
