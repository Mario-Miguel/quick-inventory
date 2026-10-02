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

        var sale = await db.Sales.Include(s=>s.Lines).FirstOrDefaultAsync(s => s.Id==saleId) ?? throw new BusinessRuleException($"La venta {saleId} ya no existe");

        if (sale.Canceled)
        {
            throw new BusinessRuleException("La venta ya ha sido cancelada");
        }

        sale.Canceled = true;

        foreach (var line in sale.Lines)
        {
            var product = await db.Products.FindAsync(line.ProductId);
            if (product is null || !product.StockControl) continue;

            product.Stock += line.Amount;
            db.StockMovements.Add(new StockMovement
            {
                ProductId=product.Id,
                Amount = line.Amount,
                    Type=StockMovementType.Cancelation,
                    Description=$"Anulación de la venta {saleId}",
                    Date=DateTime.Now,
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
            .Include(s=>s.Lines)
            .ToListAsync();
    }

    public async Task<Sale> RegisterSaleAsync(SalesPoint salesPoint, PaymentMethod paymentMethod, IReadOnlyList<CartLine> lines)
    {
        if (lines.Count == 0)
        {
            throw new BusinessRuleException("No hay nada que cobrar");
        }
        if (salesPoint == SalesPoint.Admin)
        {
            throw new BusinessRuleException("Admin no puede vender");
        }

        await using var db = await factory.CreateDbContextAsync();

        var sale = new Sale{Date = DateTime.Now, SalesPoint= salesPoint, PaymentMethod= paymentMethod};

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
            if (product.Category.SalesPoint != salesPoint)
            {
                throw new BusinessRuleException("Producto no pertenece al punto de venta.");
            }

            if (product.StockControl)
            {
                if(product.Stock < line.Amount)
                {
                    throw new BusinessRuleException($"No hay stock suficiente. Quedan {product.Stock} unidades de {product.Name}");
                }

                product.Stock -= line.Amount;

                db.StockMovements.Add(new StockMovement
                {
                    ProductId= product.Id,
                    Amount = -line.Amount,
                    Type=StockMovementType.Sale,
                    Description="Venta",
                    Date=sale.Date,
                    Sale = sale
                });
            }

            sale.Lines.Add(new SalesLine
            {
                ProductId = product.Id,
                Amount = line.Amount,
                Description = product.Name,
                BasePrice = product.SalePrice,
                UnitPrice = line.Discount?.Apply(product.SalePrice) ?? product.SalePrice,
                DiscountName = line.Discount?.Name,
                DiscountPercent = line.Discount?.Amount ?? 0,
                Category = product.Category
            });
        }

        sale.Total = sale.Lines.Sum(l=>l.Subtotal);
        db.Sales.Add(sale);
        await db.SaveChangesAsync();
        return sale;
    }
}