using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using QuickInventory.Core.Models;

namespace QuickInventory.Data;

public class BaseDbContext(DbContextOptions<BaseDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SalesLine> SaleLines => Set<SalesLine>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configuration)
    {
        // SQLite no tiene tipo decimal: guardamos los importes como céntimos enteros.
        // Así no hay errores de redondeo y se pueden ordenar y filtrar en la base de datos.
        configuration.Properties<decimal>().HaveConversion<AmountInCentsConverter>();

        // Los enums se guardan como texto ("Cash", "Card"...) para que la base
        // de datos se entienda si alguien la abre con otra herramienta.
        configuration.Properties<PaymentMethod>().HaveConversion<string>();
        configuration.Properties<StockMovementType>().HaveConversion<string>();
        configuration.Properties<CashMovementType>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Product>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.Category).HasMaxLength(100);
            e.Property(p => p.BarCode).HasMaxLength(50);
            e.HasIndex(p => p.BarCode);
            e.HasIndex(p => p.Name);
        });

        model.Entity<StockMovement>(e =>
        {
            e.HasOne(m => m.Product).WithMany().HasForeignKey(m => m.ProductId);
            e.HasIndex(m => m.Date);
        });

        model.Entity<Sale>(e =>
        {
            e.HasMany(v => v.Lineas).WithOne().HasForeignKey(l => l.SaleId);
            e.HasIndex(v => v.Date);
        });

        model.Entity<SalesLine>(e =>
        {
            e.HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId);
        });

        model.Entity<CashMovement>(e =>
        {
            e.Property(m => m.Description).HasMaxLength(300).IsRequired();
            e.HasIndex(m => m.Date);
        });
    }
}

internal sealed class AmountInCentsConverter() : ValueConverter<decimal, long>(
    importe => (long)Math.Round(importe * 100m, MidpointRounding.AwayFromZero),
    centimos => centimos / 100m);
