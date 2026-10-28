using MudBlazor;
using QuickInventory.Core.Models;

namespace QuickInventory.UI;

/// <summary>
/// Opción de la pantalla principal y perfiles que pueden entrar en ella. Si necesita un módulo
/// opcional (<paramref name="Requires"/>), solo aparece cuando el cliente lo tiene activado.
/// </summary>
public sealed record Module(string Title, string Description, string Icon, string Route, SalesPoint[] Profiles,
    ClientFeature? Requires = null)
{
    public bool IsAvailable(SalesPoint profile, ClientFeatures features) =>
        Profiles.Contains(profile) && (Requires is not { } feature || features.IsEnabled(feature));
}

public static class Modules
{
    private static readonly SalesPoint[] TicketOfficeOnly = [SalesPoint.TicketOffice];
    private static readonly SalesPoint[] ShopOnly = [SalesPoint.Shop];
    private static readonly SalesPoint[] AdminOnly = [SalesPoint.Admin];

    public static readonly Module[] All =
    [
        new("Ventas", "Cobrar entradas", Icons.Material.Filled.ConfirmationNumber, "/tickets/sales", TicketOfficeOnly),
        new("Ventas", "Cobrar artículos de la tienda", Icons.Material.Filled.ShoppingCart, "/shop/sales", ShopOnly),
        new("Cerrar caja", "Comprobación de la caja", Icons.Material.Filled.PointOfSale, "/tickets/close", TicketOfficeOnly),
        new("Cerrar caja", "Comprobación de la caja", Icons.Material.Filled.PointOfSale, "/shop/close", ShopOnly),
        new("Resumen de ventas", "Ver ventas de la taquilla", Icons.Material.Filled.ListAlt, "/tickets/summary", TicketOfficeOnly),
        new("Entradas online", "Entradas compradas en la web, por día y hora", Icons.Material.Filled.CalendarMonth, "/tickets/online", TicketOfficeOnly, ClientFeature.PrestaShop),
        new("Resumen de ventas", "Ver ventas de la tienda", Icons.Material.Filled.ListAlt, "/shop/summary", ShopOnly),
        new("Inventario", "Productos, precios y unidades en almacén", Icons.Material.Filled.Inventory2, "/inventory", AdminOnly),
        new("Reembolsos", "Devolver a un trabajador lo que pagó con factura", Icons.Material.Filled.ReceiptLong, "/refunds", AdminOnly),
        new("Exportar datos", "Exportar datos de las ventas del mes", Icons.Material.Filled.Download, "/export", AdminOnly),
        new("Tienda online", "Conexión con la tienda web (PrestaShop)", Icons.Material.Filled.Language, "/online-shop", AdminOnly, ClientFeature.PrestaShop),
        new("Datos del titular", "Nombre, NIF y dirección que salen en los tickets", Icons.Material.Filled.Business, "/issuer", AdminOnly),
        new("Series de facturas", "Numeración de los tickets de cada año", Icons.Material.Filled.FormatListNumbered, "/invoice-series", AdminOnly),
    ];

    public static IEnumerable<Module> For(SalesPoint profile, ClientFeatures features) =>
        All.Where(m => m.IsAvailable(profile, features));

    /// <summary>
    /// Indica si el perfil puede estar en la ruta indicada (relativa, sin "/" inicial).
    /// La pantalla principal está permitida siempre.
    /// </summary>
    public static bool Allows(SalesPoint profile, ClientFeatures features, string relativePath)
    {
        var route = "/" + relativePath.Split('?', '#')[0].Trim('/');
        var module = All.FirstOrDefault(m => string.Equals(m.Route, route, StringComparison.OrdinalIgnoreCase));
        return module is null || module.IsAvailable(profile, features);
    }
}
