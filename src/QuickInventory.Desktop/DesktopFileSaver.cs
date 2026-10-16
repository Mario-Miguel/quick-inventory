using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using QuickInventory.UI;

namespace QuickInventory.Desktop;

/// <summary>Guarda los archivos con el diálogo "Guardar como" de Windows.</summary>
public sealed class DesktopFileSaver : IFileSaver
{
    public async Task<bool> SaveAsync(string suggestedName, byte[] content)
    {
        var extension = Path.GetExtension(suggestedName);
        var dialog = new SaveFileDialog
        {
            FileName = suggestedName,
            DefaultExt = extension,
            Filter = extension.ToLowerInvariant() switch
            {
                ".csv" => "Archivo CSV (*.csv)|*.csv",
                ".xlsx" => "Libro de Excel (*.xlsx)|*.xlsx",
                ".zip" => "Carpeta comprimida (*.zip)|*.zip",
                ".pdf" => "Documento PDF (*.pdf)|*.pdf",
                ".jpg" or ".jpeg" or ".png" => $"Imagen (*{extension})|*{extension}",
                _ => "Todos los archivos (*.*)|*.*"
            },
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };

        // El diálogo se pone en cola en el hilo de la ventana en vez de abrirse al momento: el clic
        // de Blazor llega dentro de un evento de WebView2, y abrir ahí un diálogo modal (un bucle
        // de mensajes anidado) hace que WebView2 cierre la aplicación (0x80000003).
        var accepted = await Application.Current.Dispatcher.InvokeAsync(
            () => dialog.ShowDialog(Application.Current.MainWindow), DispatcherPriority.Background);
        if (accepted != true) return false;

        await File.WriteAllBytesAsync(dialog.FileName, content);
        return true;
    }
}
