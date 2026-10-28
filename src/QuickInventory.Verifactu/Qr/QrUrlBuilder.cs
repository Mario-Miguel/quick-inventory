namespace QuickInventory.Verifactu;

/// <summary>
/// URL de cotejo que va en el código QR de la factura.
/// </summary>
public static class QrUrlBuilder
{
    public static string Build(AeatEnvironment environment, InvoiceId invoice, decimal totalAmount) =>
        $"{AeatEndpoints.QrValidation(environment)}" +
        $"?nif={Encode(invoice.IssuerNif)}" +
        $"&numserie={Encode(invoice.Number)}" +
        $"&fecha={Encode(VerifactuFormat.Date(invoice.IssueDate))}" +
        $"&importe={Encode(VerifactuFormat.Amount(totalAmount))}";

    private static string Encode(string value) => Uri.EscapeDataString(VerifactuFormat.Text(value));
}
