namespace QuickInventory.Core.Models;

/// <summary>
/// Factura completa (<see cref="InvoiceType.F1"/>) que el museo emite a un cliente: colegios,
/// asociaciones, agencias... El número lo pone el servicio con la <see cref="CustomerInvoiceSeries"/> del
/// año de la factura. Los datos del titular (<see cref="Issuer"/>) y del cliente se copian al emitirla,
/// para que la factura no cambie si después se editan. Una vez emitida no se cambia ni se borra.
/// </summary>
public class CustomerInvoice
{
    public int Id { get; set; }

    /// <summary>Código de la serie (p. ej. "FC2026").</summary>
    public string SeriesCode { get; set; } = "";
    public int Number { get; set; }
    /// <summary>Número completo, tal como se imprime: "FC2026-000012".</summary>
    public string InvoiceId => NumberingSeries.FormatInvoiceId(SeriesCode, Number);
    public InvoiceType InvoiceType { get; set; } = InvoiceType.F1;
    /// <summary>Fecha de la factura (sin hora).</summary>
    public DateTime InvoiceDate { get; set; } = DateTime.Today;

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    /// <summary>Copia de los datos del cliente al emitirla.</summary>
    public string CustomerNif { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string CustomerAddress { get; set; } = "";

    /// <summary>Copia de los datos del titular (quien emite) al emitirla.</summary>
    public string IssuerNif { get; set; } = "";
    public string IssuerName { get; set; } = "";
    public string? IssuerAddress { get; set; }

    public List<CustomerInvoiceLine> Lines { get; set; } = [];

    /// <summary>Suma de las bases de las líneas. Se calcula con <see cref="CalculateTotals"/>.</summary>
    public decimal BaseAmount { get; set; }
    /// <summary>Suma de las cuotas de IVA de <see cref="TaxBreakdown"/>.</summary>
    public decimal TaxAmount { get; set; }
    /// <summary>Total de la factura, IVA incluido.</summary>
    public decimal Amount { get; set; }

    /// <summary>Texto libre que sale en la factura (p. ej. "Visita del 12/11/2026, 45 alumnos").</summary>
    public string? Notes { get; set; }
    /// <summary>Cuándo se emitió en la aplicación. La pone el servicio.</summary>
    public DateTime RegistrationDate { get; set; }

    public const int MaxNotesLength = 500;

    /// <summary>Desglose del IVA por tipo (y las exenciones aparte), como sale en la factura.</summary>
    public IReadOnlyList<TaxBreakdownItem> TaxBreakdown() => Models.TaxBreakdown.FromBases(Lines);

    public void CalculateTotals()
    {
        var breakdown = TaxBreakdown();
        BaseAmount = breakdown.Sum(i => i.TaxBase ?? 0);
        TaxAmount = breakdown.Sum(i => i.TaxAmount ?? 0);
        Amount = BaseAmount + TaxAmount;
    }

    /// <summary>Resumen de las líneas para la lista: "Entrada escolar × 45, Visita guiada".</summary>
    public string Summary => string.Join(", ", Lines.Select(l => l.Quantity == 1 ? l.Description : $"{l.Description} × {l.Quantity}"));

    public void Normalize()
    {
        Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();
        InvoiceDate = InvoiceDate.Date;
        foreach (var line in Lines) line.Normalize();
        CalculateTotals();
    }

    public void Validate()
    {
        if (CustomerId <= 0)
            throw new BusinessRuleException("Elige el cliente de la factura.");
        if (InvoiceDate.Year < 2000)
            throw new BusinessRuleException("La fecha de la factura no es correcta.");
        if (InvoiceDate > DateTime.Today)
            throw new BusinessRuleException("La fecha de la factura no puede ser posterior a hoy.");
        if (Lines.Count == 0)
            throw new BusinessRuleException("Añade al menos un concepto a la factura.");
        foreach (var line in Lines) line.Validate();
        if (Amount <= 0)
            throw new BusinessRuleException("El total de la factura tiene que ser mayor que 0.");
        if (Notes?.Length > MaxNotesLength)
            throw new BusinessRuleException($"Las notas no pueden tener más de {MaxNotesLength} caracteres.");
    }
}
