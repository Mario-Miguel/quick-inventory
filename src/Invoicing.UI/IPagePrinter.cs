namespace Invoicing.UI;

/// <summary>
/// Imprime la pantalla actual (con los estilos de <c>@media print</c>: en una factura, solo la factura).
/// Cada anfitrión lo implementa a su manera: la app de escritorio usa las funciones de impresión de
/// WebView2, y la futura versión web usará las del navegador.
/// </summary>
public interface IPagePrinter
{
    /// <summary>Abre el diálogo de imprimir del sistema. No espera a que se imprima.</summary>
    Task PrintAsync();

    /// <summary>Genera un PDF de la pantalla actual, sin mostrar nada.</summary>
    Task<byte[]> ToPdfAsync();
}
