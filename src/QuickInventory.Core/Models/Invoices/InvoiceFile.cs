namespace QuickInventory.Core.Models;
/// <summary>
/// Factura escaneada (PDF o foto). Pertenece a un <see cref="CashMovement"/> o a una
/// <see cref="PurchaseInvoice"/> (facturas de proveedores): siempre a uno de los dos y solo a uno.
/// </summary>
public class InvoiceFile
{
    public int Id { get; set; }
    public int? CashMovementId { get; set; }
    public int? PurchaseInvoiceId { get; set; }
    /// <summary>Nombre original del archivo, por ejemplo "factura-ferreteria.pdf".</summary>
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public byte[] Content { get; set; } = [];

    /// <summary>Tamaño máximo de una factura: 10 MB, de sobra para un PDF o una foto del móvil.</summary>
    public const long MaxSize = 10 * 1024 * 1024;

    /// <summary>Tipos de archivo que se aceptan como factura.</summary>
    public static IReadOnlyList<string> AllowedExtensions { get; } = [".pdf", ".jpg", ".jpeg", ".png"];

    /// <summary>Extensión del archivo en minúsculas, por ejemplo ".pdf".</summary>
    public string Extension => Path.GetExtension(FileName).ToLowerInvariant();

    /// <summary>Comprueba el tipo y el tamaño antes de guardarla.</summary>
    public void Validate()
    {
        if (!AllowedExtensions.Contains(Extension))
            throw new BusinessRuleException("La factura tiene que ser un PDF o una foto (JPG o PNG).");
        if (Content.Length == 0)
            throw new BusinessRuleException("El archivo de la factura está vacío.");
        if (Content.Length > MaxSize)
            throw new BusinessRuleException($"La factura ocupa demasiado. El máximo son {MaxSize / 1024 / 1024} MB.");
    }
}
