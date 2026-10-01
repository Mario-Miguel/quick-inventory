using QuickInventory.Core.Models;

namespace QuickInventory.Core.Services;

public interface IInventoryService
{
    /// <summary>Products activos, ordenados por nombre.</summary>
    Task<List<Product>> GetProductsAsync();

    /// <summary>
    /// Crea el product si Id == 0 o actualiza sus datos si ya existe.
    /// Al editar, el stock no se modifica: para eso está <see cref="AdjustStockAsync"/>.
    /// </summary>
    Task SaveProductAsync(Product product);

    /// <summary>
    /// Suma (amount positiva) o resta (negativa) unidades y deja constancia del description.
    /// </summary>
    Task AdjustStockAsync(int productId, int amount, string? description);

    /// <summary>Oculta el product sin borrar su historial.</summary>
    Task UnsuscribeProductAsync(int productId);
}
