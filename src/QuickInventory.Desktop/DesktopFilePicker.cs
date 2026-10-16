using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using QuickInventory.UI;

namespace QuickInventory.Desktop;

/// <summary>Elige archivos con el diálogo "Abrir" de Windows.</summary>
public sealed class DesktopFilePicker : IFilePicker
{
    public async Task<PickedFile?> PickAsync(string description, IReadOnlyList<string> extensions)
    {
        var patterns = string.Join(";", extensions.Select(e => "*" + e));
        var dialog = new OpenFileDialog
        {
            Filter = $"{description} ({patterns})|{patterns}",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };

        // En cola y no al momento, para no abrirlo dentro de un evento de WebView2 (ver DesktopFileSaver).
        var accepted = await Application.Current.Dispatcher.InvokeAsync(
            () => dialog.ShowDialog(Application.Current.MainWindow), DispatcherPriority.Background);
        if (accepted != true) return null;

        var path = dialog.FileName;
        return new PickedFile(Path.GetFileName(path), new FileInfo(path).Length, () => File.ReadAllBytesAsync(path));
    }
}
