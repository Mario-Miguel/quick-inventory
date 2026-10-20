namespace QuickInventory.Core.Models;

public class TicketSale : Sale
{
    public TicketSale() => SalesPoint = SalesPoint.TicketOffice;
    public bool GuidedVisit { get; set; }
    public bool GroupVisit { get; set; }


    /// <summary>Euros que se suman a cada entrada en una visita guiada (no se descuentan).</summary>
    public const decimal GuidedVisitSupplement = 1m;
    public const decimal GroupVisitDiscount = 0.5m;
}