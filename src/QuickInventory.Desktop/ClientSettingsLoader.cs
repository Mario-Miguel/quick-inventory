using System.Reflection;
using System.Text.Json;
using QuickInventory.Core;
using QuickInventory.Core.Models;

namespace QuickInventory.Desktop;

/// <summary>
/// Lee la personalización del client (clients/&lt;id&gt;/client.json), que va incrustada
/// en el ejecutable al compilar (ver QuickInventory.Desktop.csproj).
/// </summary>
public static class ClientSettingsLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Solo en Debug: id del client (carpeta de clients/) con el que arrancar en lugar del compilado.
    /// La pone la lista de clientes de .vscode/launch.json al depurar.
    /// </summary>
    public const string ClientVariable = "QUICKINVENTORY_CLIENT";

    /// <exception cref="BusinessRuleException">Si el archivo falta o tiene algún dato mal.</exception>
    public static ClientSettings Load()
    {
        using var stream = typeof(ClientSettingsLoader).Assembly.GetManifestResourceStream(ResourceName())
            ?? throw new BusinessRuleException("El programa no lleva la configuración del client (client.json).");

        ClientSettings? settings;
        try
        {
            settings = JsonSerializer.Deserialize<ClientSettings>(stream, Options);
        }
        catch (JsonException ex)
        {
            throw new BusinessRuleException($"La configuración del client (client.json) no es válida: {ex.Message}");
        }
        if (settings is null)
            throw new BusinessRuleException("La configuración del client (client.json) está vacía.");

        settings.Normalize();
        try
        {
            settings.Validate();
        }
        catch (BusinessRuleException ex)
        {
            throw new BusinessRuleException($"Error en la configuración del client (client.json): {ex.Message}");
        }
        return settings;
    }

    private static string ResourceName()
    {
#if DEBUG
        // En Debug van dentro todos los clients: el compilado como "client.json" y los demás como
        // "clients/<id>/client.json" (ver el .csproj).
        var id = Environment.GetEnvironmentVariable(ClientVariable)?.Trim();
        if (!string.IsNullOrEmpty(id))
        {
            var assembly = typeof(ClientSettingsLoader).Assembly;
            var clients = assembly.GetManifestResourceNames()
                .Where(n => n.StartsWith("clients/"))
                .ToDictionary(n => n.Split('/')[1], StringComparer.OrdinalIgnoreCase);
            var compiled = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "Client")?.Value;
            if (compiled is not null) clients[compiled] = "client.json";
            return clients.TryGetValue(id, out var name)
                ? name
                : throw new BusinessRuleException(
                    $"No existe el client «{id}» ({ClientVariable}). Clients: {string.Join(", ", clients.Keys.Order())}.");
        }
#endif
        return "client.json";
    }
}
