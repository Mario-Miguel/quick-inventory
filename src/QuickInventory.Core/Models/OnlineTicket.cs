namespace QuickInventory.Core.Models;

/// <summary>
/// Entradas de una línea de un pedido de la tienda online (PrestaShop).
/// No se guardan en la base de datos local: se leen de la tienda cada vez. Solo se guarda si ya
/// han venido al museo (<see cref="OnlineTicketCheckIn"/>).
/// </summary>
public sealed class OnlineTicket
{
    public int OrderId { get; init; }
    /// <summary>Referencia del pedido en PrestaShop (la que ve el cliente), por ejemplo "XKBKNABJK".</summary>
    public string OrderReference { get; init; } = "";
    /// <summary>
    /// Nombre y apellidos de quien hizo el pedido (cliente de PrestaShop). Vacío si la clave de la API
    /// no tiene permiso para ver los clientes.
    /// </summary>
    public string ClientName { get; init; } = "";
    /// <summary>Lo que se enseña para reconocer el pedido: el nombre del cliente o, si no se sabe, la referencia.</summary>
    public string DisplayName => ClientName.Length > 0 ? ClientName : OrderReference;
    /// <summary>Cuándo se hizo el pedido (no es el día de la visita).</summary>
    public DateTime OrderDate { get; init; }
    /// <summary>Pedido pagado ("válido" en PrestaShop). Los pendientes de pago o anulados no cuentan.</summary>
    public bool Paid { get; init; }
    /// <summary>Id de la línea dentro del pedido (order_rows → id en PrestaShop).</summary>
    public int OrderLineId { get; init; }
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
    /// <summary>Cuándo se marcó que han venido al museo, o null si todavía no.</summary>
    public DateTime? CheckedInAt { get; set; }
    public bool CheckedIn => CheckedInAt is not null;
}
