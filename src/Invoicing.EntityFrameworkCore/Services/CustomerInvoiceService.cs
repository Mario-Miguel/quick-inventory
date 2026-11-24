namespace Invoicing.EntityFrameworkCore.Services;

internal sealed class CustomerInvoiceService<TContext>(
    IDbContextFactory<TContext> factory, IInvoiceIssuerProvider issuerProvider, IInvoiceNumberingLock numberingLock)
    : ICustomerInvoiceService
    where TContext : DbContext, IInvoicingDbContext
{
    public async Task<List<CustomerInvoice>> GetInvoicesAsync(DateTime from, DateTime to)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.CustomerInvoices
            .AsNoTracking()
            .Include(i => i.Lines.OrderBy(l => l.Id))
            .Where(i => i.InvoiceDate >= from && i.InvoiceDate < to)
            .OrderBy(i => i.InvoiceDate)
            .ThenBy(i => i.Number)
            .ToListAsync();
    }

    public async Task<CustomerInvoice?> GetInvoiceAsync(int invoiceId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.CustomerInvoices
            .AsNoTracking()
            .Include(i => i.Lines.OrderBy(l => l.Id))
            .FirstOrDefaultAsync(i => i.Id == invoiceId);
    }

    public async Task<CustomerInvoice> IssueInvoiceAsync(CustomerInvoice invoice)
    {
        invoice.Normalize();
        invoice.Validate();

        await using var db = await factory.CreateDbContextAsync();

        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == invoice.CustomerId)
            ?? throw new BusinessRuleException("El cliente ya no existe.");
        if (!customer.Active)
            throw new BusinessRuleException($"El cliente {customer.LegalName} está desactivado.");
        // Los clientes antiguos o creados a mano podrían no tenerla, y en una factura completa es obligatoria.
        if (customer.Address is null)
            throw new BusinessRuleException($"Falta la dirección de {customer.LegalName}. Añádela en Clientes antes de emitir la factura.");

        var issuer = await issuerProvider.GetIssuerAsync()
            ?? throw new BusinessRuleException("Faltan los datos del titular. Rellénalos en «Datos del titular».");

        var saved = new CustomerInvoice
        {
            InvoiceType = InvoiceType.F1,
            InvoiceDate = invoice.InvoiceDate,
            CustomerId = customer.Id,
            CustomerNif = customer.Nif,
            CustomerName = customer.LegalName,
            CustomerAddress = customer.Address,
            IssuerNif = issuer.Nif,
            IssuerName = issuer.Name,
            IssuerAddress = issuer.Address,
            Notes = invoice.Notes,
            RegistrationDate = DateTime.Now,
            Lines = [.. invoice.Lines.Select(l => new CustomerInvoiceLine
            {
                Description = l.Description,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                TaxRate = l.TaxRate,
                TaxExemption = l.TaxExemption,
            })],
        };
        saved.CalculateTotals();

        await using var transaction = await db.Database.BeginTransactionAsync();
        // Mientras se numera, nadie más toma número ni cambia las series (ver IInvoiceNumberingLock).
        await numberingLock.AcquireAsync(db);

        var year = saved.InvoiceDate.Year;
        var series = await db.CustomerInvoiceSeries.SingleOrDefaultAsync(s => s.Year == year)
            ?? throw new BusinessRuleException($"No hay serie de facturas a clientes para {year}. Créala en «Series de facturas».");

        // Los números tienen que ir en el orden de las fechas: no se puede emitir con fecha anterior a la última.
        var last = await db.CustomerInvoices
            .Where(i => i.SeriesCode == series.Code)
            .OrderByDescending(i => i.Number)
            .Select(i => new { i.InvoiceDate, i.Number })
            .FirstOrDefaultAsync();
        if (last is not null && saved.InvoiceDate < last.InvoiceDate)
            throw new BusinessRuleException(
                $"La fecha no puede ser anterior a la de la última factura ({NumberingSeries.FormatInvoiceId(series.Code, last.Number)}, " +
                $"del {last.InvoiceDate:dd/MM/yyyy}).");

        series.LastNumber++;
        saved.SeriesCode = series.Code;
        saved.Number = series.LastNumber;

        db.CustomerInvoices.Add(saved);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return saved;
    }
}
