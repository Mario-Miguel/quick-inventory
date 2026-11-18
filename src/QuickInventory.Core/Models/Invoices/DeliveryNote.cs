namespace QuickInventory.Core.Models;

/// <summary>
/// Albarán de un <see cref="Provider"/>: la mercancía que nos entrega, con sus <see cref="Lines"/>. Al
/// apuntarlo suma los artículos al stock. No se paga: lo que se paga es la factura, que después lo
/// incluye (<see cref="PurchaseInvoiceId"/>) sin volver a sumar el stock. Puede venir sin precios
/// (sin valorar), y entonces su importe es 0. Lleva siempre el archivo escaneado, aparte en
/// <see cref="InvoiceFile"/>. No se borra: si se apuntó por error, se anula (<see cref="Canceled"/>).
/// </summary>
public class DeliveryNote
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    /// <summary>Se carga al listar, para mostrar el nombre del proveedor.</summary>
    public Provider? Provider { get; set; }
    /// <summary>Número del albarán tal como lo pone el proveedor.</summary>
    public string Number { get; set; } = "";
    /// <summary>Fecha que pone el albarán (sin hora).</summary>
    public DateTime DeliveryDate { get; set; } = DateTime.Today;

    public List<DeliveryNoteLine> Lines { get; set; } = [];

    /// <summary>Suma de las bases de las líneas (0 si viene sin valorar).</summary>
    public decimal BaseAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Amount { get; set; }

    /// <summary>Factura en la que se ha incluido, o null si está pendiente de facturar.</summary>
    public int? PurchaseInvoiceId { get; set; }
    /// <summary>Se carga al listar, para mostrar el número de la factura.</summary>
    public PurchaseInvoice? PurchaseInvoice { get; set; }
    public bool IsInvoiced => PurchaseInvoiceId is not null;

    public string? Notes { get; set; }

    /// <summary>Cuándo se apuntó en la aplicación. La pone el servicio.</summary>
    public DateTime RegistrationDate { get; set; }
    /// <summary>Nombre del archivo del albarán; el contenido está en <see cref="InvoiceFile"/>.</summary>
    public string FileName { get; set; } = "";
    public bool Canceled { get; set; }

    public const int MaxNumberLength = 100;
    public const int MaxNotesLength = 300;

    public IReadOnlyList<PurchaseTaxItem> TaxBreakdown() => PurchaseTaxItem.Breakdown(Lines);

    /// <summary>Recalcula <see cref="BaseAmount"/>, <see cref="TaxAmount"/> y <see cref="Amount"/> a partir de las líneas.</summary>
    public void CalculateTotals()
    {
        var breakdown = TaxBreakdown();
        BaseAmount = breakdown.Sum(i => i.BaseAmount);
        TaxAmount = breakdown.Sum(i => i.TaxAmount);
        Amount = BaseAmount + TaxAmount;
    }

    /// <summary>Resumen de las líneas para la lista: "Botella de sidra × 24, Vaso × 10".</summary>
    public string Summary => string.Join(", ", Lines.Select(l => l.Quantity == 1 ? l.Description : $"{l.Description} x {l.Quantity}"));

    public void Normalize()
    {
        Number = Number?.Trim() ?? "";
        Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();
        DeliveryDate = DeliveryDate.Date;
        foreach (var line in Lines) line.Normalize();
        CalculateTotals();
    }

    public void Validate()
    {
        if (ProviderId <= 0)
            throw new BusinessRuleException("Elige el proveedor del albarán.");
        if (Number.Length == 0)
            throw new BusinessRuleException("Escribe el número del albarán.");
        if (Number.Length > MaxNumberLength)
            throw new BusinessRuleException($"El número del albarán no puede tener más de {MaxNumberLength} caracteres.");
        if (DeliveryDate.Year < 2000)
            throw new BusinessRuleException("La fecha del albarán no es correcta.");
        if (DeliveryDate > DateTime.Today)
            throw new BusinessRuleException("La fecha del albarán no puede ser posterior a hoy.");
        if (Lines.Count == 0)
            throw new BusinessRuleException("Añade lo que viene en el albarán: busca los artículos o añade una línea libre.");
        foreach (var line in Lines) line.Validate();
        if (Notes?.Length > MaxNotesLength)
            throw new BusinessRuleException($"Las notas no pueden tener más de {MaxNotesLength} caracteres.");
    }
}
