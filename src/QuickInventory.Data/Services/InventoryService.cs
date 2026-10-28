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

    public async Task<List<Product>> GetProductsAsync(SalesPoint salesPoint)
    {
        // El perfil de cada categoría se decide en código, no en la base de datos,
        // así que se filtra en memoria. Son pocos productos.
        var products = await GetProductsAsync();
        return [.. products.Where(p => p.Category.SalesPoint == salesPoint)];
    }

    public async Task SaveProductAsync(Product product)
    {
        product.Normalize();
        product.Validate();

        await using var db = await factory.CreateDbContextAsync();

        // Solo los productos de tienda tienen código de barras.
        if (product is ShopProduct { BarCode: not null } shopProduct)
        {
            var duplicated = await db.Products.OfType<ShopProduct>().AnyAsync(p =>
                p.Active && p.Id != shopProduct.Id && p.BarCode == shopProduct.BarCode);
            if (duplicated)
                throw new BusinessRuleException("Ya hay otro producto con ese código de barras.");
        }

        if (product.Id == 0)
        {
            product.RegistrationDate = DateTime.Now;
            product.Active = true;

            db.Products.Add(product);

            if (product is ShopProduct { Stock: > 0 } newShopProduct)
            {
                db.StockMovements.Add(new StockMovement
                {
                    Product = newShopProduct,
                    Amount = newShopProduct.Stock,
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

            if (existing.GetType() != product.GetType())
                throw new BusinessRuleException(
                    "No se puede cambiar el tipo de un producto. Dalo de baja y crea uno nuevo.");

            existing.CopyFrom(product);
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

        if (product is not ShopProduct shopProduct)
            throw new BusinessRuleException("Este producto no lleva control de stock.");

        shopProduct.ChangeStock(amount);

        db.StockMovements.Add(new StockMovement
        {
            ProductId = shopProduct.Id,
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

}
