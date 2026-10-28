using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using Npgsql;
using QuickInventory.Core;
using QuickInventory.Core.Models;
using QuickInventory.UI;

namespace QuickInventory.Desktop;

/// <summary>
/// Guarda la conexión de este equipo en %LOCALAPPDATA%\QuickInventory\appsettings.json
/// (el mismo archivo que lee <see cref="App"/> al arrancar) después de comprobar que funciona.
/// </summary>
public sealed class DesktopConnectionSetup(ClientDatabase database, string settingsPath, string? reason) : IConnectionSetup
{
    public string? Reason => reason;

    public async Task SaveAndRestartAsync(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new BusinessRuleException("Escribe la contraseña.");

        var connectionString = BuildConnectionString(database, password);
        await TestAsync(connectionString);
        Save(connectionString);

        // Se vuelve a abrir para arrancar ya con la base (y seguir con el asistente si es la primera vez).
        Process.Start(Environment.ProcessPath!);
        Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
    }

    public static string BuildConnectionString(ClientDatabase database, string password) => new NpgsqlConnectionStringBuilder
    {
        Host = database.Host,
        Port = database.Port,
        Database = database.Name,
        Username = database.Username,
        Password = password,
        // La contraseña viaja por internet: cifrado y comprobando que el servidor es quien dice ser.
        SslMode = SslMode.VerifyFull,
        // Las bases que se apagan solas cuando no se usan (Neon) tardan unos segundos en despertar.
        Timeout = 15,
    }.ConnectionString;

    private static async Task TestAsync(string connectionString)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.InvalidPassword or PostgresErrorCodes.InvalidAuthorizationSpecification)
        {
            throw new BusinessRuleException("La contraseña no es correcta. Revisa que esté bien escrita.");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidCatalogName)
        {
            throw new BusinessRuleException("La base de datos no existe en el servidor. Avisa a quien instaló la aplicación.");
        }
        catch (Exception ex) when (ex is NpgsqlException or SocketException or TimeoutException)
        {
            throw new BusinessRuleException("No se llega al servidor de la base de datos. Comprueba la conexión a internet.");
        }
    }

    private void Save(string connectionString)
    {
        // Se conserva lo que hubiera en el archivo; solo se cambia la conexión.
        JsonObject settings;
        try
        {
            settings = File.Exists(settingsPath) ? JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject ?? [] : [];
        }
        catch (JsonException)
        {
            settings = [];
        }

        if (settings["ConnectionStrings"] is not JsonObject connectionStrings)
            settings["ConnectionStrings"] = connectionStrings = [];
        connectionStrings["Museo"] = connectionString;

        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}
