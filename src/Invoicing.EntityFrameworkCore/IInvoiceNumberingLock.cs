namespace Invoicing.EntityFrameworkCore;

/// <summary>
/// Bloqueo mientras se numera una factura o se cambia una serie, para que dos equipos no tomen el mismo
/// número a la vez. Se llama dentro de la transacción ya abierta. La aplicación pone el suyo (p. ej. un
/// <c>pg_advisory_xact_lock</c> en PostgreSQL); el de por defecto no bloquea, y entonces lo único que impide
/// números repetidos es el índice único (serie, número): el segundo en guardar recibe un error.
/// </summary>
public interface IInvoiceNumberingLock
{
    Task AcquireAsync(DbContext db);
}

internal sealed class NoInvoiceNumberingLock : IInvoiceNumberingLock
{
    public Task AcquireAsync(DbContext db) => Task.CompletedTask;
}

internal sealed class NoReservedSeriesCodes : IReservedSeriesCodes
{
    public Task<IReadOnlySet<string>> GetAsync() => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
}
