using MudBlazor;
using QuickInventory.Core.Models;

namespace QuickInventory.UI;

/// <summary>
/// Perfil con el que se está trabajando ahora (Taquilla, Tienda o Admin).
/// No hay contraseñas: se cambia desde la barra superior. Cada perfil solo ve
/// sus opciones de la pantalla principal (ver <see cref="Modules"/>).
/// </summary>
public sealed class ActiveProfile
{
    public SalesPoint Current { get; private set; } = SalesPoint.TicketOffice;

    /// <summary>Avisa a las pantallas abiertas para que recarguen sus datos.</summary>
    public event Action? Changed;

    public void Change(SalesPoint salesPoint)
    {
        if (salesPoint == Current) return;
        Current = salesPoint;
        Changed?.Invoke();
    }

    public static string Name(SalesPoint salesPoint) => salesPoint switch
    {
        SalesPoint.TicketOffice => "Taquilla",
        SalesPoint.Shop => "Tienda",
        SalesPoint.Admin => "Admin",
        _ => salesPoint.ToString()
    };

    public static string Icon(SalesPoint salesPoint) => salesPoint switch
    {
        SalesPoint.TicketOffice => Icons.Material.Filled.ConfirmationNumber,
        SalesPoint.Admin => Icons.Material.Filled.AdminPanelSettings,
        _ => Icons.Material.Filled.Storefront
    };
}
