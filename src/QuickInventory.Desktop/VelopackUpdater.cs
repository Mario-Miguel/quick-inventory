using System.Reflection;
using QuickInventory.Core;
using QuickInventory.UI;
using Velopack;

namespace QuickInventory.Desktop;

/// <summary>
/// Actualizaciones con Velopack. La dirección de donde se descargan la mete el pipeline al compilar
/// (-p:UpdateUrl=..., una release de GitHub por cliente; ver docs/publicacion.md).
/// </summary>
public sealed class VelopackUpdater : IAppUpdater
{
    private readonly UpdateManager? manager;
    private UpdateInfo? pending;

    public VelopackUpdater()
    {
        var url = typeof(VelopackUpdater).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "UpdateUrl")?.Value;
        if (string.IsNullOrWhiteSpace(url)) return;

        var updates = new UpdateManager(url);
        if (updates.IsInstalled) manager = updates;
    }

    public bool IsAvailable => manager is not null;

    public string CurrentVersion =>
        manager?.CurrentVersion?.ToString()
        ?? typeof(VelopackUpdater).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "";

    public async Task<string?> CheckAsync()
    {
        if (manager is null) return null;
        pending = await manager.CheckForUpdatesAsync();
        return pending?.TargetFullRelease.Version.ToString();
    }

    public async Task UpdateAndRestartAsync(Action<int>? progress = null)
    {
        if (manager is null || pending is null)
            throw new BusinessRuleException("No hay ninguna actualización pendiente.");

        await manager.DownloadUpdatesAsync(pending, progress);
        // Cierra la app ahora mismo, instala la versión nueva y la vuelve a abrir.
        manager.ApplyUpdatesAndRestart(pending);
    }
}
