using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuickInventory.Core;
using QuickInventory.Core.Models;
using QuickInventory.Core.Services;

namespace QuickInventory.Data.Services;

public sealed class CustomerService(IDbContextFactory<BaseDbContext> factory) : ICustomerService
{
    public async Task<List<Customer>> GetCustomersAsync(bool includeInactive = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Customers
            .AsNoTracking()
            .Where(p => includeInactive || p.Active)
            .OrderBy(p => p.LegalName)
            .ToListAsync();
    }

    public async Task<Customer> SaveCustomerAsync(Customer customer)
    {
        customer.Normalize();
        customer.Validate();

        await using var db = await factory.CreateDbContextAsync();

        // El índice único lo impediría igualmente, pero así el mensaje es claro.
        var other = await db.Customers
            .Where(p => p.Nif == customer.Nif && p.Id != customer.Id)
            .Select(p => new { p.LegalName, p.Active })
            .FirstOrDefaultAsync();
        if (other is not null)
            throw new BusinessRuleException(other.Active
                ? $"Ya hay un cliente con ese NIF: {other.LegalName}."
                : $"Ya hay un cliente con ese NIF, pero está desactivado: {other.LegalName}. Actívalo en vez de crearlo de nuevo.");

        Customer saved;
        if (customer.Id == 0)
        {
            // Nuevo: siempre empieza activo (valor por defecto de la clase).
            saved = new Customer();
            saved.CopyFrom(customer);
            db.Customers.Add(saved);
        }
        else
        {
            saved = await db.Customers.FindAsync(customer.Id)
                ?? throw new BusinessRuleException("El cliente no existe.");
            saved.CopyFrom(customer);
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Dos equipos guardaron el mismo NIF a la vez y la comprobación de arriba no lo vio.
            throw new BusinessRuleException("Ya hay un cliente con ese NIF.");
        }
        return saved;
    }

    public async Task SetCustomerActiveAsync(int customerId, bool active)
    {
        await using var db = await factory.CreateDbContextAsync();
        var customer = await db.Customers.FindAsync(customerId)
            ?? throw new BusinessRuleException("El cliente no existe.");

        customer.Active = active;
        await db.SaveChangesAsync();
    }
}
