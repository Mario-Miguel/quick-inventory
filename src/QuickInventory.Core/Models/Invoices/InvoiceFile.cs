namespace QuickInventory.Core.Models;
/// <summary>Factura escaneada (PDF o foto) adjunta a un <see cref="CashMovement"/>.</summary>
public class InvoiceFile
{
    public int Id { get; set; }
    public int CashMovementId { get; set; }
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
}
