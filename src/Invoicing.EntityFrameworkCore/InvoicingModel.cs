using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Invoicing.EntityFrameworkCore;

/// <summary>Configuración de EF Core de las tablas de Invoicing, para llamarla desde el DbContext anfitrión.</summary>
public static class InvoicingModel
{
    /// <summary>
    /// Desde <c>ConfigureConventions</c>: guarda como texto los tipos fiscales (IVA, exención, tipo de factura)
    /// y la disposición de la factura. Se aplica a todo el modelo, también a las tablas de la aplicación que
    /// usen esos tipos. Los importes (<c>decimal</c>) y las fechas se dejan a la aplicación.
    /// </summary>
    public static ModelConfigurationBuilder AddInvoicingConventions(this ModelConfigurationBuilder configuration)
    {
        configuration.Properties<InvoiceLayout>().HaveConversion<string>();
        configuration.Properties<InvoiceType>().HaveConversion<InvoiceTypeConverter>();
        configuration.Properties<TaxExemption>().HaveConversion<TaxExemptionConverter>();
        configuration.Properties<TaxRate>().HaveConversion<TaxRateConverter>();
        return configuration;
    }

    /// <summary>Desde <c>OnModelCreating</c>: longitudes, índices y relaciones de las tablas de Invoicing.</summary>
    public static ModelBuilder ApplyInvoicingModel(this ModelBuilder model)
    {
        // Una serie por año para las facturas a clientes.
        model.Entity<CustomerInvoiceSeries>(e =>
        {
            e.Property(i => i.Code).HasMaxLength(NumberingSeries.MaxCodeLength).IsRequired();
            e.HasIndex(s => s.Code).IsUnique();
            e.HasIndex(s => s.Year).IsUnique();
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

        return model;
    }
}

internal sealed class TaxRateConverter() : ValueConverter<TaxRate, string>(
    rate => rate.Value.ToString(CultureInfo.InvariantCulture),
    value => TaxRate.FromValue(decimal.Parse(value, CultureInfo.InvariantCulture))
);

internal sealed class TaxExemptionConverter() : ValueConverter<TaxExemption, string>(
    exemption => exemption.Value,
    value => TaxExemption.FromValue(value)
);

internal sealed class InvoiceTypeConverter() : ValueConverter<InvoiceType, string>(
    type => type.Value,
    value => InvoiceType.FromValue(value)
);
