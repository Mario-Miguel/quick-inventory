using System.Security.Cryptography.X509Certificates;

namespace QuickInventory.Verifactu.Tests;

/// <summary>
/// Prueba real contra el entorno de pruebas de la AEAT (no tiene efectos fiscales). Solo se ejecuta si se
/// configuran estas variables de entorno; si no, sale como omitida:
/// <list type="bullet">
/// <item>VERIFACTU_PRUEBAS_CERTIFICADO: ruta del certificado .pfx (con clave privada).</item>
/// <item>VERIFACTU_PRUEBAS_CLAVE: contraseña del .pfx.</item>
/// <item>VERIFACTU_PRUEBAS_NIF y VERIFACTU_PRUEBAS_NOMBRE: el obligado tributario (el titular del certificado
/// o alguien a quien representa).</item>
/// <item>VERIFACTU_PRUEBAS_SELLO=1 si es un certificado de sello de entidad (opcional).</item>
/// </list>
/// </summary>
public class AeatTestEnvironmentTests
{
    [AeatFact]
    public async Task Dos_altas_encadenadas_y_una_anulacion()
    {
        var issuer = new InvoiceIssuer(Variable("NIF")!, Variable("NOMBRE")!);
        using var certificate = X509CertificateLoader.LoadPkcs12FromFile(Variable("CERTIFICADO")!, Variable("CLAVE"));
        var client = AeatClient.Create(certificate, AeatEnvironment.Test, sealCertificate: Variable("SELLO") == "1");

        // Una instalación nueva en cada ejecución: así el primer registro es de verdad el primero de su cadena
        // (si no, la AEAT lo aceptaría con el error 2007).
        var now = SpainTime.Now();
        var run = now.ToString("yyyyMMddHHmmss");
        var builder = new RecordXmlBuilder(Ejemplos.Sistema with
        {
            ProducerName = issuer.LegalName,
            ProducerNif = issuer.Nif,
            InstallationNumber = $"PRUEBA-{run}",
        }, AeatEnvironment.Test);
        var today = DateOnly.FromDateTime(now.DateTime);

        InvoiceData Invoice(int n) => Ejemplos.Ticket($"QI-PRUEBA-{run}-{n}") with { Issuer = issuer, IssueDate = today };

        var first = builder.BuildRegistration(Invoice(1), null, now);
        var second = builder.BuildRegistration(Invoice(2), new PreviousRecord(Invoice(1).Id, first.Fingerprint), now.AddSeconds(1));

        var sent = await client.SendAsync(issuer, [first.Xml, second.Xml]);
        Assert.Equal(SubmissionStatus.Correct, sent.Status);
        Assert.All(sent.Lines, l => Assert.Equal(RecordStatus.Correct, l.Status));
        Assert.False(string.IsNullOrEmpty(sent.Csv));

        // Control de flujo: hay que esperar lo que diga la AEAT antes del siguiente envío.
        await Task.Delay(TimeSpan.FromSeconds(sent.WaitSeconds));

        var cancellation = builder.BuildCancellation(new CancellationData { Invoice = Invoice(2).Id },
            new PreviousRecord(Invoice(2).Id, second.Fingerprint), SpainTime.Now());
        var cancelled = await client.SendAsync(issuer, [cancellation.Xml]);
        Assert.Equal(SubmissionStatus.Correct, cancelled.Status);

        var stored = new List<StoredRecord>();
        await foreach (var record in client.QueryMonthAsync(issuer, today.Year, today.Month))
            if (record.Invoice.Number.StartsWith($"QI-PRUEBA-{run}-"))
                stored.Add(record);
        Assert.Equal(StoredRecordStatus.Correct, stored.Single(r => r.Invoice.Number.EndsWith("-1")).Status);
        Assert.Equal(StoredRecordStatus.Cancelled, stored.Single(r => r.Invoice.Number.EndsWith("-2")).Status);
    }

    private static string? Variable(string name) => Environment.GetEnvironmentVariable($"VERIFACTU_PRUEBAS_{name}");

    /// <summary>[Fact] que se omite si no está configurado el certificado de pruebas.</summary>
    private sealed class AeatFactAttribute : FactAttribute
    {
        public AeatFactAttribute()
        {
            if (new[] { "CERTIFICADO", "NIF", "NOMBRE" }.Any(n => string.IsNullOrEmpty(Variable(n))))
                Skip = "Falta configurar el certificado de pruebas de la AEAT (variables VERIFACTU_PRUEBAS_*).";
        }
    }
}
