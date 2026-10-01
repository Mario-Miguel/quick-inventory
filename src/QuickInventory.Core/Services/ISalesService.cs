using QuickInventory.Core.Models;

namespace QuickInventory.Core.Services;

public interface ISalesService
{
    Task<Sale> RegisterSaleAsync(SalesPoint salesPoint, PaymentMethod paymentMethod, IReadOnlyList<CartLine> lines);
    Task CancelSaleAsync(int saleId);
    Task<List<Sale>> GetSalesAsync(DateTime from, DateTime to, SalesPoint? salesPoint = null);
}