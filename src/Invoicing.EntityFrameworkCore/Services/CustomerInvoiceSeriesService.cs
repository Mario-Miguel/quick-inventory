namespace Invoicing.EntityFrameworkCore.Services;

internal sealed class CustomerInvoiceSeriesService<TContext>(
    IDbContextFactory<TContext> factory, IInvoiceNumberingLock numberingLock, IReservedSeriesCodes reservedCodes)
    : ICustomerInvoiceSeriesService
    where TContext : DbContext, IInvoicingDbContext
{
    public async Task<List<CustomerInvoiceSeriesStatus>> GetSeriesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var series = await db.CustomerInvoiceSeries.AsNoTracking().OrderByDescending(s => s.Year).ToListAsync();
        var used = await UsedCodesAsync(db);
        return [.. series.Select(s => new CustomerInvoiceSeriesStatus(s, used.Contains(s.Code)))];
    }

    /// <summary>
    /// Crea o cambia una serie. El año no se repite, y el código no se repite ni entre estas series ni con los
    /// códigos reservados por la aplicación (<see cref="IReservedSeriesCodes"/>): si coincidieran, habría dos
    /// facturas con el mismo número.
    /// </summary>
    public async Task SaveSeriesAsync(CustomerInvoiceSeries series)
    {
        series.Normalize();
        series.Validate();

        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Mismo bloqueo que al emitir: así nadie toma número mientras se cambia la serie.
        await numberingLock.AcquireAsync(db);

        var used = await UsedCodesAsync(db);

        CustomerInvoiceSeries? existing = null;
        if (series.Id != 0)
        {
            existing = await db.CustomerInvoiceSeries.FindAsync(series.Id)
                ?? throw new BusinessRuleException("La serie ya no existe.");

            if (used.Contains(existing.Code))
                throw new BusinessRuleException(
                    $"La serie {existing.Code} ya tiene facturas emitidas desde la aplicación y no se puede cambiar.");
        }

        if (await db.CustomerInvoiceSeries.AnyAsync(s => s.Id != series.Id && s.Year == series.Year))
            throw new BusinessRuleException($"Ya hay una serie para {series.Year}.");
        if (await db.CustomerInvoiceSeries.AnyAsync(s => s.Id != series.Id && s.Code == series.Code)
            || (await reservedCodes.GetAsync()).Contains(series.Code))
            throw new BusinessRuleException($"Ya hay otra serie con el código {series.Code}.");
        // Por si se borró a mano una serie ya usada: reutilizar su código repetiría números de factura.
        if (used.Contains(series.Code))
            throw new BusinessRuleException($"El código {series.Code} ya se ha usado en facturas. Elige otro.");

        if (existing is null)
        {
            var created = new CustomerInvoiceSeries();
            created.CopyFrom(series);
            db.CustomerInvoiceSeries.Add(created);
        }
        else
        {
            existing.CopyFrom(series);
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    /// <summary>Códigos de serie con los que ya se han emitido facturas a clientes.</summary>
    private static async Task<HashSet<string>> UsedCodesAsync(TContext db) =>
        [.. await db.CustomerInvoices.Select(i => i.SeriesCode).Distinct().ToListAsync()];
}
