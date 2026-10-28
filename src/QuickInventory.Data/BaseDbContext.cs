using System.Globalization;
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
    public DbSet<InvoiceFile> InvoiceFiles => Set<InvoiceFile>();
    public DbSet<Issuer> Issuers => Set<Issuer>();
    public DbSet<InvoiceSeries> InvoiceSeries => Set<InvoiceSeries>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configuration)
    {
        // Los importes se guardan como céntimos enteros: no hay errores de redondeo
        // y no dependemos del tipo decimal de cada base de datos.
        configuration.Properties<decimal>().HaveConversion<AmountInCentsConverter>();

        // Las fechas son horas locales del museo (DateTime.Now), sin huso horario.
        // Sin esto, PostgreSQL usaría "timestamp with time zone" y exigiría fechas en UTC.
        configuration.Properties<DateTime>().HaveColumnType("timestamp without time zone");

        // Los enums se guardan como texto ("Cash", "Card"...) para que la base
        // de datos se entienda si alguien la abre con otra herramienta.
        configuration.Properties<PaymentMethod>().HaveConversion<string>();
        configuration.Properties<StockMovementType>().HaveConversion<string>();
        configuration.Properties<CashMovementType>().HaveConversion<string>();
        configuration.Properties<SalesPoint>().HaveConversion<string>();
        configuration.Properties<ProductCategory>().HaveConversion<ProductCategoryConverter>();
        configuration.Properties<InvoiceType>().HaveConversion<InvoiceTypeConverter>();
        configuration.Properties<TaxExemption>().HaveConversion<TaxExemptionConverter>();
        configuration.Properties<TaxRate>().HaveConversion<TaxRateConverter>();
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Product>(e =>
        {
            e.HasDiscriminator<string>("Type")
            .HasValue<TicketProduct>("Ticket")
            .HasValue<ShopProduct>("Shop");
            e.Ignore(p => p.Category);
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(p => p.Name);
        });

        model.Entity<ShopProduct>(e =>
        {
            e.Property(p => p.BarCode).HasMaxLength(50);
            e.HasIndex(p => p.BarCode);
        });

        model.Entity<StockMovement>(e =>
        {
            e.HasOne(m => m.Product).WithMany().HasForeignKey(m => m.ProductId);
            e.HasIndex(m => m.Date);
        });

        model.Entity<Sale>(e =>
        {
            e.Property(s => s.InvoiceSeries).HasMaxLength(20);
            e.HasDiscriminator(s => s.SalesPoint)
                .HasValue<TicketSale>(SalesPoint.TicketOffice)
                .HasValue<ShopSale>(SalesPoint.Shop);
            e.HasMany(v => v.Lines).WithOne().HasForeignKey(l => l.SaleId);
            e.HasOne<Sale>().WithMany().HasForeignKey(s => s.RectifiedSaleId);
            e.HasIndex(v => v.Date);
            e.HasIndex(s => new { s.InvoiceSeries, s.InvoiceNumber }).IsUnique();
        });

        model.Entity<SalesLine>(e =>
        {
            e.HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId);
        });

        model.Entity<CashMovement>(e =>
        {
            e.Property(m => m.Description).HasMaxLength(300).IsRequired();
            e.Property(m => m.Person).HasMaxLength(200);
            e.Property(m => m.InvoiceNumber).HasMaxLength(100);
            e.Property(m => m.InvoiceFileName).HasMaxLength(260);
            e.HasIndex(m => m.Date);
        });

        // Como mucho una factura por movimiento.
        model.Entity<InvoiceFile>(e =>
        {
            e.Property(f => f.FileName).HasMaxLength(260).IsRequired();
            e.Property(f => f.ContentType).HasMaxLength(100);
            e.HasOne<CashMovement>().WithOne().HasForeignKey<InvoiceFile>(f => f.CashMovementId);
        });

        // Una sola fila: la crea el asistente de la primera instalación.
        model.Entity<Issuer>(e =>
        {
            e.Property(i => i.Nif).HasMaxLength(9).IsRequired();
            e.Property(i => i.LegalName).HasMaxLength(120).IsRequired();
            e.Property(i => i.Address).HasMaxLength(300);
        });

        // Una serie por año, común a Taquilla y Tienda.
        model.Entity<InvoiceSeries>(e =>
        {
            e.Property(i => i.Code).HasMaxLength(Core.Models.InvoiceSeries.MaxCodeLength).IsRequired();
            e.HasIndex(s => s.Code).IsUnique();
            e.HasIndex(s => s.Year).IsUnique();
        });
    }
}

internal sealed class AmountInCentsConverter() : ValueConverter<decimal, long>(
    importe => (long)Math.Round(importe * 100m, MidpointRounding.AwayFromZero),
    centimos => centimos / 100m
);

internal sealed class ProductCategoryConverter() : ValueConverter<ProductCategory, string>(
    categoria => categoria.Value,
    valor => ProductCategory.FromValue(valor)
);

internal sealed class TaxRateConverter() : ValueConverter<TaxRate, string>(
    tipo => tipo.Value.ToString(CultureInfo.InvariantCulture),
    valor => TaxRate.FromValue(decimal.Parse(valor, CultureInfo.InvariantCulture))
);

internal sealed class TaxExemptionConverter() : ValueConverter<TaxExemption, string>(
    categoria => categoria.Value,
    valor => TaxExemption.FromValue(valor)
);

internal sealed class InvoiceTypeConverter() : ValueConverter<InvoiceType, string>(
    categoria => categoria.Value,
    valor => InvoiceType.FromValue(valor)
);

internal static class AdvisoryLocks
{
    /// <summary>
    /// Bloqueo de facturación (pg_advisory_xact_lock). Mientras una transacción lo tiene, ninguna otra
    /// puede numerar facturas ni añadir registros a la cadena VERI*FACTU. El valor es arbitrario:
    /// solo tiene que ser el mismo en todos los sitios y no repetirse con otros bloqueos. No cambiarlo.
    /// </summary>
    public const long Invoicing = 1;
}