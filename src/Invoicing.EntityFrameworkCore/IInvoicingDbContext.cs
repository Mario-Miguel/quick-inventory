namespace Invoicing.EntityFrameworkCore;

/// <summary>
/// Tablas de Invoicing. Lo implementa el DbContext de la aplicación anfitriona, que además llama a
/// <see cref="InvoicingModel.AddInvoicingConventions"/> y <see cref="InvoicingModel.ApplyInvoicingModel"/>.
/// Así las tablas de la librería y las de la aplicación van en la misma base y en las mismas migraciones.
/// </summary>
/// <remarks>Los nombres de las tablas salen de estas propiedades: no cambiarlos con datos ya guardados.</remarks>
public interface IInvoicingDbContext
{
    DbSet<Customer> Customers { get; }
    DbSet<CustomerInvoiceSeries> CustomerInvoiceSeries { get; }
    DbSet<CustomerInvoice> CustomerInvoices { get; }
    DbSet<CustomerInvoiceLine> CustomerInvoiceLines { get; }
    DbSet<InvoiceDesign> InvoiceDesigns { get; }
}
