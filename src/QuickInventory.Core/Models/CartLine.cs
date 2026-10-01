namespace QuickInventory.Core.Models;

/// <summary>
/// Producto y unidades que se quieren cobrar. Lo rellena la pantalla de cobro.
/// El nombre y el precio no van aquí: al registrar la venta se toman de la base
/// de datos y se copian en la <see cref="SalesLine"/>.
/// </summary>
public sealed record CartLine(int ProductId, int Amount);
