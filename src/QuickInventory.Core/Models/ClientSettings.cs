namespace QuickInventory.Core.Models;

/// <summary>
/// Personalización de la aplicación para un cliente: nombre, colores, módulos y, si se conocen,
/// los datos fiscales. Sale de <c>clientes/&lt;id&gt;/cliente.json</c> y va dentro del ejecutable
/// (se elige al compilar con <c>-p:Cliente=&lt;id&gt;</c>), así que el cliente no puede cambiarla.
/// </summary>
/// <remarks>
/// No lleva la contraseña de la base de datos: la escribe quien instala la app en cada equipo
/// (ver <see cref="Database"/>) y se guarda en appsettings.json.
/// </remarks>
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

    /// <summary>
    /// Base de datos del cliente, sin la contraseña (opcional). Si está, la primera vez que se abre la app
    /// en un equipo solo se pide la contraseña. Si no, la conexión tiene que venir de appsettings.json.
    /// </summary>
    public ClientDatabase? Database { get; set; }

    public void Normalize()
    {
        AppName = AppName?.Trim() ?? "";
        Issuer?.Normalize();
        Database?.Normalize();
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
        Database?.Validate();
    }

    private static void CheckColor(string? color, string name)
    {
        if (!IsHexColor(color))
            throw new BusinessRuleException($"El color {name} tiene que ir en formato #RRGGBB (p. ej. \"#2F5D2A\").");
    }

    private static bool IsHexColor(string? color) =>
        color is { Length: 7 } && color[0] == '#' && color.Skip(1).All(char.IsAsciiHexDigit);
}

/// <summary>Dónde está la base de datos del cliente. La contraseña nunca va aquí.</summary>
public sealed class ClientDatabase
{
    /// <summary>
    /// Servidor. No se escribe en client.json (el repo es público): lo mete el pipeline al compilar
    /// desde el secreto DB_HOST (-p:DbHost). Sin él no se puede pedir solo la contraseña.
    /// </summary>
    public string Host { get; set; } = "";
    public int Port { get; set; } = 5432;
    /// <summary>Nombre de la base de datos en el servidor.</summary>
    public string Name { get; set; } = "";
    /// <summary>Usuario de la aplicación (museo_app), sin permiso para cambiar el esquema.</summary>
    public string Username { get; set; } = "";

    public void Normalize()
    {
        Host = Host?.Trim() ?? "";
        Name = Name?.Trim() ?? "";
        Username = Username?.Trim() ?? "";
    }

    public void Validate()
    {
        if (Name.Length == 0 || Username.Length == 0)
            throw new BusinessRuleException("En Database faltan Name o Username.");
        if (Port is < 1 or > 65535)
            throw new BusinessRuleException("El puerto de la base de datos (Database.Port) no es válido.");
    }
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
