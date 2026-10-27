namespace QuickInventory.Core.Models;

public class InvoiceSeries
{
    public int Id { get; set; }

    public string Code { get; set; } = "";
    public SalesPoint SalesPoint { get; set; }
    public int Year { get; set; } = DateTime.Now.Year;
    public int LastNumber { get; set; }
}