namespace QuickInventory.Core.Models;

public sealed record InvoiceType
{
    private InvoiceType(string value, string description)
    {
        Value = value;
        Description = description;
    }

    public string Value { get; }
    public string Description { get; }

    public static readonly InvoiceType F1 = new("F1", "Factura (art. 6, 7.2 y 7.3 del RD 1619/2012)");
    public static readonly InvoiceType F2 = new("F2", "Factura simplificada y facturas sin identificación del destinatario (art. 6.1.d RD 1619/2012)");
    public static readonly InvoiceType F3 = new("F3", "Factura emitida en sustitución de facturas simplificadas facturadas y declaradas");
    public static readonly InvoiceType R1 = new("R1", "Rectificativa (art. 80.1 y 80.2 y error fundado en derecho)");
    public static readonly InvoiceType R2 = new("R2", "Rectificativa (art. 80.3)");
    public static readonly InvoiceType R3 = new("R3", "Rectificativa (art. 80.4)");
    public static readonly InvoiceType R4 = new("R4", "Rectificativa (resto)");
    public static readonly InvoiceType R5 = new("R5", "Rectificativa en facturas simplificadas");

    public static IReadOnlyList<InvoiceType> All { get; } = [F1, F2, F3, R1, R2, R3, R4, R5];

    public static InvoiceType FromValue(string value) =>
        All.FirstOrDefault(t => t.Value == value)
        ?? throw new InvalidOperationException($"Tipo de factura desconocido: {value}");

    public override string ToString() => Value;
}