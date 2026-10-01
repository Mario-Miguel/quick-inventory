using Microsoft.EntityFrameworkCore;
using QuickInventory.Core;
using QuickInventory.Core.Models;
using QuickInventory.Core.Services;

namespace QuickInventory.Data.Services;

public sealed class InventoryService(IDbContextFactory<BaseDbContext> factory) : IInventoryService
{
    public async Task<List<Product>> GetProductsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Products
            .AsNoTracking()
            .Where(p => p.Active)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task SaveProductAsync(Product product)
    {
        Normalize(product);
        Validate(product);

        await using var db = await factory.CreateDbContextAsync();

        if (product.BarCode is not null)
        {
            var duplicated = await db.Products.AnyAsync(p =>
                p.Active && p.Id != product.Id && p.BarCode == product.BarCode);
            if (duplicated)
                throw new BusinessRuleException("Ya hay otro producto con ese código de barras.");
        }

        if (product.Id == 0)
        {
            product.RegistrationDate = DateTime.Now;
            product.Active = true;
            if (!product.StockControl) product.Stock = 0;

            db.Products.Add(product);

            if (product.StockControl && product.Stock > 0)
            {
                db.StockMovements.Add(new StockMovement
                {
                    Product = product,
                    Amount = product.Stock,
                    Type = StockMovementType.Input,
                    Description = "Stock inicial",
                    Date = DateTime.Now
                });
            }
        }
        else
        {
            var existing = await db.Products.FindAsync(product.Id)
                ?? throw new BusinessRuleException("El producto ya no existe.");

            // El stock NO se copia: solo cambia con movimientos registrados.
            existing.Name = product.Name;
            existing.Category = product.Category;
            existing.BarCode = product.BarCode;
            existing.SalePrice = product.SalePrice;
            existing.CostPrice = product.CostPrice;
            existing.StockControl = product.StockControl;
            existing.MinStock = product.MinStock;
        }

        await db.SaveChangesAsync();
    }

    public async Task AdjustStockAsync(int productId, int amount, string? description)
    {
        if (amount == 0)
            throw new BusinessRuleException("La cantidad no puede ser cero.");

        await using var db = await factory.CreateDbContextAsync();

        var product = await db.Products.FindAsync(productId)
            ?? throw new BusinessRuleException("El producto ya no existe.");

        if (!product.StockControl)
            throw new BusinessRuleException("Este producto no lleva control de stock.");

        if (product.Stock + amount < 0)
            throw new BusinessRuleException(
                $"No hay stock suficiente. Quedan {product.Stock} unidades de «{product.Name}».");

        product.Stock += amount;

        db.StockMovements.Add(new StockMovement
        {
            ProductId = product.Id,
            Amount = amount,
            Type = amount > 0 ? StockMovementType.Input : StockMovementType.Output,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Date = DateTime.Now
        });

        await db.SaveChangesAsync();
    }

    public async Task UnsuscribeProductAsync(int productId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var product = await db.Products.FindAsync(productId)
            ?? throw new BusinessRuleException("El producto ya no existe.");

        product.Active = false;
        await db.SaveChangesAsync();
    }

    private static void Normalize(Product p)
    {
        p.Name = p.Name?.Trim() ?? "";
        p.Category = p.Category?.Trim() ?? "";
        p.BarCode = string.IsNullOrWhiteSpace(p.BarCode) ? null : p.BarCode.Trim();
    }

    private static void Validate(Product p)
    {
        if (p.Name.Length == 0)
            throw new BusinessRuleException("El producto necesita un nombre.");
        if (p.SalePrice < 0 || p.CostPrice < 0)
            throw new BusinessRuleException("Los precios no pueden ser negativos.");
        if (p.Stock < 0 || p.MinStock < 0)
            throw new BusinessRuleException("El stock no puede ser negativo.");
    }
}
