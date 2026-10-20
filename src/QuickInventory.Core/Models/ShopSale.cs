namespace QuickInventory.Core.Models;

public class ShopSale : Sale
{
    public ShopSale() => SalesPoint = SalesPoint.Shop;
}