using MudBlazor;
using QuickInventory.Core.Models;

namespace QuickInventory.UI;

/// <summary>Texto e icono de cada método de pago para mostrarlos en pantalla.</summary>
public static class PaymentMethods
{
    public static string Name(PaymentMethod paymentMethod) => paymentMethod switch
    {
        PaymentMethod.Cash => "Efectivo",
        PaymentMethod.Card => "Tarjeta",
        PaymentMethod.Bizum => "Bizum",
        PaymentMethod.BankTransfer => "Transferencia",
        _ => paymentMethod.ToString()
    };

    public static string Icon(PaymentMethod paymentMethod) => paymentMethod switch
    {
        PaymentMethod.Cash => Icons.Material.Filled.Payments,
        PaymentMethod.Card => Icons.Material.Filled.CreditCard,
        PaymentMethod.Bizum => Icons.Material.Filled.PhoneAndroid,
        _ => Icons.Material.Filled.AccountBalance
    };

    /// <summary>
    /// Número de ventas e importe cobrado con cada método de pago, sin las anuladas.
    /// Salen todos los métodos, también los que no se han usado (a 0).
    /// </summary>
    public static List<(PaymentMethod Method, int Count, decimal Amount)> Totals(IEnumerable<Sale> sales)
    {
        var valid = sales.Where(s => !s.Canceled).ToList();
        return Enum.GetValues<PaymentMethod>().Select(method =>
        {
            var ofMethod = valid.Where(s => s.PaymentMethod == method).ToList();
            return (method, ofMethod.Count, ofMethod.Sum(s => s.Total));
        }).ToList();
    }
}
