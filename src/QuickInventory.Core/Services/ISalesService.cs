using QuickInventory.Core.Models;

namespace QuickInventory.Core.Services;

public interface ISalesService
{
    Task<Sale> RegisterSaleAsync(SalesPoint salesPoint, PaymentMethod paymentMethod, IReadOnlyList<CartLine> lines, bool guidedVisit = false, bool groupVisit = false);
    Task CancelSaleAsync(int saleId);
    Task<List<Sale>> GetSalesAsync(DateTime from, DateTime to, SalesPoint? salesPoint = null);
    /// <summary>Cambia el método de pago de una venta. Es lo único que se puede editar de una venta.</summary>
    Task ChangePaymentMethodAsync(int saleId, PaymentMethod paymentMethod);
}