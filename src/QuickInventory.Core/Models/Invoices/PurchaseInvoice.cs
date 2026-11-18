namespace QuickInventory.Core.Models;

/// <summary>
/// Factura de compra: la que nos emite un <see cref="Provider"/>, con sus <see cref="Lines"/> (artículos
/// del inventario, líneas libres o líneas copiadas de sus <see cref="DeliveryNotes"/>, cada una con su IVA).
/// Lleva siempre el archivo escaneado, que está aparte en <see cref="InvoiceFile"/> para no cargarlo al listar.
/// No se borra: si se apuntó por error, se anula (<see cref="Canceled"/>), y eso deshace el stock que
/// sumó y deja sus albaranes otra vez pendientes de facturar.
/// </summary>
public class PurchaseInvoice
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    /// <summary>Se carga al listar, para mostrar el nombre del proveedor.</summary>
    public Provider? Provider { get; set; }
    /// <summary>Número de la factura tal como lo pone el proveedor (p. ej. "2026/0153").</summary>
    public string Number { get; set; } = "";
    /// <summary>Fecha que pone la factura (sin hora). Es la que cuenta para la contabilidad y el IVA.</summary>
    public DateTime InvoiceDate { get; set; } = DateTime.Today;

    public List<PurchaseInvoiceLine> Lines { get; set; } = [];

    /// <summary>
    /// Albaranes que incluye la factura. Sus líneas están copiadas en <see cref="Lines"/> (con
    /// <see cref="PurchaseInvoiceLine.DeliveryNoteId"/>), que es lo que cuenta para el importe.
    /// </summary>
    public List<DeliveryNote> DeliveryNotes { get; set; } = [];

    /// <summary>Suma de las bases de las líneas. Se calcula con <see cref="CalculateTotals"/>.</summary>
    public decimal BaseAmount { get; set; }
    /// <summary>Suma de las cuotas de IVA de <see cref="TaxBreakdown"/>.</summary>
    public decimal TaxAmount { get; set; }
    /// <summary>Total de la factura, IVA incluido: <see cref="BaseAmount"/> + <see cref="TaxAmount"/>.</summary>
    public decimal Amount { get; set; }

    /// <summary>Día en que se pagó, o null si está pendiente de pago.</summary>
    public DateTime? PaymentDate { get; set; }
    /// <summary>Cómo se pagó; solo tiene valor si está pagada.</summary>
    public PaymentMethod? PaymentMethod { get; set; }
    /// <summary>
    /// Caja de la que salió el dinero si se pagó en efectivo (siempre sale de una caja). Se resta al
    /// cerrar esa caja el día del pago. Null si se pagó con tarjeta o transferencia.
    /// </summary>
    public SalesPoint? PaymentSalesPoint { get; set; }
    public bool IsPaid => PaymentDate is not null;

    public string? Notes { get; set; }

    /// <summary>Cuándo se apuntó en la aplicación. La pone el servicio.</summary>
    public DateTime RegistrationDate { get; set; }
    /// <summary>Nombre del archivo de la factura; el contenido está en <see cref="InvoiceFile"/>.</summary>
    public string InvoiceFileName { get; set; } = "";
    public bool Canceled { get; set; }

    public const int MaxNumberLength = 100;
    public const int MaxNotesLength = 300;

    /// <summary>Desglose del IVA por tipo, de mayor a menor (ver <see cref="PurchaseTaxItem.Breakdown"/>).</summary>
    public IReadOnlyList<PurchaseTaxItem> TaxBreakdown() => PurchaseTaxItem.Breakdown(Lines);

    /// <summary>Recalcula <see cref="BaseAmount"/>, <see cref="TaxAmount"/> y <see cref="Amount"/> a partir de las líneas.</summary>
    public void CalculateTotals()
    {
        var breakdown = TaxBreakdown();
        BaseAmount = breakdown.Sum(i => i.BaseAmount);
        TaxAmount = breakdown.Sum(i => i.TaxAmount);
        Amount = BaseAmount + TaxAmount;
    }

    /// <summary>
    /// Resumen para la lista: "Botella de sidra × 24, Vaso × 10, Albarán 153". Las líneas de los
    /// albaranes no se repiten: basta con el número del albarán.
    /// </summary>
    public string Summary => string.Join(", ",
        Lines.Where(l => !l.IsFromDeliveryNote).Select(l => l.Quantity == 1 ? l.Description : $"{l.Description} × {l.Quantity}")
            .Concat(DeliveryNotes.Select(n => $"Albarán {n.Number}")));

    public void Normalize()
    {
        Number = Number?.Trim() ?? "";
        Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();
        InvoiceDate = InvoiceDate.Date;
        foreach (var line in Lines) line.Normalize();
        CalculateTotals();
        NormalizePayment();
    }

    /// <summary>Sin fecha de pago no hay forma de pago ni caja, y la fecha va sin hora.</summary>
    public void NormalizePayment()
    {
        PaymentDate = PaymentDate?.Date;
        if (PaymentDate is null) PaymentMethod = null;
        if (PaymentMethod is null) PaymentSalesPoint = null;
    }

    public void Validate()
    {
        if (ProviderId <= 0)
            throw new BusinessRuleException("Elige el proveedor de la factura.");
        if (Number.Length == 0)
            throw new BusinessRuleException("Escribe el número de la factura.");
        if (Number.Length > MaxNumberLength)
            throw new BusinessRuleException($"El número de la factura no puede tener más de {MaxNumberLength} caracteres.");
        if (InvoiceDate.Year < 2000)
            throw new BusinessRuleException("La fecha de la factura no es correcta.");
        if (InvoiceDate > DateTime.Today)
            throw new BusinessRuleException("La fecha de la factura no puede ser posterior a hoy.");
        if (Lines.Count == 0)
            throw new BusinessRuleException("Añade lo que viene en la factura: busca los artículos o los albaranes, o añade una línea libre.");
        foreach (var line in Lines) line.Validate();
        if (Amount <= 0)
            throw new BusinessRuleException("El total de la factura tiene que ser mayor que 0.");
        if (Notes?.Length > MaxNotesLength)
            throw new BusinessRuleException($"Las notas no pueden tener más de {MaxNotesLength} caracteres.");
        ValidatePayment();
    }

    public void ValidatePayment()
    {
        if (PaymentDate is null) return;
        if (PaymentDate.Value.Year < 2000)
            throw new BusinessRuleException("La fecha de pago no es correcta.");
        if (PaymentDate > DateTime.Today)
            throw new BusinessRuleException("La fecha de pago no puede ser posterior a hoy.");
        // En efectivo el dinero sale de una caja; con tarjeta o transferencia no pasa por ninguna.
        switch (PaymentMethod)
        {
            case null:
                throw new BusinessRuleException("Elige cómo se pagó la factura.");
            case Models.PaymentMethod.Cash when PaymentSalesPoint is not (SalesPoint.TicketOffice or SalesPoint.Shop):
                throw new BusinessRuleException("Elige de qué caja salió el dinero: Taquilla o Tienda.");
            case not Models.PaymentMethod.Cash when PaymentSalesPoint is not null:
                throw new BusinessRuleException("Un pago con tarjeta o transferencia no sale de ninguna caja.");
        }
    }
}
