namespace QuickInventory.Core.Models;

/// <summary>
/// Entradas de una línea de un pedido de la tienda online (PrestaShop).
/// No se guardan en la base de datos local: se leen de la tienda cada vez.
/// </summary>
public sealed class OnlineTicket
{
    public int OrderId { get; init; }
    /// <summary>Referencia del pedido en PrestaShop (la que ve el cliente), por ejemplo "XKBKNABJK".</summary>
    public string OrderReference { get; init; } = "";
    /// <summary>Cuándo se hizo el pedido (no es el día de la visita).</summary>
    public DateTime OrderDate { get; init; }
    /// <summary>Pedido pagado ("válido" en PrestaShop). Los pendientes de pago o anulados no cuentan.</summary>
    public bool Paid { get; init; }
    public int ProductId { get; init; }
    /// <summary>
    /// Nombre de la línea tal como lo guarda PrestaShop, con las combinaciones elegidas:
    /// "Entrada general - Fecha: 25/10/2026, Hora: 11:00".
    /// </summary>
    public string ProductName { get; init; } = "";
    /// <summary>Tipo de entrada: el nombre del producto y las combinaciones que no son la fecha ni la hora.</summary>
    public string Type { get; init; } = "";
    /// <summary>Día de la visita, o null si no se ha encontrado en <see cref="ProductName"/>.</summary>
    public DateOnly? VisitDay { get; init; }
    /// <summary>Hora de la visita, o null si no se ha encontrado en <see cref="ProductName"/>.</summary>
    public TimeOnly? VisitTime { get; init; }
    public int Quantity { get; init; }
}
