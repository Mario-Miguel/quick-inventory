using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Invoicing.UI;

namespace QuickInventory.Desktop;

/// <summary>
/// Imprime con las funciones de WebView2 en vez de con <c>window.print()</c>: este abre la vista previa
/// de Edge dentro de la propia ventana, y al cerrarla la aplicación se puede quedar en negro.
/// </summary>
public sealed class DesktopPagePrinter : IPagePrinter
{
    public Task PrintAsync() => OnWindowThread(async web =>
    {
        // Diálogo de imprimir de Windows, en su propia ventana. Desde él también se puede elegir
        // «Microsoft Print to PDF».
        web.ShowPrintUI(CoreWebView2PrintDialogKind.System);
        await Task.CompletedTask;
        return true;
    });

    public Task<byte[]> ToPdfAsync() => OnWindowThread(async web =>
    {
        var settings = web.Environment.CreatePrintSettings();
        settings.ShouldPrintHeaderAndFooter = false; // sin la fecha ni la dirección interna de la página
        settings.ShouldPrintBackgrounds = true;
        settings.MarginTop = settings.MarginBottom = settings.MarginLeft = settings.MarginRight = 0; // los pone la propia factura

        using var pdf = await web.PrintToPdfStreamAsync(settings);
        using var copy = new MemoryStream();
        await pdf.CopyToAsync(copy);
        return copy.ToArray();
    });

    /// <summary>
    /// Ejecuta <paramref name="action"/> en el hilo de la ventana, en cola y no al momento: la llamada llega
    /// desde un evento de WebView2, y meterse ahí en otra operación de WebView2 puede cerrar la aplicación
    /// (ver <see cref="DesktopFileSaver"/>).
    /// </summary>
    private static async Task<T> OnWindowThread<T>(Func<CoreWebView2, Task<T>> action)
    {
        var task = await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var window = (MainWindow)Application.Current.MainWindow;
            var web = window.WebView.WebView.CoreWebView2
                ?? throw new InvalidOperationException("La ventana todavía no está lista para imprimir.");
            return action(web);
        }, DispatcherPriority.Background);
        return await task;
    }
}
