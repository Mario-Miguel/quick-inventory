using QuickInventory.Core.Models;

namespace QuickInventory.Core.Services;

public interface IIssuerService
{
    /// <summary>Datos del titular, o null si todavía no se ha hecho la primera instalación.</summary>
    Task<Issuer?> GetIssuerAsync();

    /// <summary>Guarda los datos del titular (los crea la primera vez).</summary>
    Task SaveIssuerAsync(Issuer issuer);
}
