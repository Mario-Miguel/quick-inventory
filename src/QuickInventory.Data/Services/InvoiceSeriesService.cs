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

        var series = await db.InvoiceSeries.AsNoTracking()
            .OrderByDescending(s => s.Year)
            .ToListAsync();
        var used = await db.Sales
            .Where(s => s.InvoiceSeries != null)
            .Select(s => s.InvoiceSeries!)
            .Distinct()
            .ToListAsync();

        return [.. series.Select(s => new InvoiceSeriesStatus(s, used.Contains(s.Code)))];
    }

    public async Task SaveSeriesAsync(InvoiceSeries series)
    {
        series.Normalize();
        series.Validate();

        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Mismo bloqueo que las ventas: así nadie toma número mientras se cambia la serie.
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({AdvisoryLocks.Invoicing})");

        InvoiceSeries? existing = null;
        if (series.Id != 0)
        {
            existing = await db.InvoiceSeries.FindAsync(series.Id)
                ?? throw new BusinessRuleException("La serie ya no existe.");

            if (await db.Sales.AnyAsync(s => s.InvoiceSeries == existing.Code))
                throw new BusinessRuleException(
                    $"La serie {existing.Code} ya tiene facturas emitidas desde la aplicación y no se puede cambiar.");
        }

        if (await db.InvoiceSeries.AnyAsync(s => s.Id != series.Id && s.Year == series.Year))
            throw new BusinessRuleException($"Ya hay una serie para {series.Year}.");
        if (await db.InvoiceSeries.AnyAsync(s => s.Id != series.Id && s.Code == series.Code))
            throw new BusinessRuleException($"Ya hay otra serie con el código {series.Code}.");
        // Por si se borró a mano una serie ya usada: reutilizar su código repetiría números de factura.
        if (await db.Sales.AnyAsync(s => s.InvoiceSeries == series.Code))
            throw new BusinessRuleException($"El código {series.Code} ya se ha usado en facturas. Elige otro.");

        if (existing is null)
        {
            db.InvoiceSeries.Add(new InvoiceSeries
            {
                Code = series.Code,
                Year = series.Year,
                LastNumber = series.LastNumber,
            });
        }
        else
        {
            existing.CopyFrom(series);
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
