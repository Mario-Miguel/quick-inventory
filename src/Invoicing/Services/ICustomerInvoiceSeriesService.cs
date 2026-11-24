using Invoicing.Models;

namespace Invoicing.Services;

/// <summary>Serie de facturas a clientes y si ya se han emitido facturas con ella (entonces no se puede cambiar).</summary>
public sealed record CustomerInvoiceSeriesStatus(CustomerInvoiceSeries Series, bool InUse);

/// <summary>Series de numeración de las facturas a clientes, una por año.</summary>
public interface ICustomerInvoiceSeriesService
{
    /// <summary>Todas las series, de la más reciente a la más antigua.</summary>
    Task<List<CustomerInvoiceSeriesStatus>> GetSeriesAsync();

    /// <summary>
    /// Crea una serie o cambia una que todavía no se ha usado. Una serie con facturas emitidas no se puede
    /// cambiar: dejaría huecos o números repetidos. El código no puede coincidir con ninguno de
    /// <see cref="IReservedSeriesCodes"/> (p. ej. las series de tickets de la aplicación anfitriona).
    /// </summary>
    Task SaveSeriesAsync(CustomerInvoiceSeries series);
}
