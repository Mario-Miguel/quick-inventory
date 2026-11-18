using QuickInventory.Core.Models;

namespace QuickInventory.Core.Services;

/// <summary>Albaranes de proveedores: la mercancía que llega. Suman stock, pero no se pagan.</summary>
public interface IDeliveryNoteService
{
    Task<List<DeliveryNote>> GetDeliveryNotesAsync(DateTime from, DateTime to);
    Task<List<DeliveryNote>> GetPendingDeliveryNotesAsync(int? providerId = null);
    Task<DeliveryNote> RegisterDeliveryNoteAsync(DeliveryNote note, InvoiceFile file);
    Task CancelDeliveryNoteAsync(int deliveryNoteId);
    Task<InvoiceFile?> GetDeliveryNoteFileAsync(int deliveryNoteId);
}
