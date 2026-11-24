using Invoicing.EntityFrameworkCore;
using Invoicing.Services;
using Microsoft.EntityFrameworkCore;

namespace QuickInventory.Data.Services;

// Lo que la librería Invoicing (facturas a clientes) necesita de esta aplicación.

/// <summary>El emisor de las facturas a clientes es el titular (<see cref="BaseDbContext.Issuers"/>).</summary>
internal sealed class IssuerProvider(IDbContextFactory<BaseDbContext> factory) : IInvoiceIssuerProvider
{
    public async Task<InvoiceIssuer?> GetIssuerAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var issuer = await db.Issuers.AsNoTracking().FirstOrDefaultAsync();
        return issuer is null ? null : new InvoiceIssuer(issuer.Nif, issuer.LegalName, issuer.Address);
    }
}

/// <summary>
/// Las facturas a clientes se numeran con el mismo bloqueo que los tickets (<see cref="AdvisoryLocks.Invoicing"/>):
/// mientras se numera, nadie más toma número ni cambia las series.
/// </summary>
internal sealed class PostgresInvoicingLock : IInvoiceNumberingLock
{
    Task IInvoiceNumberingLock.AcquireAsync(DbContext db) => AcquireAsync(db);

    public static Task AcquireAsync(DbContext db) =>
        db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({AdvisoryLocks.Invoicing})");
}

/// <summary>
/// Códigos que una serie de facturas a clientes no puede usar: los de las series de tickets y los que ya
/// llevan tickets emitidos (aunque su serie se haya borrado a mano).
/// </summary>
internal sealed class HostReservedSeriesCodes(IDbContextFactory<BaseDbContext> factory) : IReservedSeriesCodes
{
    public async Task<IReadOnlySet<string>> GetAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var series = await db.InvoiceSeries.Select(s => s.Code).ToListAsync();
        var used = await db.Sales.Where(s => s.InvoiceSeries != null).Select(s => s.InvoiceSeries!).Distinct().ToListAsync();
        return new HashSet<string>([.. series, .. used]);
    }
}
