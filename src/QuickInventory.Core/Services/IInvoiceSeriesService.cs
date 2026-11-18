using QuickInventory.Core.Models;

namespace QuickInventory.Core.Services;

/// <summary>Serie y si la aplicación ya ha emitido facturas con ella (entonces no se puede cambiar).</summary>
public sealed record InvoiceSeriesStatus(InvoiceSeries Series, bool InUse);

/// <summary>Serie de facturas a clientes y si ya se han emitido facturas con ella.</summary>
public sealed record CustomerInvoiceSeriesStatus(CustomerInvoiceSeries Series, bool InUse);

public interface IInvoiceSeriesService
{
    /// <summary>Todas las series, de la más reciente a la más antigua.</summary>
    Task<List<InvoiceSeriesStatus>> GetSeriesAsync();

    /// <summary>
    /// Crea una serie o cambia una que todavía no se ha usado. Una serie con facturas emitidas
    /// desde la aplicación no se puede cambiar: dejaría huecos o números repetidos.
    /// </summary>
    Task SaveSeriesAsync(InvoiceSeries series);

    /// <summary>Todas las series de facturas a clientes, de la más reciente a la más antigua.</summary>
    Task<List<CustomerInvoiceSeriesStatus>> GetCustomerInvoiceSeriesAsync();

    /// <summary>Igual que <see cref="SaveSeriesAsync"/>, para las series de facturas a clientes.</summary>
    Task SaveCustomerInvoiceSeriesAsync(CustomerInvoiceSeries series);
}
