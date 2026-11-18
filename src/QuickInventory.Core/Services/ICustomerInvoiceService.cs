using QuickInventory.Core.Models;

namespace QuickInventory.Core.Services;

/// <summary>Facturas completas que el museo emite a sus clientes.</summary>
public interface ICustomerInvoiceService
{
    /// <summary>Facturas con fecha entre dos días (el último sin incluir), con sus líneas.</summary>
    Task<List<CustomerInvoice>> GetInvoicesAsync(DateTime from, DateTime to);

    /// <summary>Una factura con sus líneas, o null si no existe.</summary>
    Task<CustomerInvoice?> GetInvoiceAsync(int invoiceId);

    /// <summary>
    /// Emite una factura. De <paramref name="invoice"/> se toman el cliente, la fecha, las líneas y las
    /// notas. El servicio le da el número siguiente de la <see cref="CustomerInvoiceSeries"/> del año de la
    /// factura y copia los datos del titular y del cliente. La fecha no puede ser anterior a la de la última
    /// factura de la serie (la numeración tiene que seguir el orden de las fechas).
    /// </summary>
    Task<CustomerInvoice> IssueInvoiceAsync(CustomerInvoice invoice);
}
