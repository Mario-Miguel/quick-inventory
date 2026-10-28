namespace QuickInventory.UI;

/// <summary>
/// Actualizaciones de la aplicación. Las versiones las publica el pipeline de GitHub Actions
/// (ver docs/publicacion.md); la app de escritorio las descarga con Velopack y la futura web
/// no lo necesitará (siempre está al día).
/// </summary>
public interface IAppUpdater
{
    /// <summary>
    /// False si esta copia no se puede actualizar: no se instaló con el instalador (p. ej. al depurar)
    /// o se compiló sin dirección de actualizaciones.
    /// </summary>
    bool IsAvailable { get; }

    string CurrentVersion { get; }

    /// <summary>Número de la versión nueva publicada, o null si ya está al día.</summary>
    Task<string?> CheckAsync();

    /// <summary>
    /// Descarga la versión encontrada por <see cref="CheckAsync"/>, cierra la aplicación y la vuelve
    /// a abrir ya actualizada. <paramref name="progress"/> recibe el porcentaje descargado.
    /// </summary>
    Task UpdateAndRestartAsync(Action<int>? progress = null);
}
