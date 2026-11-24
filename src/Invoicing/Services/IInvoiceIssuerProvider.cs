namespace Invoicing.Services;

/// <summary>Datos de quien emite las facturas, tal como se copian en cada una.</summary>
public sealed record InvoiceIssuer(string Nif, string Name, string? Address);

/// <summary>
/// De dónde salen los datos del emisor. Lo implementa la aplicación anfitriona (p. ej. leyendo su tabla
/// de titular), porque la librería no guarda esos datos.
/// </summary>
public interface IInvoiceIssuerProvider
{
    /// <summary>Los datos del emisor, o null si todavía no están rellenos (entonces no se puede emitir).</summary>
    Task<InvoiceIssuer?> GetIssuerAsync();
}
