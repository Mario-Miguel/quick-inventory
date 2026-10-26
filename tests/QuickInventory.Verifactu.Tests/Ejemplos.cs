namespace QuickInventory.Verifactu.Tests;

/// <summary>Datos de ejemplo compartidos por los tests. 89890001K es un NIF de pruebas de la AEAT.</summary>
internal static class Ejemplos
{
    public static readonly InvoiceIssuer Titular = new("89890001K", "EMPRESA DE PRUEBA");

    public static readonly SystemInfo Sistema = new()
    {
        ProducerName = "EMPRESA DE PRUEBA",
        ProducerNif = "89890001K",
        SystemName = "QuickInventory",
        SystemId = "QI",
        Version = "0.1.0",
        InstallationNumber = "1",
    };

    /// <summary>15-01-2027 a las 11:20:30, hora de invierno.</summary>
    public static readonly DateTimeOffset Ahora = new(2027, 1, 15, 11, 20, 30, TimeSpan.FromHours(1));

    public static readonly RecordXmlBuilder Builder = new(Sistema, AeatEnvironment.Test);

    /// <summary>Ticket de 12,10 € (10 € + 21 % de IVA) y 5 € de una entrada exenta.</summary>
    public static InvoiceData Ticket(string number = "T2027-000001") => new()
    {
        Issuer = Titular,
        Number = number,
        IssueDate = new DateOnly(2027, 1, 15),
        Type = InvoiceType.F2,
        Description = "Venta en tienda",
        TaxLines = [TaxLine.Taxed(10m, 21m, 2.10m), TaxLine.Exempt(5m, TaxExemption.E1)],
        ExternalRef = "venta-1",
    };

    /// <summary>Devolución de la parte con IVA del ticket: rectificativa por diferencias, en negativo.</summary>
    public static InvoiceData Devolucion() => new()
    {
        Issuer = Titular,
        Number = "R2027-000001",
        IssueDate = new DateOnly(2027, 1, 15),
        Type = InvoiceType.R5,
        Description = "Devolución",
        TaxLines = [TaxLine.Taxed(-10m, 21m, -2.10m)],
        CorrectionType = CorrectionType.Differences,
        RectifiedInvoices = [new InvoiceId("89890001K", "T2027-000001", new DateOnly(2027, 1, 15))],
    };

    public static string Respuesta(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Respuestas", name));
}
