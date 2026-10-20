using Microsoft.EntityFrameworkCore;
using QuickInventory.Core;
using QuickInventory.Core.Models;
using QuickInventory.Core.Services;

namespace QuickInventory.Data.Services;

public sealed class SalesService(IDbContextFactory<BaseDbContext> factory) : ISalesService
{
    public async Task CancelSaleAsync(int saleId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var sale = await db.Sales.Include(s => s.Lines).FirstOrDefaultAsync(s => s.Id == saleId) ?? throw new BusinessRuleException($"La venta {saleId} ya no existe");

        if (sale.Canceled)
        {
            throw new BusinessRuleException("La venta ya ha sido cancelada");
        }

        sale.Canceled = true;

        foreach (var line in sale.Lines)
        {
            var product = await db.Products.FindAsync(line.ProductId);
            if (product is not ShopProduct shopProduct) continue;

            shopProduct.Stock += line.Amount;
            db.StockMovements.Add(new StockMovement
            {
                ProductId = shopProduct.Id,
                Amount = line.Amount,
                Type = StockMovementType.Cancelation,
                Description = $"Anulación de la venta {saleId}",
                Date = DateTime.Now,
                SaleId = saleId
            });
        }
        await db.SaveChangesAsync();
    }

    public async Task<List<Sale>> GetSalesAsync(DateTime from, DateTime to, SalesPoint? salesPoint = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Sales
            .AsNoTracking()
            .Where(s => s.Date >= from)
            .Where(s => s.Date < to)
            .Where(s => salesPoint == null || s.SalesPoint == salesPoint)
            .OrderBy(s => s.Date)
            .Include(s => s.Lines)
            .ToListAsync();
    }

    public async Task<Sale> RegisterTicketSaleAsync(PaymentMethod paymentMethod, IReadOnlyList<CartLine> lines, bool guidedVisit, bool groupVisit)
    {
        if (lines.Count == 0)
        {
            throw new BusinessRuleException("No hay nada que cobrar");
        }

        await using var db = await factory.CreateDbContextAsync();

        var sale = new TicketSale { Date = DateTime.Now, PaymentMethod = paymentMethod, GuidedVisit = guidedVisit, GroupVisit = groupVisit };
        // El suplemento de la visita guiada se suma después del descuento: el descuento es solo sobre la entrada.
        var supplement = guidedVisit ? TicketSale.GuidedVisitSupplement : 0;
        var groupDiscount = groupVisit ? TicketSale.GroupVisitDiscount : 0;

        await AddLinesAsync(db, sale, lines, supplement, groupDiscount);

        sale.Total = sale.Lines.Sum(l => l.Subtotal);
        db.Sales.Add(sale);
        await db.SaveChangesAsync();
        return sale;
    }

    public async Task<Sale> RegisterShopSaleAsync(PaymentMethod paymentMethod, IReadOnlyList<CartLine> lines)
    {
        if (lines.Count == 0)
        {
            throw new BusinessRuleException("No hay nada que cobrar");
        }

        await using var db = await factory.CreateDbContextAsync();

        var sale = new ShopSale { Date = DateTime.Now, PaymentMethod = paymentMethod, };

        await AddLinesAsync(db, sale, lines);

        sale.Total = sale.Lines.Sum(l => l.Subtotal);
        db.Sales.Add(sale);
        await db.SaveChangesAsync();
        return sale;
    }

    private async static Task AddLinesAsync(BaseDbContext db, Sale sale, IReadOnlyList<CartLine> lines, decimal supplement = 0, decimal discount = 0)
    {
        foreach (var line in lines)
        {
            if (line.Amount <= 0)
            {
                throw new BusinessRuleException("Linea sin cantidad.");
            }
            if (line.Discount is { Amount: <= 0 or > 100 })
            {
                throw new BusinessRuleException("El descuento tiene que estar entre 0 y 100 %.");
            }
            var product = await db.Products.FindAsync(line.ProductId) ?? throw new BusinessRuleException($"El producto {line.ProductId} no existe.");

            if (!product.Active)
            {
                throw new BusinessRuleException($"Producto {product.Name} descatalogado.");
            }
            if (product.Category.SalesPoint != sale.SalesPoint)
            {
                throw new BusinessRuleException("Producto no pertenece al punto de venta.");
            }

            if (product is ShopProduct shopProduct)
            {
                shopProduct.ChangeStock(-line.Amount);

                db.StockMovements.Add(new StockMovement
                {
                    ProductId = product.Id,
                    Amount = -line.Amount,
                    Type = StockMovementType.Sale,
                    Description = "Venta",
                    Date = sale.Date,
                    Sale = sale
                });
            }

            sale.Lines.Add(new SalesLine
            {
                ProductId = product.Id,
                Amount = line.Amount,
                Description = product.Name,
                CountsAs = product is TicketProduct ticketProduct ? ticketProduct.CountsAs : 1,
                BasePrice = product.SalePrice + supplement,
                UnitPrice = (line.Discount?.Apply(product.SalePrice) ?? product.SalePrice) + supplement - discount,
                DiscountName = line.Discount?.Name,
                DiscountPercent = line.Discount?.Amount ?? 0,
                Category = product.Category
            });
        }
    }

    public async Task ChangePaymentMethodAsync(int saleId, PaymentMethod paymentMethod)
    {
        await using var db = await factory.CreateDbContextAsync();

        var sale = await db.Sales.FindAsync(saleId)
            ?? throw new BusinessRuleException("La venta ya no existe.");

        if (sale.Canceled)
        {
            throw new BusinessRuleException("La venta está anulada y no se puede cambiar.");
        }

        sale.PaymentMethod = paymentMethod;
        await db.SaveChangesAsync();
    }
}
