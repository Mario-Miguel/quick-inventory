namespace QuickInventory.Verifactu.Tests;

public class ResponseParserTests
{
    [Fact]
    public void Envio_correcto()
    {
        var result = ResponseParser.ParseSubmission(Ejemplos.Respuesta("envio-correcto.xml"));

        Assert.Equal(SubmissionStatus.Correct, result.Status);
        Assert.Equal("A-YDSW8NLFLANWPM", result.Csv);
        Assert.Equal(new DateTimeOffset(2027, 1, 15, 11, 20, 31, TimeSpan.FromHours(1)), result.SubmittedAt);
        Assert.Equal(60, result.WaitSeconds);
        Assert.Equal(2, result.Lines.Count);

        var line = result.Lines[0];
        Assert.Equal(new InvoiceId("89890001K", "T2027-000001", new DateOnly(2027, 1, 15)), line.Invoice);
        Assert.Equal(RecordStatus.Correct, line.Status);
        Assert.Equal("venta-1", line.ExternalRef);
        Assert.False(line.IsCancellation);
        Assert.False(line.IsAlreadyRegistered);
        Assert.Null(line.ErrorCode);
    }

    [Fact]
    public void Envio_parcialmente_correcto()
    {
        var result = ResponseParser.ParseSubmission(Ejemplos.Respuesta("envio-parcial.xml"));

        Assert.Equal(SubmissionStatus.PartiallyCorrect, result.Status);
        Assert.Equal(120, result.WaitSeconds);

        var withErrors = result.Lines[0];
        Assert.Equal(RecordStatus.AcceptedWithErrors, withErrors.Status);
        Assert.Equal(2000, withErrors.ErrorCode);
        Assert.Equal(AeatErrorKind.AcceptedWithErrors, ErrorCatalog.KindOf(withErrors.ErrorCode!.Value));

        var rejected = result.Lines[1];
        Assert.True(rejected.IsCancellation);
        Assert.Equal(RecordStatus.Incorrect, rejected.Status);
        Assert.Equal(3002, rejected.ErrorCode);
        Assert.False(rejected.IsAlreadyRegistered);
    }

    [Fact]
    public void Registro_duplicado_cuenta_como_enviado()
    {
        var duplicate = ResponseParser.ParseSubmission(Ejemplos.Respuesta("envio-parcial.xml")).Lines[2];

        Assert.Equal(RecordStatus.Incorrect, duplicate.Status);
        Assert.Equal(3000, duplicate.ErrorCode);
        Assert.True(duplicate.IsAlreadyRegistered);
        Assert.Equal("20270701095959123456", duplicate.Duplicate!.RequestId);
        Assert.Equal(StoredRecordStatus.Correct, duplicate.Duplicate.Status);
    }

    [Fact]
    public void Fault_del_cliente_no_se_reintenta()
    {
        var ex = Assert.Throws<AeatFaultException>(() => ResponseParser.ParseSubmission(Ejemplos.Respuesta("fault-cliente.xml")));

        Assert.True(ex.IsClientError);
        Assert.False(ex.CanRetry);
        Assert.Equal(4104, ex.ErrorCode);
        Assert.Contains("no está identificado", ex.Message);
    }

    [Fact]
    public void Fault_del_servidor_se_reintenta()
    {
        var ex = Assert.Throws<AeatFaultException>(() => ResponseParser.ParseSubmission(Ejemplos.Respuesta("fault-servidor.xml")));

        Assert.False(ex.IsClientError);
        Assert.True(ex.CanRetry);
        Assert.Equal(3501, ex.ErrorCode);
    }

    [Fact]
    public void Respuesta_que_no_es_xml() =>
        Assert.Throws<VerifactuException>(() => ResponseParser.ParseSubmission("<html>Servicio no disponible"));

    [Fact]
    public void Consulta_del_ejemplo_de_la_aeat()
    {
        var result = ResponseParser.ParseQuery(Ejemplos.Respuesta("consulta.xml"));

        Assert.False(result.HasMore);
        Assert.Null(result.NextPageKey);
        var record = Assert.Single(result.Records);
        Assert.Equal(new InvoiceId("XXXXXXXXX", "88", new DateOnly(2024, 11, 27)), record.Invoice);
        Assert.Equal(InvoiceType.F1, record.Type);
        Assert.Equal(16976.37m, record.TotalAmount);
        Assert.Equal("DAA7F72EEDC1AE8A294B7A011EC4A1EC2BE0E4DDB79AF3758377F8D61F38FE6B", record.Fingerprint);
        Assert.Equal(new DateTimeOffset(2024, 11, 27, 11, 54, 10, TimeSpan.FromHours(1)), record.GeneratedAt);
        Assert.Equal("20241113115410360242", record.RequestId);
        // En el ejemplo viene " Correcta ", con espacios.
        Assert.Equal(StoredRecordStatus.Correct, record.Status);
    }

    [Fact]
    public void Consulta_con_mas_paginas()
    {
        var result = ResponseParser.ParseQuery(Ejemplos.Respuesta("consulta-paginada.xml"));

        Assert.True(result.HasMore);
        Assert.Equal(new InvoiceId("89890001K", "T2027-000001", new DateOnly(2027, 1, 15)), result.NextPageKey);
        Assert.Equal(StoredRecordStatus.Cancelled, Assert.Single(result.Records).Status);
    }
}
