using Microsoft.EntityFrameworkCore;
using QuickInventory.Core;
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

    public async Task<List<CustomerInvoiceSeriesStatus>> GetCustomerInvoiceSeriesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var series = await db.CustomerInvoiceSeries.AsNoTracking().OrderByDescending(s => s.Year).ToListAsync();
        var used = await UsedCodesAsync(db);
        return [.. series.Select(s => new CustomerInvoiceSeriesStatus(s, used.Contains(s.Code)))];
    }

    public Task SaveSeriesAsync(InvoiceSeries series) =>
        SaveAsync(series, db => db.InvoiceSeries, () => new InvoiceSeries());

    public Task SaveCustomerInvoiceSeriesAsync(CustomerInvoiceSeries series) =>
        SaveAsync(series, db => db.CustomerInvoiceSeries, () => new CustomerInvoiceSeries());

    /// <summary>
    /// Crea o cambia una serie de <paramref name="table"/>. El año no se repite dentro de la misma tabla,
    /// y el código no se repite en ninguna de las dos (tickets y facturas a clientes): si coincidieran,
    /// habría dos facturas con el mismo número.
    /// </summary>
    private async Task SaveAsync<TSeries>(TSeries series, Func<BaseDbContext, DbSet<TSeries>> table, Func<TSeries> create)
        where TSeries : NumberingSeries
    {
        series.Normalize();
        series.Validate();

        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Mismo bloqueo que al emitir: así nadie toma número mientras se cambia la serie.
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({AdvisoryLocks.Invoicing})");

        var used = await UsedCodesAsync(db);

        TSeries? existing = null;
        if (series.Id != 0)
        {
            existing = await table(db).FindAsync(series.Id)
                ?? throw new BusinessRuleException("La serie ya no existe.");

            if (used.Contains(existing.Code))
                throw new BusinessRuleException(
                    $"La serie {existing.Code} ya tiene facturas emitidas desde la aplicación y no se puede cambiar.");
        }

        if (await table(db).AnyAsync(s => s.Id != series.Id && s.Year == series.Year))
            throw new BusinessRuleException($"Ya hay una serie para {series.Year}.");
        // En las dos tablas: el código no se puede repetir aunque sea de la otra clase de serie.
        // En su propia tabla se excluye ella misma (por Id); en la otra, cualquier fila cuenta.
        var ticketsId = series is InvoiceSeries ? series.Id : 0;
        var customersId = series is CustomerInvoiceSeries ? series.Id : 0;
        var sameCodeTickets = await db.InvoiceSeries.AnyAsync(s => s.Code == series.Code && s.Id != ticketsId);
        var sameCodeCustomers = await db.CustomerInvoiceSeries.AnyAsync(s => s.Code == series.Code && s.Id != customersId);
        if (sameCodeTickets || sameCodeCustomers)
            throw new BusinessRuleException($"Ya hay otra serie con el código {series.Code}.");
        // Por si se borró a mano una serie ya usada: reutilizar su código repetiría números de factura.
        if (used.Contains(series.Code))
            throw new BusinessRuleException($"El código {series.Code} ya se ha usado en facturas. Elige otro.");

        if (existing is null)
        {
            var created = create();
            created.CopyFrom(series);
            table(db).Add(created);
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
