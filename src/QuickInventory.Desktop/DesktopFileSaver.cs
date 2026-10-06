using System.IO;
using System.Windows;
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
                _ => "Todos los archivos (*.*)|*.*"
            },
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };

        // El diálogo tiene que abrirse en el hilo de la ventana.
        var accepted = Application.Current.Dispatcher.Invoke(() => dialog.ShowDialog(Application.Current.MainWindow));
        if (accepted != true) return false;

        await File.WriteAllBytesAsync(dialog.FileName, content);
        return true;
    }
}
