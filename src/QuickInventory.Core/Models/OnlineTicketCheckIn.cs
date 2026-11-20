namespace QuickInventory.Core.Models;

/// <summary>
/// Una línea de un pedido de la tienda online (<see cref="OnlineTicket"/>) cuyas entradas ya se han usado:
/// la gente ha venido al museo. Es lo único de las entradas online que se guarda en la base de datos local;
/// el pedido sigue estando solo en PrestaShop.
/// </summary>
public class OnlineTicketCheckIn
{
    public int Id { get; set; }
    /// <summary>Id del pedido en PrestaShop.</summary>
    public int OrderId { get; set; }
    /// <summary>Id de la línea del pedido en PrestaShop (order_rows → id). Con el pedido, no se repite.</summary>
    public int OrderLineId { get; set; }
    /// <summary>Cuándo se marcó que habían venido.</summary>
    public DateTime CheckedInAt { get; set; }
}
