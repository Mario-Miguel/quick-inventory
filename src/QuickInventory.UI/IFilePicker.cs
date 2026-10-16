namespace QuickInventory.UI;

/// <summary>
/// Pide al usuario que elija un archivo de su equipo. Cada anfitrión lo implementa a su manera:
/// la app de escritorio abre el diálogo "Abrir" de Windows y lee el archivo directamente del disco.
/// </summary>
/// <remarks>
/// No se usa InputFile/MudFileUpload porque en la app de escritorio el archivo pasaría por el
/// WebView2, que se cuelga al leer el archivo y cierra la aplicación.
/// </remarks>
public interface IFilePicker
{
    /// <summary>
    /// Abre el selector mostrando solo los archivos con <paramref name="extensions"/> (".pdf", ".jpg"...).
    /// Devuelve null si el usuario cancela.
    /// </summary>
    Task<PickedFile?> PickAsync(string description, IReadOnlyList<string> extensions);
}

/// <summary>
/// Archivo elegido. El contenido se lee aparte con <see cref="ReadAsync"/>, para poder
/// comprobar antes el tamaño y no cargar en memoria un archivo enorme.
/// </summary>
public sealed record PickedFile(string Name, long Size, Func<Task<byte[]>> ReadAsync);
