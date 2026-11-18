using QuickInventory.Core.Models;

namespace QuickInventory.Core.Services;

/// <summary>Proveedores habituales, para elegirlos al apuntar sus facturas.</summary>
public interface IProviderService
{
    /// <summary>Proveedores ordenados por nombre. Por defecto solo los activos.</summary>
    Task<List<Provider>> GetProvidersAsync(bool includeInactive = false);

    /// <summary>
    /// Crea el proveedor (si <see cref="Provider.Id"/> es 0) o guarda sus cambios. No cambia
    /// <see cref="Provider.Active"/>. Devuelve el proveedor tal como ha quedado guardado.
    /// </summary>
    Task<Provider> SaveProviderAsync(Provider provider);

    Task SetProviderActiveAsync(int providerId, bool active);
}
