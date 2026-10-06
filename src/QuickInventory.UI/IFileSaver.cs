namespace QuickInventory.UI;

/// <summary>
/// Guarda un archivo generado por la aplicación donde elija el usuario.
/// Cada anfitrión lo implementa a su manera: la app de escritorio abre el diálogo
/// "Guardar como" de Windows y la futura versión web lo descargará desde el navegador.
/// </summary>
public interface IFileSaver
{
    /// <summary>
    /// Pide al usuario dónde guardar el archivo y lo escribe.
    /// Devuelve false si el usuario cancela.
    /// </summary>
    Task<bool> SaveAsync(string suggestedName, byte[] content);
}
