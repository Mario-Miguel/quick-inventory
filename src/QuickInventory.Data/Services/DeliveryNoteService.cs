using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuickInventory.Core.Models;
using QuickInventory.Core.Services;

namespace QuickInventory.Data.Services;

public sealed class DeliveryNoteService(IDbContextFactory<BaseDbContext> factory) : IDeliveryNoteService
{
    public async Task<List<DeliveryNote>> GetDeliveryNotesAsync(DateTime from, DateTime to)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await WithDetails(db)
            .Include(n => n.PurchaseInvoice)
            .Where(n => n.DeliveryDate >= from && n.DeliveryDate < to)
            .OrderBy(n => n.DeliveryDate)
            .ToListAsync();
    }

    public async Task<List<DeliveryNote>> GetPendingDeliveryNotesAsync(int? providerId = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await WithDetails(db)
            .Where(n => n.PurchaseInvoiceId == null && !n.Canceled && (providerId == null || n.ProviderId == providerId))
            .OrderBy(n => n.DeliveryDate)
            .ThenBy(n => n.Id)
            .ToListAsync();
    }

    private static IQueryable<DeliveryNote> WithDetails(BaseDbContext db) => db.DeliveryNotes
        .AsNoTracking()
        .AsSplitQuery()
        .Include(n => n.Provider)
        .Include(n => n.Lines.OrderBy(l => l.Id));

    public async Task<DeliveryNote> RegisterDeliveryNoteAsync(DeliveryNote note, InvoiceFile file)
    {
        note.Normalize();
        note.Validate();
        file.Validate();

        await using var db = await factory.CreateDbContextAsync();

        var provider = await db.Providers.AsNoTracking().FirstOrDefaultAsync(p => p.Id == note.ProviderId)
            ?? throw new BusinessRuleException("El proveedor ya no existe.");
        if (!provider.Active)
            throw new BusinessRuleException($"El proveedor {provider.LegalName} está desactivado.");

        // El índice único lo impediría igualmente, pero así el mensaje es claro.
        if (await db.DeliveryNotes.AnyAsync(n => n.ProviderId == note.ProviderId && n.Number == note.Number && !n.Canceled))
            throw new BusinessRuleException(DuplicateMessage(note.Number, provider.LegalName));

        // Artículos de las líneas: se cargan con seguimiento para sumarles el stock y cambiar su coste.
        var productIds = note.Lines.Where(l => l.ProductId is not null).Select(l => l.ProductId!.Value).Distinct().ToList();
        var products = await db.Products.OfType<ShopProduct>()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var saved = new DeliveryNote
        {
            ProviderId = note.ProviderId,
            Number = note.Number,
            DeliveryDate = note.DeliveryDate,
            Notes = note.Notes,
            RegistrationDate = DateTime.Now,
            FileName = Path.GetFileName(file.FileName),
        };

        foreach (var line in note.Lines)
        {
            var copy = new DeliveryNoteLine
            {
                ProductId = line.ProductId,
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                TaxRate = line.TaxRate,
            };

            if (line.ProductId is { } productId)
            {
                if (!products.TryGetValue(productId, out var product) || !product.Active)
                    throw new BusinessRuleException($"El artículo «{line.Description}» ya no está en el inventario de Tienda.");

                // El nombre se copia del producto (no del formulario), como en las ventas.
                copy.Description = product.Name;
                product.ChangeStock(line.Quantity);
                // Un albarán sin valorar (precio 0) no cambia el coste: ya lo pondrá la factura.
                if (line.UnitPrice > 0) product.CostPrice = line.UnitPrice;
            }
            saved.Lines.Add(copy);
        }
        saved.CalculateTotals();

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            db.DeliveryNotes.Add(saved);
            await db.SaveChangesAsync();

            // En un segundo paso porque necesitan el Id del albarán. Si algo falla, se deshace todo.
            var reference = $"Albarán {saved.Number} de {provider.LegalName}";
            foreach (var line in saved.Lines.Where(l => l.ProductId is not null))
            {
                db.StockMovements.Add(new StockMovement
                {
                    ProductId = line.ProductId!.Value,
                    Amount = line.Quantity,
                    Type = StockMovementType.DeliveryNote,
                    Description = reference,
                    Date = saved.RegistrationDate,
                    DeliveryNoteId = saved.Id,
                });
            }
            db.InvoiceFiles.Add(new InvoiceFile
            {
                DeliveryNoteId = saved.Id,
                FileName = saved.FileName,
                ContentType = file.ContentType,
                Content = file.Content,
            });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Otro equipo apuntó el mismo albarán a la vez.
            throw new BusinessRuleException(DuplicateMessage(note.Number, provider.LegalName));
        }

        saved.Provider = provider;
        return saved;
    }

    /// <summary>
    /// Anula el albarán y resta del stock las unidades que sumó. No se puede si ya está en una factura,
    /// ni si se han vendido y no quedan bastantes (avisa de qué artículo falta). El precio de coste no se toca.
    /// </summary>
    public async Task CancelDeliveryNoteAsync(int deliveryNoteId)
    {
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var note = await db.DeliveryNotes
            .Include(n => n.Provider)
            .Include(n => n.PurchaseInvoice)
            .FirstOrDefaultAsync(n => n.Id == deliveryNoteId)
            ?? throw new BusinessRuleException("El albarán ya no existe.");
        if (note.Canceled)
            throw new BusinessRuleException("El albarán ya está anulado.");
        if (note.PurchaseInvoice is { } invoice)
            throw new BusinessRuleException(
                $"El albarán {note.Number} está en la factura {invoice.Number}. Para anularlo, anula antes la factura.");

        // Se marca con una condición para que no se cuele una factura que lo incluya a la vez: la fila queda
        // bloqueada hasta el final de la transacción, y la factura vuelve a comprobar que no está anulado.
        var canceled = await db.DeliveryNotes
            .Where(n => n.Id == deliveryNoteId && !n.Canceled && n.PurchaseInvoiceId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.Canceled, true));
        if (canceled == 0)
            throw new BusinessRuleException($"El albarán {note.Number} acaba de cambiar en otro equipo. Vuelve a intentarlo.");

        // Lo que sumó el albarán, según sus movimientos de stock.
        var added = await db.StockMovements
            .Where(m => m.DeliveryNoteId == deliveryNoteId && m.Type == StockMovementType.DeliveryNote)
            .GroupBy(m => m.ProductId)
            .Select(g => new { ProductId = g.Key, Amount = g.Sum(m => m.Amount) })
            .ToListAsync();
        var productIds = added.Select(a => a.ProductId).ToList();
        var products = await db.Products.OfType<ShopProduct>()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var reference = $"Anulado el albarán {note.Number} de {note.Provider?.LegalName}";
        var now = DateTime.Now;
        foreach (var item in added)
        {
            products[item.ProductId].ChangeStock(-item.Amount);
            db.StockMovements.Add(new StockMovement
            {
                ProductId = item.ProductId,
                Amount = -item.Amount,
                Type = StockMovementType.DeliveryNoteCancelation,
                Description = reference,
                Date = now,
                DeliveryNoteId = deliveryNoteId,
            });
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<InvoiceFile?> GetDeliveryNoteFileAsync(int deliveryNoteId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.InvoiceFiles.AsNoTracking().FirstOrDefaultAsync(f => f.DeliveryNoteId == deliveryNoteId);
    }

    private static string DuplicateMessage(string number, string providerName) =>
        $"El albarán {number} de {providerName} ya está apuntado.";
}
