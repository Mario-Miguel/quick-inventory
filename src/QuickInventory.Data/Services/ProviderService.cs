using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuickInventory.Core;
using QuickInventory.Core.Models;
using QuickInventory.Core.Services;

namespace QuickInventory.Data.Services;

public sealed class ProviderService(IDbContextFactory<BaseDbContext> factory) : IProviderService
{
    public async Task<List<Provider>> GetProvidersAsync(bool includeInactive = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Providers
            .AsNoTracking()
            .Where(p => includeInactive || p.Active)
            .OrderBy(p => p.LegalName)
            .ToListAsync();
    }

    public async Task<Provider> SaveProviderAsync(Provider provider)
    {
        provider.Normalize();
        provider.Validate();

        await using var db = await factory.CreateDbContextAsync();

        // El índice único lo impediría igualmente, pero así el mensaje es claro.
        var other = await db.Providers
            .Where(p => p.Nif == provider.Nif && p.Id != provider.Id)
            .Select(p => new { p.LegalName, p.Active })
            .FirstOrDefaultAsync();
        if (other is not null)
            throw new BusinessRuleException(other.Active
                ? $"Ya hay un proveedor con ese NIF: {other.LegalName}."
                : $"Ya hay un proveedor con ese NIF, pero está desactivado: {other.LegalName}. Actívalo en vez de crearlo de nuevo.");

        Provider saved;
        if (provider.Id == 0)
        {
            // Nuevo: siempre empieza activo (valor por defecto de la clase).
            saved = new Provider();
            saved.CopyFrom(provider);
            db.Providers.Add(saved);
        }
        else
        {
            saved = await db.Providers.FindAsync(provider.Id)
                ?? throw new BusinessRuleException("El proveedor no existe.");
            saved.CopyFrom(provider);
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Dos equipos guardaron el mismo NIF a la vez y la comprobación de arriba no lo vio.
            throw new BusinessRuleException("Ya hay un proveedor con ese NIF.");
        }
        return saved;
    }

    public async Task SetProviderActiveAsync(int providerId, bool active)
    {
        await using var db = await factory.CreateDbContextAsync();
        var provider = await db.Providers.FindAsync(providerId)
            ?? throw new BusinessRuleException("El proveedor no existe.");

        provider.Active = active;
        await db.SaveChangesAsync();
    }
}
