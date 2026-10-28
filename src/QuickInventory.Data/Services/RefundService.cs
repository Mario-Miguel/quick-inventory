using Microsoft.EntityFrameworkCore;
using QuickInventory.Core;
using QuickInventory.Core.Models;
using QuickInventory.Core.Services;

namespace QuickInventory.Data.Services;

public sealed class RefundService(IDbContextFactory<BaseDbContext> factory) : IRefundService
{
    public async Task<CashMovement> RegisterRefundAsync(CashMovement refund, InvoiceFile? invoice = null)
    {
        if (invoice is not null) Validate(invoice);

        if (string.IsNullOrWhiteSpace(refund.Person))
        {
            throw new BusinessRuleException("Escribe a quién se le devuelve el dinero.");
        }
        if (string.IsNullOrWhiteSpace(refund.Description))
        {
            throw new BusinessRuleException("Escribe qué se compró.");
        }
        if (refund.Amount <= 0)
        {
            throw new BusinessRuleException("El importe tiene que ser mayor que 0.");
        }
        // En efectivo el dinero sale de una caja; por transferencia no pasa por ninguna.
        switch (refund.PaymentMethod)
        {
            case PaymentMethod.Cash when refund.SalesPoint is not (SalesPoint.TicketOffice or SalesPoint.Shop):
                throw new BusinessRuleException("Elige de qué caja sale el dinero: Taquilla o Tienda.");
            case PaymentMethod.BankTransfer when refund.SalesPoint is not null:
                throw new BusinessRuleException("Una transferencia no sale de ninguna caja.");
            case PaymentMethod.Card:
                throw new BusinessRuleException("Un reembolso se paga en efectivo de una caja o por transferencia.");
        }

        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var movement = new CashMovement
        {
            Date = DateTime.Now,
            Type = CashMovementType.Pay,
            Category = CashMovement.RefundCategory,
            SalesPoint = refund.SalesPoint,
            Person = refund.Person.Trim(),
            Description = refund.Description.Trim(),
            InvoiceNumber = string.IsNullOrWhiteSpace(refund.InvoiceNumber) ? null : refund.InvoiceNumber.Trim(),
            Amount = Math.Round(refund.Amount, 2, MidpointRounding.AwayFromZero),
            PaymentMethod = refund.PaymentMethod,
            Notes = string.IsNullOrWhiteSpace(refund.Notes) ? null : refund.Notes.Trim(),
        };

        db.CashMovements.Add(movement);
        await db.SaveChangesAsync();

        // En dos pasos porque la factura necesita el Id del reembolso. Si falla, se deshace todo.
        if (invoice is not null)
        {
            db.InvoiceFiles.Add(NewInvoice(movement, invoice));
            await db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        return movement;
    }

    public async Task AttachInvoiceAsync(int refundId, InvoiceFile invoice)
    {
        Validate(invoice);

        await using var db = await factory.CreateDbContextAsync();
        // El borrado de la anterior se ejecuta al momento: con la transacción, si algo falla no se pierde.
        await using var transaction = await db.Database.BeginTransactionAsync();
        var refund = await FindRefundAsync(db, refundId);

        await db.InvoiceFiles.Where(f => f.CashMovementId == refundId).ExecuteDeleteAsync();
        db.InvoiceFiles.Add(NewInvoice(refund, invoice));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task RemoveInvoiceAsync(int refundId)
    {
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var refund = await FindRefundAsync(db, refundId);

        await db.InvoiceFiles.Where(f => f.CashMovementId == refundId).ExecuteDeleteAsync();
        refund.InvoiceFileName = null;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<InvoiceFile?> GetInvoiceAsync(int refundId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.InvoiceFiles.AsNoTracking().FirstOrDefaultAsync(f => f.CashMovementId == refundId);
    }

    public async Task<List<InvoiceFile>> GetInvoicesAsync(IReadOnlyCollection<int> refundIds)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.InvoiceFiles.AsNoTracking().Where(f => refundIds.Contains(f.CashMovementId)).ToListAsync();
    }

    private static async Task<CashMovement> FindRefundAsync(BaseDbContext db, int refundId) =>
        await db.CashMovements.FirstOrDefaultAsync(m => m.Id == refundId && m.Category == CashMovement.RefundCategory)
        ?? throw new BusinessRuleException($"El reembolso {refundId} ya no existe.");

    /// <summary>Copia de la factura para guardarla, y apunta su nombre en el reembolso.</summary>
    private static InvoiceFile NewInvoice(CashMovement refund, InvoiceFile invoice)
    {
        refund.InvoiceFileName = Path.GetFileName(invoice.FileName);
        return new InvoiceFile
        {
            CashMovementId = refund.Id,
            FileName = refund.InvoiceFileName,
            ContentType = invoice.ContentType,
            Content = invoice.Content,
        };
    }

    private static void Validate(InvoiceFile invoice)
    {
        if (!InvoiceFile.AllowedExtensions.Contains(invoice.Extension))
        {
            throw new BusinessRuleException("La factura tiene que ser un PDF o una foto (JPG o PNG).");
        }
        if (invoice.Content.Length == 0)
        {
            throw new BusinessRuleException("El archivo de la factura está vacío.");
        }
        if (invoice.Content.Length > InvoiceFile.MaxSize)
        {
            throw new BusinessRuleException($"La factura ocupa demasiado. El máximo son {InvoiceFile.MaxSize / 1024 / 1024} MB.");
        }
    }

    public async Task CancelRefundAsync(int refundId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var refund = await db.CashMovements
            .FirstOrDefaultAsync(m => m.Id == refundId && m.Category == CashMovement.RefundCategory)
            ?? throw new BusinessRuleException($"El reembolso {refundId} ya no existe.");

        if (refund.Canceled)
        {
            throw new BusinessRuleException("El reembolso ya está anulado.");
        }

        refund.Canceled = true;
        await db.SaveChangesAsync();
    }

    public async Task<List<CashMovement>> GetRefundsAsync(DateTime from, DateTime to, SalesPoint? salesPoint = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.CashMovements
            .AsNoTracking()
            .Where(m => m.Category == CashMovement.RefundCategory)
            .Where(m => m.Date >= from)
            .Where(m => m.Date < to)
            .Where(m => salesPoint == null || m.SalesPoint == salesPoint)
            .OrderBy(m => m.Date)
            .ToListAsync();
    }
}
