using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuickInventory.Core.Models;
using QuickInventory.Core.Services;

namespace QuickInventory.Data.Services;

public sealed class PurchaseInvoiceService(IDbContextFactory<BaseDbContext> factory) : IPurchaseInvoiceService
{
    public async Task<List<PurchaseInvoice>> GetInvoicesAsync(DateTime from, DateTime to)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await WithDetails(db)
            .Where(i => i.InvoiceDate >= from && i.InvoiceDate < to)
            .OrderBy(i => i.InvoiceDate)
            .ToListAsync();
    }

    public async Task<List<PurchaseInvoice>> GetPaidInvoicesAsync(DateTime from, DateTime to)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await WithDetails(db)
            .Where(i => !i.Canceled && i.PaymentDate >= from && i.PaymentDate < to)
            .OrderBy(i => i.PaymentDate)
            .ToListAsync();
    }

    public async Task<List<PurchaseInvoice>> GetUnpaidInvoicesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await WithDetails(db)
            .Where(i => i.PaymentDate == null && !i.Canceled)
            .OrderBy(i => i.InvoiceDate)
            .ToListAsync();
    }

    public async Task<List<PurchaseInvoice>> GetCashPaymentsAsync(DateTime day, SalesPoint salesPoint)
    {
        var date = day.Date;
        await using var db = await factory.CreateDbContextAsync();
        return await db.PurchaseInvoices
            .AsNoTracking()
            .Include(i => i.Provider)
            .Where(i => !i.Canceled
                && i.PaymentDate == date
                && i.PaymentMethod == PaymentMethod.Cash
                && i.PaymentSalesPoint == salesPoint)
            .OrderBy(i => i.Id)
            .ToListAsync();
    }

    private static IQueryable<PurchaseInvoice> WithDetails(BaseDbContext db) => db.PurchaseInvoices
        .AsNoTracking()
        .AsSplitQuery()
        .Include(i => i.Provider)
        .Include(i => i.Lines.OrderBy(l => l.Id))
        .Include(i => i.DeliveryNotes.OrderBy(n => n.DeliveryDate).ThenBy(n => n.Id));

    public async Task<PurchaseInvoice> RegisterInvoiceAsync(PurchaseInvoice invoice, InvoiceFile file)
    {
        invoice.Normalize();
        invoice.Validate();
        file.Validate();

        await using var db = await factory.CreateDbContextAsync();

        var provider = await db.Providers.AsNoTracking().FirstOrDefaultAsync(p => p.Id == invoice.ProviderId)
            ?? throw new BusinessRuleException("El proveedor ya no existe.");
        if (!provider.Active)
            throw new BusinessRuleException($"El proveedor {provider.LegalName} está desactivado.");

        // El índice único lo impediría igualmente, pero así el mensaje es claro.
        if (await db.PurchaseInvoices.AnyAsync(i => i.ProviderId == invoice.ProviderId && i.Number == invoice.Number && !i.Canceled))
            throw new BusinessRuleException(DuplicateMessage(invoice.Number, provider.LegalName));

        // Albaranes incluidos: del mismo proveedor y pendientes de facturar. Se vuelve a comprobar al unirlos.
        var noteIds = invoice.Lines.Where(l => l.DeliveryNoteId is not null).Select(l => l.DeliveryNoteId!.Value).Distinct().ToList();
        var notes = await db.DeliveryNotes.AsNoTracking().Where(n => noteIds.Contains(n.Id)).ToListAsync();
        foreach (var noteId in noteIds)
        {
            var note = notes.FirstOrDefault(n => n.Id == noteId)
                ?? throw new BusinessRuleException("Uno de los albaranes ya no existe.");
            if (note.ProviderId != invoice.ProviderId)
                throw new BusinessRuleException($"El albarán {note.Number} es de otro proveedor.");
            if (note.Canceled)
                throw new BusinessRuleException($"El albarán {note.Number} está anulado.");
            if (note.IsInvoiced)
                throw new BusinessRuleException(NoteAlreadyInvoicedMessage(note.Number));
        }

        // Artículos de las líneas: se cargan con seguimiento para sumarles el stock y cambiar su coste.
        var productIds = invoice.Lines.Where(l => l.ProductId is not null).Select(l => l.ProductId!.Value).Distinct().ToList();
        var products = await db.Products.OfType<ShopProduct>()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var saved = new PurchaseInvoice
        {
            ProviderId = invoice.ProviderId,
            Number = invoice.Number,
            InvoiceDate = invoice.InvoiceDate,
            PaymentDate = invoice.PaymentDate,
            PaymentMethod = invoice.PaymentMethod,
            PaymentSalesPoint = invoice.PaymentSalesPoint,
            Notes = invoice.Notes,
            RegistrationDate = DateTime.Now,
            InvoiceFileName = Path.GetFileName(file.FileName),
        };

        foreach (var line in invoice.Lines)
        {
            var copy = new PurchaseInvoiceLine
            {
                DeliveryNoteId = line.DeliveryNoteId,
                ProductId = line.ProductId,
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                TaxRate = line.TaxRate,
            };

            if (line.IsFromDeliveryNote)
            {
                // El stock ya lo sumó el albarán: solo cuenta el precio que cobra la factura. El concepto es
                // el que se copió en el albarán (el artículo puede haberse renombrado o desactivado después).
                if (line.ProductId is { } id && products.TryGetValue(id, out var product) && product.Active && line.UnitPrice > 0)
                    product.CostPrice = line.UnitPrice;
            }
            else if (line.ProductId is { } productId)
            {
                if (!products.TryGetValue(productId, out var product) || !product.Active)
                    throw new BusinessRuleException($"El artículo «{line.Description}» ya no está en el inventario de Tienda.");

                // El nombre se copia del producto (no del formulario), como en las ventas.
                copy.Description = product.Name;
                product.ChangeStock(line.Quantity);
                // Coste = último precio pagado. Si el artículo sale dos veces, se queda el de la última línea.
                product.CostPrice = line.UnitPrice;
            }
            saved.Lines.Add(copy);
        }
        // Con las descripciones definitivas: los totales no cambian, pero así quedan calculados aquí.
        saved.CalculateTotals();

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            db.PurchaseInvoices.Add(saved);
            await db.SaveChangesAsync();

            // En un segundo paso porque necesitan el Id de la factura. Si algo falla, se deshace todo.
            var reference = $"Factura {saved.Number} de {provider.LegalName}";
            foreach (var line in saved.Lines.Where(l => l.ProductId is not null && !l.IsFromDeliveryNote))
            {
                db.StockMovements.Add(new StockMovement
                {
                    ProductId = line.ProductId!.Value,
                    Amount = line.Quantity,
                    Type = StockMovementType.Purchase,
                    Description = reference,
                    Date = saved.RegistrationDate,
                    PurchaseInvoiceId = saved.Id,
                });
            }
            db.InvoiceFiles.Add(new InvoiceFile
            {
                PurchaseInvoiceId = saved.Id,
                FileName = saved.InvoiceFileName,
                ContentType = file.ContentType,
                Content = file.Content,
            });
            await db.SaveChangesAsync();

            // Se unen los albaranes solo si siguen pendientes: si otro equipo los ha facturado o anulado
            // a la vez, no se actualizan todos y se deshace la factura entera.
            if (noteIds.Count > 0)
            {
                var linked = await db.DeliveryNotes
                    .Where(n => noteIds.Contains(n.Id) && n.PurchaseInvoiceId == null && !n.Canceled)
                    .ExecuteUpdateAsync(s => s.SetProperty(n => n.PurchaseInvoiceId, saved.Id));
                if (linked != noteIds.Count)
                    throw new BusinessRuleException("Alguno de los albaranes se acaba de facturar o anular en otro equipo. Vuelve a abrir la factura.");
            }
            await transaction.CommitAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Otro equipo apuntó la misma factura a la vez.
            throw new BusinessRuleException(DuplicateMessage(invoice.Number, provider.LegalName));
        }

        saved.Provider = provider;
        foreach (var note in notes) note.PurchaseInvoiceId = saved.Id;
        saved.DeliveryNotes = notes.OrderBy(n => n.DeliveryDate).ThenBy(n => n.Id).ToList();
        return saved;
    }

    public async Task SetPaymentAsync(int invoiceId, DateTime? paymentDate, PaymentMethod? paymentMethod, SalesPoint? salesPoint)
    {
        await using var db = await factory.CreateDbContextAsync();
        var invoice = await db.PurchaseInvoices.FindAsync(invoiceId)
            ?? throw new BusinessRuleException("La factura ya no existe.");
        if (invoice.Canceled)
            throw new BusinessRuleException("La factura está anulada.");

        invoice.PaymentDate = paymentDate;
        invoice.PaymentMethod = paymentMethod;
        invoice.PaymentSalesPoint = salesPoint;
        invoice.NormalizePayment();
        invoice.ValidatePayment();
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Anula la factura y resta del stock las unidades que sumó. Si ya se han vendido y no quedan
    /// bastantes, no se anula (avisa de qué artículo falta). El precio de coste no se toca.
    /// </summary>
    public async Task CancelInvoiceAsync(int invoiceId)
    {
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var invoice = await db.PurchaseInvoices
            .Include(i => i.Provider)
            .FirstOrDefaultAsync(i => i.Id == invoiceId)
            ?? throw new BusinessRuleException("La factura ya no existe.");
        if (invoice.Canceled)
            throw new BusinessRuleException("La factura ya está anulada.");

        // Lo que sumó la factura, según sus movimientos de stock.
        var added = await db.StockMovements
            .Where(m => m.PurchaseInvoiceId == invoiceId && m.Type == StockMovementType.Purchase)
            .GroupBy(m => m.ProductId)
            .Select(g => new { ProductId = g.Key, Amount = g.Sum(m => m.Amount) })
            .ToListAsync();
        var productIds = added.Select(a => a.ProductId).ToList();
        var products = await db.Products.OfType<ShopProduct>()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var reference = $"Anulada la factura {invoice.Number} de {invoice.Provider?.LegalName}";
        var now = DateTime.Now;
        foreach (var item in added)
        {
            products[item.ProductId].ChangeStock(-item.Amount);
            db.StockMovements.Add(new StockMovement
            {
                ProductId = item.ProductId,
                Amount = -item.Amount,
                Type = StockMovementType.PurchaseCancelation,
                Description = reference,
                Date = now,
                PurchaseInvoiceId = invoiceId,
            });
        }

        invoice.Canceled = true;
        await db.SaveChangesAsync();

        // Sus albaranes vuelven a estar pendientes de facturar. Su stock no se toca: es del albarán.
        await db.DeliveryNotes
            .Where(n => n.PurchaseInvoiceId == invoiceId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.PurchaseInvoiceId, (int?)null));
        await transaction.CommitAsync();
    }

    public async Task<InvoiceFile?> GetInvoiceFileAsync(int invoiceId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.InvoiceFiles.AsNoTracking().FirstOrDefaultAsync(f => f.PurchaseInvoiceId == invoiceId);
    }

    public async Task<List<InvoiceFile>> GetInvoiceFilesAsync(IReadOnlyCollection<int> invoiceIds)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.InvoiceFiles
            .AsNoTracking()
            .Where(f => f.PurchaseInvoiceId != null && invoiceIds.Contains(f.PurchaseInvoiceId.Value))
            .ToListAsync();
    }

    private static string NoteAlreadyInvoicedMessage(string number) =>
        $"El albarán {number} ya está en otra factura.";

    private static string DuplicateMessage(string number, string providerName) =>
        $"La factura {number} de {providerName} ya está apuntada.";
}
