namespace QuickInventory.Core.Models;

/// <summary>
/// Perfil desde el que se trabaja. Taquilla y Tienda solo venden (cada una sus productos,
/// según la categoría) y cada venta queda apuntada al perfil que la hizo, para llevar
/// las cuentas por separado. Admin gestiona el resto: inventario, ingresos y pagos.
/// </summary>
public enum SalesPoint
{
    /// <summary>Taquilla: solo vende entradas.</summary>
    TicketOffice,

    /// <summary>Tienda: vende todo lo que no son entradas.</summary>
    Shop,

    /// <summary>Administración: inventario, ingresos y pagos. No vende.</summary>
    Admin
}
