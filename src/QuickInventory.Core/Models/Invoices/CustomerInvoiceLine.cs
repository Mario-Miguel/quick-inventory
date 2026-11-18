namespace QuickInventory.Core.Models;

/// <summary>
/// Línea de una factura a un cliente: un concepto libre ("Entrada escolar", "Visita guiada") con su
/// cantidad, su precio sin IVA y su IVA, o una exención si la operación está exenta.
/// </summary>
public class CustomerInvoiceLine
{
    public int Id { get; set; }
    public int CustomerInvoiceId { get; set; }

    public string Description { get; set; } = "";
    public int Quantity { get; set; } = 1;
    /// <summary>Precio por unidad sin IVA.</summary>
    public decimal UnitPrice { get; set; }
    /// <summary>IVA de la línea, o null si está exenta (<see cref="TaxExemption"/>).</summary>
    public TaxRate? TaxRate { get; set; } = TaxRate.TR2100;
    /// <summary>Causa de exención, o null si lleva IVA.</summary>
    public TaxExemption? TaxExemption { get; set; }

    /// <summary>Base imponible de la línea: <see cref="Quantity"/> × <see cref="UnitPrice"/>, redondeada.</summary>
    public decimal BaseAmount => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);

    public const int MaxDescriptionLength = 200;
    public const int MaxQuantity = 100_000;

    public void Normalize()
    {
        Description = Description?.Trim() ?? "";
        UnitPrice = Math.Round(UnitPrice, 2, MidpointRounding.AwayFromZero);
        // Exenta y con IVA a la vez no puede ser: manda la exención.
        if (TaxExemption is not null) TaxRate = null;
    }

    public void Validate()
    {
        if (Description.Length == 0)
            throw new BusinessRuleException("Escribe el concepto de todas las líneas.");
        if (Description.Length > MaxDescriptionLength)
            throw new BusinessRuleException($"El concepto de una línea no puede tener más de {MaxDescriptionLength} caracteres.");
        if (Quantity < 1)
            throw new BusinessRuleException($"La cantidad de «{Description}» tiene que ser al menos 1.");
        if (Quantity > MaxQuantity)
            throw new BusinessRuleException($"La cantidad de «{Description}» es demasiado grande.");
        if (UnitPrice < 0)
            throw new BusinessRuleException($"El precio de «{Description}» no puede ser negativo.");
        if (TaxRate is null && TaxExemption is null)
            throw new BusinessRuleException($"Elige el IVA de «{Description}» (o la exención, si está exenta).");
    }
}
