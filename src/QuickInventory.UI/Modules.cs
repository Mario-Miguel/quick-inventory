using MudBlazor;
using QuickInventory.Core.Models;

namespace QuickInventory.UI;

/// <summary>
/// Apartado de la pantalla principal en el que se agrupan los módulos, para que se vea a qué área pertenece cada uno.
/// </summary>
public sealed record ModuleArea(string Title, string Description, string Icon);

/// <summary>Apartados en el orden en que salen en la pantalla principal.</summary>
public static class ModuleAreas
{
    public static readonly ModuleArea Sales =
        new("Ventas y caja", "Cobrar, cerrar la caja y ver lo vendido", Icons.Material.Filled.PointOfSale);
    public static readonly ModuleArea Calendar =
        new("Calendario y reservas", "Eventos, visitas y salas", Icons.Material.Filled.CalendarMonth);
    public static readonly ModuleArea Stock =
        new("Almacén y proveedores", "Productos, mercancía que llega y facturas que nos mandan", Icons.Material.Filled.Inventory2);
    public static readonly ModuleArea Invoicing =
        new("Facturas a clientes", "Facturas para colegios y organizaciones", Icons.Material.Filled.RequestQuote);
    public static readonly ModuleArea Settings =
        new("Configuración", "Datos del museo y conexiones", Icons.Material.Filled.Settings);

    public static readonly ModuleArea[] All = [Sales, Calendar, Stock, Invoicing, Settings];
}

/// <summary>
/// Opción de la pantalla principal, apartado en el que sale y perfiles que pueden entrar en ella. Si necesita
/// un módulo opcional (<paramref name="Requires"/>), solo aparece cuando el cliente lo tiene activado.
/// </summary>
public sealed record Module(string Title, string Description, string Icon, string Route, ModuleArea Area,
    SalesPoint[] Profiles, ClientFeature? Requires = null)
{
    public bool IsAvailable(SalesPoint profile, ClientFeatures features) =>
        Profiles.Contains(profile) && (Requires is not { } feature || features.IsEnabled(feature));
}

public static class Modules
{
    private static readonly SalesPoint[] TicketOfficeOnly = [SalesPoint.TicketOffice];
    private static readonly SalesPoint[] ShopOnly = [SalesPoint.Shop];
    private static readonly SalesPoint[] AdminOnly = [SalesPoint.Admin];
    private static readonly SalesPoint[] TicketOfficeAndAdmin = [SalesPoint.TicketOffice, SalesPoint.Admin];

