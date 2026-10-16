using QuickInventory.Core;
using QuickInventory.Core.Models;

namespace QuickInventory.UI;

/// <summary>Pide al usuario la factura que quiere adjuntar (botón "Adjuntar factura").</summary>
public static class InvoiceUploads
{
    /// <summary>
    /// Abre el selector de archivos y copia en memoria la factura elegida. Devuelve null si
    /// el usuario cancela. Lanza <see cref="BusinessRuleException"/> si no es un tipo aceptado
    /// o es demasiado grande, para avisar antes de enviarla a guardar.
    /// </summary>
    public static async Task<InvoiceFile?> PickAsync(IFilePicker picker)
    {
        var file = await picker.PickAsync("Factura en PDF o foto", InvoiceFile.AllowedExtensions);
        if (file is null) return null;

        var invoice = new InvoiceFile { FileName = file.Name };
        invoice.ContentType = invoice.Extension switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            _ => "image/jpeg",
        };

        if (!InvoiceFile.AllowedExtensions.Contains(invoice.Extension))
            throw new BusinessRuleException("La factura tiene que ser un PDF o una foto (JPG o PNG).");
        if (file.Size > InvoiceFile.MaxSize)
            throw new BusinessRuleException($"La factura ocupa demasiado. El máximo son {InvoiceFile.MaxSize / 1024 / 1024} MB.");

        invoice.Content = await file.ReadAsync();
        return invoice;
    }
}
