using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuickInventory.Core.Models;
using QuickInventory.Core.Services;
using QuickInventory.Data.Services;

namespace QuickInventory.Data;

public static class DataConfiguration
{
    /// <summary>Registra la base de datos y los servicios de negocio.</summary>
    public static IServiceCollection AddData(this IServiceCollection services, string databasePath)
    {
        services.AddDbContextFactory<BaseDbContext>(o => o.UseSqlite($"Data Source={databasePath}"));
        services.AddScoped<IInventoryService, InventoryService>();
        return services;
    }

    /// <summary>
    /// Crea la base de datos si no existe y añade productos de ejemplo la primera vez.
    /// </summary>
    /// <remarks>
    /// EnsureCreated sirve para empezar. Antes de instalarlo en el museo conviene pasar
    /// a migraciones (dotnet ef migrations add Inicial) para poder cambiar el esquema
    /// sin perder datos. Está explicado en el README.
    /// </remarks>
    public static void InitDatabase(IServiceProvider services)
    {
        var factory = services.GetRequiredService<IDbContextFactory<BaseDbContext>>();
        using var db = factory.CreateDbContext();

        db.Database.EnsureCreated();

        if (db.Products.Any()) return;

        var t0 = DateTime.Now;
        var examples = new List<Product>
        {
            new() { Name = "Input general", Category = "Entradas", SalePrice = 5.00m, StockControl = false },
            new() { Name = "Input reducida", Category = "Entradas", SalePrice = 3.00m, StockControl = false },
            new() { Name = "Botella de sidra natural", Category = "Tienda", SalePrice = 3.50m, CostPrice = 1.80m, Stock = 48, MinStock = 12 },
            new() { Name = "Vaso de sidra", Category = "Tienda", SalePrice = 4.00m, CostPrice = 1.50m, Stock = 30, MinStock = 10 },
            new() { Name = "Camiseta del museo", Category = "Tienda", SalePrice = 15.00m, CostPrice = 6.00m, Stock = 6, MinStock = 8 },
            new() { Name = "Postal", Category = "Tienda", SalePrice = 1.00m, CostPrice = 0.30m, Stock = 120, MinStock = 20 },
        };

        foreach (var p in examples)
        {
            p.RegistrationDate = t0;
            db.Products.Add(p);
            if (p.StockControl && p.Stock > 0)
            {
                db.StockMovements.Add(new StockMovement
                {
                    Product = p,
                    Amount = p.Stock,
                    Type = StockMovementType.Input,
                    Description = "Stock inicial (datos de ejemplo)",
                    Date = t0
                });
            }
        }

        db.SaveChanges();
    }
}
