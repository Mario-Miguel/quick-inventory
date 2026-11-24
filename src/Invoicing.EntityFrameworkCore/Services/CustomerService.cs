namespace Invoicing.EntityFrameworkCore.Services;

internal sealed class CustomerService<TContext>(IDbContextFactory<TContext> factory) : ICustomerService
    where TContext : DbContext, IInvoicingDbContext
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
        catch (DbUpdateException)
        {
            // Dos equipos guardaron el mismo NIF a la vez y la comprobación de arriba no lo vio: el índice
            // único lo ha impedido. Se mira de nuevo (con otro contexto) para dar un mensaje claro sin
            // depender de los códigos de error de cada base de datos.
            await using var check = await factory.CreateDbContextAsync();
            if (await check.Customers.AnyAsync(p => p.Nif == customer.Nif && p.Id != customer.Id))
                throw new BusinessRuleException("Ya hay un cliente con ese NIF.");
            throw;
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
