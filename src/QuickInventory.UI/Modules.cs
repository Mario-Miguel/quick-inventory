using MudBlazor;
using QuickInventory.Core.Models;

namespace QuickInventory.UI;

/// <summary>Opción de la pantalla principal y perfiles que pueden entrar en ella.</summary>
public sealed record Module(string Title, string Description, string Icon, string Route, SalesPoint[] Profiles);

public static class Modules
{
    private static readonly SalesPoint[] Sellers = [SalesPoint.TicketOffice, SalesPoint.Shop];
    private static readonly SalesPoint[] AdminOnly = [SalesPoint.Admin];

    public static readonly Module[] All =
    [
        new("Ventas", "Cobrar entradas y artículos de la tienda", Icons.Material.Filled.PointOfSale, "/sales", Sellers),
        new("Inventario", "Productos, precios y unidades en almacén", Icons.Material.Filled.Inventory2, "/inventory", AdminOnly),
        new("Ingresos", "Subvenciones, visitas concertadas y otros cobros", Icons.Material.Filled.TrendingUp, "/incomes", AdminOnly),
        new("Pagos", "Proveedores, facturas y otros gastos", Icons.Material.Filled.Payments, "/payments", AdminOnly),
    ];

    public static IEnumerable<Module> For(SalesPoint profile) => All.Where(m => m.Profiles.Contains(profile));

    /// <summary>
    /// Indica si el perfil puede estar en la ruta indicada (relativa, sin "/" inicial).
    /// La pantalla principal está permitida siempre.
    /// </summary>
    public static bool Allows(SalesPoint profile, string relativePath)
    {
        var route = "/" + relativePath.Split('?', '#')[0].Trim('/');
        var module = All.FirstOrDefault(m => string.Equals(m.Route, route, StringComparison.OrdinalIgnoreCase));
        return module is null || module.Profiles.Contains(profile);
    }
}
