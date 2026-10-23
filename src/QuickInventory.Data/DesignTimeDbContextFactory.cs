using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QuickInventory.Data;

/// <summary>
/// Contexto que usa "dotnet ef" para crear y aplicar migraciones, sin arrancar la app.
/// Se conecta a la base indicada en la variable de entorno QUICKINVENTORY_ConnectionStrings__Museo
/// (la misma que lee la app) o, si no está, al PostgreSQL local de desarrollo.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BaseDbContext>
{

    public BaseDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("QUICKINVENTORY_ConnectionStrings__Museo");
        var options = new DbContextOptionsBuilder<BaseDbContext>().UseNpgsql(connectionString).Options;
        return new BaseDbContext(options);
    }
}
