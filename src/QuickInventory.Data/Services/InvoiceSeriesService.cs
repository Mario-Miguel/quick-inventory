using Microsoft.EntityFrameworkCore;
using QuickInventory.Core.Models;
using QuickInventory.Core.Services;

namespace QuickInventory.Data.Services;

public sealed class InvoiceSeriesService(IDbContextFactory<BaseDbContext> factory) : IInvoiceSeriesService
{
    public async Task<List<InvoiceSeriesStatus>> GetSeriesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var series = await db.InvoiceSeries.AsNoTracking().OrderByDescending(s => s.Year).ToListAsync();
        var used = await UsedCodesAsync(db);
        return [.. series.Select(s => new InvoiceSeriesStatus(s, used.Contains(s.Code)))];
    }

    /// <summary>
    /// Crea o cambia una serie de tickets. El año no se repite entre las series de tickets, y el código no se
    /// repite tampoco con las de facturas a clientes (de la librería Invoicing, misma base de datos): si
    /// coincidieran, habría dos facturas con el mismo número. Las de facturas a clientes se guardan con
    /// <see cref="Invoicing.Services.ICustomerInvoiceSeriesService"/>, que hace la comprobación inversa con
    /// <see cref="HostReservedSeriesCodes"/>.
    /// </summary>
    public async Task SaveSeriesAsync(InvoiceSeries series)
    {
        series.Normalize();
        series.Validate();

        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Mismo bloqueo que al emitir: así nadie toma número mientras se cambia la serie.
        await PostgresInvoicingLock.AcquireAsync(db);

        var used = await UsedCodesAsync(db);

        InvoiceSeries? existing = null;
        if (series.Id != 0)
        {
            existing = await db.InvoiceSeries.FindAsync(series.Id)
                ?? throw new BusinessRuleException("La serie ya no existe.");

            if (used.Contains(existing.Code))
                throw new BusinessRuleException(
                    $"La serie {existing.Code} ya tiene facturas emitidas desde la aplicación y no se puede cambiar.");
        }

        if (await db.InvoiceSeries.AnyAsync(s => s.Id != series.Id && s.Year == series.Year))
            throw new BusinessRuleException($"Ya hay una serie para {series.Year}.");
        // En las dos tablas: el código no se puede repetir aunque sea de la otra clase de serie.
        var sameCodeTickets = await db.InvoiceSeries.AnyAsync(s => s.Code == series.Code && s.Id != series.Id);
        var sameCodeCustomers = await db.CustomerInvoiceSeries.AnyAsync(s => s.Code == series.Code);
        if (sameCodeTickets || sameCodeCustomers)
            throw new BusinessRuleException($"Ya hay otra serie con el código {series.Code}.");
        // Por si se borró a mano una serie ya usada: reutilizar su código repetiría números de factura.
        if (used.Contains(series.Code))
            throw new BusinessRuleException($"El código {series.Code} ya se ha usado en facturas. Elige otro.");

        if (existing is null)
        {
            var created = new InvoiceSeries();
            created.CopyFrom(series);
            db.InvoiceSeries.Add(created);
        }
        else
        {
            existing.CopyFrom(series);
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    /// <summary>Códigos de serie con los que ya se ha emitido algo: tickets o facturas a clientes.</summary>
    private static async Task<HashSet<string>> UsedCodesAsync(BaseDbContext db)
    {
        var tickets = await db.Sales.Where(s => s.InvoiceSeries != null).Select(s => s.InvoiceSeries!).Distinct().ToListAsync();
        var invoices = await db.CustomerInvoices.Select(i => i.SeriesCode).Distinct().ToListAsync();
        return [.. tickets, .. invoices];
    }
}
