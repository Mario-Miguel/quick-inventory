using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using QuickInventory.Core;
using QuickInventory.Core.Models;
using QuickInventory.Core.Services;
using QuickInventory.Data.Services;

namespace QuickInventory.Data;

public static class DataConfiguration
{
    /// <summary>Registra la base de datos (PostgreSQL) y los servicios de negocio.</summary>
    public static IServiceCollection AddData(this IServiceCollection services, string connectionString)
    {
        var monitor = new ConnectionMonitor(connectionString);
        services.AddSingleton(monitor);
        services.AddSingleton<IConnectionStatus>(monitor);

        services.AddDbContextFactory<BaseDbContext>(o => o
            .UseNpgsql(connectionString)
            .AddInterceptors(new ConnectionFailureInterceptor(monitor)));
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<ISalesService, SalesService>();
        services.AddScoped<IRefundService, RefundService>();
        services.AddScoped<IOnlineTicketService, PrestaShopTicketService>();
        services.AddScoped<IIssuerService, IssuerService>();
        services.AddScoped<IInvoiceSeriesService, InvoiceSeriesService>();
        return services;
    }

    /// <summary>
    /// Comprueba que la base de datos está en la misma versión que la aplicación
    /// y empieza a vigilar la conexión.
    /// </summary>
    /// <remarks>
    /// En Debug aplica las migraciones pendientes (y crea la base si no existe) y añade
    /// productos de ejemplo la primera vez. En Release no toca el esquema: las migraciones
    /// se aplican desde desarrollo, con el usuario propietario (ver README).
    /// </remarks>
    /// <exception cref="BusinessRuleException">Con un mensaje para el usuario si no se puede usar la base.</exception>
    public static void InitDatabase(IServiceProvider services)
    {
        var factory = services.GetRequiredService<IDbContextFactory<BaseDbContext>>();
        using var db = factory.CreateDbContext();

        try
        {
#if DEBUG
            // Solo si hace falta: Migrate() siempre lanza CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory",
            // que PostgreSQL rechaza sin permiso CREATE en el esquema aunque la tabla exista. Así también se
            // puede depurar con el usuario de la app (museo_app), que no tiene ese permiso.
            if (db.Database.GetPendingMigrations().Any())
                db.Database.Migrate();
            SeedExamples(db);
#else
            CheckVersion(db);
#endif
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.InvalidPassword or PostgresErrorCodes.InvalidAuthorizationSpecification)
        {
            throw new BusinessRuleException("No se puede entrar en la base de datos: el usuario o la contraseña no son correctos.");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidCatalogName)
        {
            throw new BusinessRuleException("La base de datos no existe en el servidor.");
        }
        catch (NpgsqlException ex) when (ex is not PostgresException)
        {
            throw new BusinessRuleException("No hay conexión con el servidor de la base de datos. Comprueba la conexión a internet.");
        }

        services.GetRequiredService<ConnectionMonitor>().Start();
    }

    private static void CheckVersion(BaseDbContext db)
    {
        var known = db.Database.GetMigrations().ToHashSet();
        var applied = db.Database.GetAppliedMigrations().ToList();

        if (applied.Any(m => !known.Contains(m)))
            throw new BusinessRuleException("La base de datos es de una versión más nueva. Actualiza la aplicación.");
        if (known.Count != applied.Count)
            throw new BusinessRuleException("La base de datos no está actualizada para esta versión de la aplicación.");
    }

    /// <summary>Productos de ejemplo, solo en desarrollo y con la base vacía.</summary>
    private static void SeedExamples(BaseDbContext db)
    {
        if (db.Products.Any()) return;

        // Entradas online de ejemplo hasta que se configure la tienda de verdad (Admin → Tienda online).
        db.OnlineShopSettings.Add(new OnlineShopSettings { Url = OnlineShopSettings.DemoUrl });

        var t0 = DateTime.Now;
        var examples = new List<Product>
        {
            new TicketProduct() { Name = "Entrada general", SalePrice = 5.00m, TaxRate=TaxRate.TR2100},
            new TicketProduct() { Name = "Entrada reducida", SalePrice = 3.00m, TaxRate=TaxRate.TR2100},
            new ShopProduct() { Name = "Botella de sidra natural", SalePrice = 3.50m, CostPrice = 1.80m, Stock = 48, MinStock = 12, TaxRate=TaxRate.TR2100 },
            new ShopProduct() { Name = "Vaso de sidra", SalePrice = 4.00m, CostPrice = 1.50m, Stock = 30, MinStock = 10, TaxRate=TaxRate.TR2100 },
            new ShopProduct() { Name = "Camiseta del museo", SalePrice = 15.00m, CostPrice = 6.00m, Stock = 6, MinStock = 8, TaxRate=TaxRate.TR2100 },
            new ShopProduct() { Name = "Postal", SalePrice = 1.00m, CostPrice = 0.30m, Stock = 120, MinStock = 20, TaxRate=TaxRate.TR2100 },
        };

        foreach (var p in examples)
        {
            p.RegistrationDate = t0;
            db.Products.Add(p);
            if (p is ShopProduct { Stock: > 0 } shopProduct)
            {
                db.StockMovements.Add(new StockMovement
                {
                    Product = shopProduct,
                    Amount = shopProduct.Stock,
                    Type = StockMovementType.Input,
                    Description = "Stock inicial (datos de ejemplo)",
                    Date = t0
                });
            }
        }

        db.SaveChanges();
    }
}
