using Microsoft.Extensions.DependencyInjection;

namespace Invoicing.UI;

/// <summary>Opciones de las pantallas de Invoicing.</summary>
public sealed class InvoicingUiOptions
{
    /// <summary>
    /// Color de títulos y franjas de la factura cuando el diseño no tiene uno propio ("#RRGGBB"); normalmente
    /// el color principal de la aplicación.
    /// </summary>
    public string DefaultAccentColor { get; set; } = "#000000";
}

/// <summary>
/// Punto de entrada de las pantallas de Invoicing. Rutas: <c>/customers</c>, <c>/customer-invoices</c>,
/// <c>/customer-invoices/{id}</c> y <c>/invoice-design</c>; las series se muestran con
/// <see cref="Components.CustomerInvoiceSeriesPanel"/> dentro de una pantalla de la aplicación.
/// </summary>
/// <remarks>
/// La aplicación anfitriona tiene que:
/// <list type="bullet">
/// <item>añadir <c>typeof(InvoicingUi).Assembly</c> a <c>AdditionalAssemblies</c> de su <c>Router</c>;</item>
/// <item>enlazar <c>_content/Invoicing.UI/invoicing.css</c> y tener MudBlazor configurado;</item>
/// <item>registrar <see cref="IFilePicker"/>, <see cref="IFileSaver"/> y <see cref="IPagePrinter"/>, los
/// servicios de Invoicing (p. ej. con <c>AddInvoicingData</c>) y llamar a <see cref="AddInvoicingUi"/>.</item>
/// </list>
/// </remarks>
public static class InvoicingUi
{
    public static IServiceCollection AddInvoicingUi(this IServiceCollection services, Action<InvoicingUiOptions>? configure = null)
    {
        var options = new InvoicingUiOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        return services;
    }
}
