using QuickInventory.Core.Models;

namespace QuickInventory.Core.Services;

/// <summary>Clientes habituales (colegios, asociaciones...), para elegirlos al emitirles facturas.</summary>
public interface ICustomerService
{
    /// <summary>Clientes ordenados por nombre. Por defecto solo los activos.</summary>
    Task<List<Customer>> GetCustomersAsync(bool includeInactive = false);

    /// <summary>
    /// Crea el cliente (si <see cref="Contact.Id"/> es 0) o guarda sus cambios. No cambia
    /// <see cref="Contact.Active"/>. Las facturas ya emitidas no cambian: llevan copiados sus datos.
    /// </summary>
    Task<Customer> SaveCustomerAsync(Customer customer);

    Task SetCustomerActiveAsync(int customerId, bool active);
}
