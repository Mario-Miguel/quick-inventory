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
}
