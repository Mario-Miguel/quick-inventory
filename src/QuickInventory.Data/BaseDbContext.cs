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
    public DbSet<OnlineShopSettings> OnlineShopSettings => Set<OnlineShopSettings>();
    public DbSet<Issuer> Issuers => Set<Issuer>();
    public DbSet<InvoiceSeries> InvoiceSeries => Set<InvoiceSeries>();
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceLine> PurchaseInvoiceLines => Set<PurchaseInvoiceLine>();
    public DbSet<DeliveryNote> DeliveryNotes => Set<DeliveryNote>();
    public DbSet<DeliveryNoteLine> DeliveryNoteLines => Set<DeliveryNoteLine>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerInvoiceSeries> CustomerInvoiceSeries => Set<CustomerInvoiceSeries>();
    public DbSet<CustomerInvoice> CustomerInvoices => Set<CustomerInvoice>();
    public DbSet<CustomerInvoiceLine> CustomerInvoiceLines => Set<CustomerInvoiceLine>();
    public DbSet<InvoiceDesign> InvoiceDesigns => Set<InvoiceDesign>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();

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
        configuration.Properties<InvoiceLayout>().HaveConversion<string>();
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
            e.HasOne<PurchaseInvoice>().WithMany().HasForeignKey(m => m.PurchaseInvoiceId);
            e.HasOne<DeliveryNote>().WithMany().HasForeignKey(m => m.DeliveryNoteId);
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

        // Como mucho un archivo por movimiento, factura o albarán de proveedor, y siempre de uno solo.
        model.Entity<InvoiceFile>(e =>
        {
            e.Property(f => f.FileName).HasMaxLength(260).IsRequired();
            e.Property(f => f.ContentType).HasMaxLength(100);
            e.HasOne<CashMovement>().WithOne().HasForeignKey<InvoiceFile>(f => f.CashMovementId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<PurchaseInvoice>().WithOne().HasForeignKey<InvoiceFile>(f => f.PurchaseInvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<DeliveryNote>().WithOne().HasForeignKey<InvoiceFile>(f => f.DeliveryNoteId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t => t.HasCheckConstraint("CK_InvoiceFiles_UnSoloDueno",
                "num_nonnulls(\"CashMovementId\", \"PurchaseInvoiceId\", \"DeliveryNoteId\") = 1"));
        });

        model.Entity<OnlineShopSettings>(e =>
        {
            e.Property(s => s.Url).HasMaxLength(300);
            e.Property(s => s.ApiKey).HasMaxLength(100);
            e.Property(s => s.TicketProductIds).HasMaxLength(500);
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

        // Una serie por año para las facturas a clientes, aparte de la de los tickets. Tabla propia (y no
        // una columna "tipo" en InvoiceSeries) para que las versiones anteriores de la app, que buscan la
        // serie de los tickets solo por el año, no encuentren dos.
        model.Entity<CustomerInvoiceSeries>(e =>
        {
            e.Property(i => i.Code).HasMaxLength(NumberingSeries.MaxCodeLength).IsRequired();
            e.HasIndex(s => s.Code).IsUnique();
            e.HasIndex(s => s.Year).IsUnique();
        });

        model.Entity<Provider>(e =>
        {
            e.Property(i => i.Nif).HasMaxLength(9);
            e.Property(i => i.LegalName).HasMaxLength(Provider.MaxLegalNameLength).IsRequired();
            e.Property(i => i.Address).HasMaxLength(Provider.MaxAddressLength);
            e.Property(i => i.Email).HasMaxLength(Provider.MaxEmailLength);
            e.Property(i => i.PhoneNumber).HasMaxLength(Provider.MaxPhoneNumberLength);
            e.Property(i => i.Notes).HasMaxLength(Provider.MaxNotesLength);
            e.HasIndex(i => i.Nif).IsUnique();
        });

        model.Entity<Customer>(e =>
        {
            e.Property(i => i.Nif).HasMaxLength(9);
            e.Property(i => i.LegalName).HasMaxLength(Contact.MaxLegalNameLength).IsRequired();
            e.Property(i => i.Address).HasMaxLength(Contact.MaxAddressLength);
            e.Property(i => i.Email).HasMaxLength(Contact.MaxEmailLength);
            e.Property(i => i.PhoneNumber).HasMaxLength(Contact.MaxPhoneNumberLength);
            e.Property(i => i.Notes).HasMaxLength(Contact.MaxNotesLength);
            e.HasIndex(i => i.Nif).IsUnique();
        });

        model.Entity<CustomerInvoice>(e =>
        {
            e.Property(i => i.SeriesCode).HasMaxLength(NumberingSeries.MaxCodeLength).IsRequired();
            e.Property(i => i.InvoiceDate).HasColumnType("date");
            e.Property(i => i.CustomerNif).HasMaxLength(9).IsRequired();
            e.Property(i => i.CustomerName).HasMaxLength(Contact.MaxLegalNameLength).IsRequired();
            e.Property(i => i.CustomerAddress).HasMaxLength(Contact.MaxAddressLength).IsRequired();
            e.Property(i => i.IssuerNif).HasMaxLength(9).IsRequired();
            e.Property(i => i.IssuerName).HasMaxLength(120).IsRequired();
            e.Property(i => i.IssuerAddress).HasMaxLength(300);
            e.Property(i => i.Notes).HasMaxLength(CustomerInvoice.MaxNotesLength);
            e.Ignore(i => i.InvoiceId);
            e.HasOne(i => i.Customer).WithMany().HasForeignKey(i => i.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(i => i.InvoiceDate);
            // Un número no se puede repetir en la misma serie.
            e.HasIndex(i => new { i.SeriesCode, i.Number }).IsUnique();
            e.HasMany(i => i.Lines).WithOne().HasForeignKey(l => l.CustomerInvoiceId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<CustomerInvoiceLine>(e =>
        {
            e.Property(l => l.Description).HasMaxLength(CustomerInvoiceLine.MaxDescriptionLength).IsRequired();
            e.Ignore(l => l.BaseAmount);
        });

        // Una sola fila: el diseño de las facturas a clientes.
        model.Entity<InvoiceDesign>(e =>
        {
            e.Property(d => d.BackgroundColor).HasMaxLength(7).IsRequired();
            e.Property(d => d.AccentColor).HasMaxLength(7);
            e.Property(d => d.FooterText).HasMaxLength(InvoiceDesign.MaxFooterLength);
            e.Property(d => d.LogoContentType).HasMaxLength(50);
            e.Property(d => d.LogoFileName).HasMaxLength(InvoiceDesign.MaxFileNameLength);
            e.Property(d => d.CustomTemplateFileName).HasMaxLength(InvoiceDesign.MaxFileNameLength);
            e.Ignore(d => d.HasCustomTemplate);
        });

        model.Entity<Room>(e =>
        {
            e.Property(r => r.Name).HasMaxLength(Room.MaxNameLength).IsRequired();
            e.HasIndex(r => r.Name).IsUnique();
        });

        model.Entity<CalendarEvent>(e =>
        {
            e.Property(c => c.Title).HasMaxLength(CalendarEvent.MaxTitleLength).IsRequired();
            e.Property(c => c.Color).HasMaxLength(7).IsRequired();
            e.Property(c => c.Contact).HasMaxLength(CalendarEvent.MaxContactLength);
            e.Property(c => c.Notes).HasMaxLength(CalendarEvent.MaxNotesLength);
            e.Ignore(c => c.Day);
            // Las salas no se borran (se desactivan), así que sus eventos nunca se quedan sin ella.
            e.HasOne(c => c.Room).WithMany().HasForeignKey(c => c.RoomId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(c => c.Start);
            e.ToTable(t => t.HasCheckConstraint("CK_CalendarEvents_FinDespuesDeInicio", "\"End\" > \"Start\""));
        });

        model.Entity<PurchaseInvoice>(e =>
        {
            e.Property(i => i.Number).HasMaxLength(PurchaseInvoice.MaxNumberLength).IsRequired();
            e.Property(i => i.Notes).HasMaxLength(PurchaseInvoice.MaxNotesLength);
            e.Property(i => i.InvoiceFileName).HasMaxLength(260).IsRequired();
            e.Property(i => i.InvoiceDate).HasColumnType("date");
            e.Property(i => i.PaymentDate).HasColumnType("date");
            e.Ignore(i => i.IsPaid);
            e.HasOne(i => i.Provider).WithMany().HasForeignKey(i => i.ProviderId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(i => i.InvoiceDate);
            // La misma factura no se puede apuntar dos veces; las anuladas no cuentan.
            e.HasIndex(i => new { i.ProviderId, i.Number }).IsUnique().HasFilter("\"Canceled\" = false");
            e.HasMany(i => i.Lines).WithOne().HasForeignKey(l => l.PurchaseInvoiceId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<PurchaseInvoiceLine>(e =>
        {
            e.Property(l => l.Description).HasMaxLength(PurchaseInvoiceLine.MaxDescriptionLength).IsRequired();
            e.HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<DeliveryNote>().WithMany().HasForeignKey(l => l.DeliveryNoteId).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(l => l.BaseAmount);
            e.Ignore(l => l.IsFree);
            e.Ignore(l => l.IsFromDeliveryNote);
        });

        model.Entity<DeliveryNote>(e =>
        {
            e.Property(n => n.Number).HasMaxLength(DeliveryNote.MaxNumberLength).IsRequired();
            e.Property(n => n.Notes).HasMaxLength(DeliveryNote.MaxNotesLength);
            e.Property(n => n.FileName).HasMaxLength(260).IsRequired();
            e.Property(n => n.DeliveryDate).HasColumnType("date");
            e.Ignore(n => n.IsInvoiced);
            e.HasOne(n => n.Provider).WithMany().HasForeignKey(n => n.ProviderId).OnDelete(DeleteBehavior.Restrict);
            // La factura que lo incluye. Al anularla se vuelve a dejar a null (lo hace el servicio).
            e.HasOne(n => n.PurchaseInvoice).WithMany(i => i.DeliveryNotes).HasForeignKey(n => n.PurchaseInvoiceId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(n => n.DeliveryDate);
            // El mismo albarán no se puede apuntar dos veces; los anulados no cuentan.
            e.HasIndex(n => new { n.ProviderId, n.Number }).IsUnique().HasFilter("\"Canceled\" = false");
            e.HasMany(n => n.Lines).WithOne().HasForeignKey(l => l.DeliveryNoteId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<DeliveryNoteLine>(e =>
        {
            e.Property(l => l.Description).HasMaxLength(PurchaseLine.MaxDescriptionLength).IsRequired();
            e.HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(l => l.BaseAmount);
            e.Ignore(l => l.IsFree);
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

    /// <summary>Bloqueo de reservas de salas: mientras se comprueba si una sala está libre y se guarda el evento.</summary>
    public const long Calendar = 2;
}