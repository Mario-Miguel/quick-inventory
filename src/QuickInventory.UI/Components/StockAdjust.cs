namespace QuickInventory.UI.Components;

/// <summary>Resultado del diálogo de stock: positivo entra, negativo sale.</summary>
public sealed record StockAdjust(int Amount, string? Description);
