using Microsoft.EntityFrameworkCore;
using QuickInventory.Core.Models;
using QuickInventory.Core.Services;

namespace QuickInventory.Data.Services;

public sealed class IssuerService(IDbContextFactory<BaseDbContext> factory) : IIssuerService
{
    public async Task<Issuer?> GetIssuerAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Issuers.AsNoTracking().OrderBy(i => i.Id).FirstOrDefaultAsync();
    }

    public async Task SaveIssuerAsync(Issuer issuer)
    {
        issuer.Normalize();
        issuer.Validate();

        await using var db = await factory.CreateDbContextAsync();

        // Se guarda siempre en la misma fila, venga de donde venga el Id.
        var existing = await db.Issuers.OrderBy(i => i.Id).FirstOrDefaultAsync();
        if (existing is null)
        {
            db.Issuers.Add(new Issuer { Nif = issuer.Nif, LegalName = issuer.LegalName, Address = issuer.Address });
        }
        else
        {
            existing.CopyFrom(issuer);
        }

        await db.SaveChangesAsync();
    }
}
