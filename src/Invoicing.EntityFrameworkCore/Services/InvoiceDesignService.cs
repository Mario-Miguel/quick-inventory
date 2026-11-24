namespace Invoicing.EntityFrameworkCore.Services;

internal sealed class InvoiceDesignService<TContext>(IDbContextFactory<TContext> factory) : IInvoiceDesignService
    where TContext : DbContext, IInvoicingDbContext
{
    public async Task<InvoiceDesign> GetDesignAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.InvoiceDesigns.AsNoTracking().OrderBy(d => d.Id).FirstOrDefaultAsync() ?? new InvoiceDesign();
    }

    public async Task SaveDesignAsync(InvoiceDesign design)
    {
        design.Normalize();
        design.Validate();

        await using var db = await factory.CreateDbContextAsync();
        var saved = await db.InvoiceDesigns.OrderBy(d => d.Id).FirstOrDefaultAsync();
        if (saved is null)
        {
            saved = new InvoiceDesign();
            db.InvoiceDesigns.Add(saved);
        }

        saved.Layout = design.Layout;
        saved.BackgroundColor = design.BackgroundColor;
        saved.AccentColor = design.AccentColor;
        saved.FooterText = design.FooterText;
        saved.Logo = design.Logo;
        saved.LogoContentType = design.LogoContentType;
        saved.LogoFileName = design.LogoFileName;
        saved.CustomTemplate = design.CustomTemplate;
        saved.CustomTemplateFileName = design.CustomTemplateFileName;
        await db.SaveChangesAsync();
    }
}