    // Dentro de cada apartado salen en este orden.
    public static readonly Module[] All =
    [
        // Ventas y caja
        new("Ventas", "Cobrar entradas", Icons.Material.Filled.ConfirmationNumber, "/tickets/sales", ModuleAreas.Sales, TicketOfficeOnly),
        new("Ventas", "Cobrar artículos de la tienda", Icons.Material.Filled.ShoppingCart, "/shop/sales", ModuleAreas.Sales, ShopOnly),
        new("Cerrar caja", "Comprobación de la caja", Icons.Material.Filled.PointOfSale, "/tickets/close", ModuleAreas.Sales, TicketOfficeOnly),
        new("Cerrar caja", "Comprobación de la caja", Icons.Material.Filled.PointOfSale, "/shop/close", ModuleAreas.Sales, ShopOnly),
        new("Resumen de ventas", "Ver ventas de la taquilla", Icons.Material.Filled.ListAlt, "/tickets/summary", ModuleAreas.Sales, TicketOfficeOnly),
        new("Resumen de ventas", "Ver ventas de la tienda", Icons.Material.Filled.ListAlt, "/shop/summary", ModuleAreas.Sales, ShopOnly),
        new("Exportar datos", "Exportar datos de las ventas del mes", Icons.Material.Filled.Download, "/export", ModuleAreas.Sales, AdminOnly),

        // Calendario y reservas
        new("Calendario", "Eventos, reservas de salas y de horas", Icons.Material.Filled.CalendarMonth, "/calendar", ModuleAreas.Calendar, TicketOfficeAndAdmin),
        new("Salas", "Salas que se pueden reservar para eventos", Icons.Material.Filled.MeetingRoom, "/rooms", ModuleAreas.Calendar, AdminOnly),

        // Almacén y proveedores
        new("Inventario", "Productos, precios y unidades en almacén", Icons.Material.Filled.Inventory2, "/inventory", ModuleAreas.Stock, AdminOnly),
        new("Albaranes de proveedores", "Apuntar la mercancía que llega", Icons.Material.Filled.Assignment, "/delivery-notes", ModuleAreas.Stock, AdminOnly),
        new("Facturas de proveedores", "Apuntar las facturas que nos mandan", Icons.Material.Filled.Receipt, "/purchase-invoices", ModuleAreas.Stock, AdminOnly),
        new("Proveedores", "Nombre, NIF y datos de contacto", Icons.Material.Filled.LocalShipping, "/providers", ModuleAreas.Stock, AdminOnly),

        // Facturas a clientes
        new("Facturas a clientes", "Facturas para colegios y organizaciones", Icons.Material.Filled.RequestQuote, "/customer-invoices", ModuleAreas.Invoicing, AdminOnly, ClientFeature.CustomerInvoices),
        new("Clientes", "Colegios y organizaciones a los que facturamos", Icons.Material.Filled.School, "/customers", ModuleAreas.Invoicing, AdminOnly, ClientFeature.CustomerInvoices),
        new("Diseño de factura", "Logotipo, colores y plantilla de las facturas a clientes", Icons.Material.Filled.Palette, "/invoice-design", ModuleAreas.Invoicing, AdminOnly, ClientFeature.CustomerInvoices),

        // Configuración
        new("Datos del titular", "Nombre, NIF y dirección que salen en los tickets", Icons.Material.Filled.Business, "/issuer", ModuleAreas.Settings, AdminOnly),
        new("Series de facturas", "Numeración de los tickets de cada año", Icons.Material.Filled.FormatListNumbered, "/invoice-series", ModuleAreas.Settings, AdminOnly),
        new("Tienda online", "Conexión con la tienda web (PrestaShop)", Icons.Material.Filled.Language, "/online-shop", ModuleAreas.Settings, AdminOnly, ClientFeature.PrestaShop),
    ];

    public static IEnumerable<Module> For(SalesPoint profile, ClientFeatures features) =>
        All.Where(m => m.IsAvailable(profile, features));

    /// <summary>Módulos del perfil agrupados por apartado, en orden. Los apartados sin ningún módulo no salen.</summary>
    public static IEnumerable<(ModuleArea Area, List<Module> Modules)> ByArea(SalesPoint profile, ClientFeatures features)
    {
        var available = For(profile, features).ToList();
        return ModuleAreas.All
            .Select(area => (area, available.Where(m => m.Area == area).ToList()))
            .Where(g => g.Item2.Count > 0);
    }

    /// <summary>
    /// Indica si el perfil puede estar en la ruta indicada (relativa, sin "/" inicial).
    /// La pantalla principal está permitida siempre. Las subpantallas de un módulo (p. ej.
    /// "/customer-invoices/12") siguen las reglas del módulo cuya ruta las contiene.
    /// </summary>
    public static bool Allows(SalesPoint profile, ClientFeatures features, string relativePath)
    {
        var route = "/" + relativePath.Split('?', '#')[0].Trim('/');
        var modules = All.Where(m => string.Equals(m.Route, route, StringComparison.OrdinalIgnoreCase)
            || route.StartsWith(m.Route + "/", StringComparison.OrdinalIgnoreCase)).ToList();
        // Si la misma ruta sale en varios módulos (uno por perfil), basta con que uno lo permita.
        return modules.Count == 0 || modules.Any(m => m.IsAvailable(profile, features));
    }
}
