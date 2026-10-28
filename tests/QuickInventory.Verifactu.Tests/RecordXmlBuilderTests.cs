using System.Xml.Linq;

namespace QuickInventory.Verifactu.Tests;

public class RecordXmlBuilderTests
{
    private static readonly XNamespace Sf = AeatSchemas.Information;

    [Fact]
    public void Primer_registro_de_alta_cumple_el_esquema()
    {
        var record = Ejemplos.Builder.BuildRegistration(Ejemplos.Ticket(), previous: null, Ejemplos.Ahora);

        var xml = XElement.Parse(record.Xml);
        Assert.Empty(AeatSchemas.Validate(xml));
        Assert.Equal("S", Value(xml, "PrimerRegistro"));
        Assert.Equal("F2", Value(xml, "TipoFactura"));
        Assert.Equal("2.10", Value(xml, "CuotaTotal"));
        Assert.Equal("17.10", Value(xml, "ImporteTotal"));
        Assert.Equal("venta-1", Value(xml, "RefExterna"));
        Assert.Equal("2027-01-15T11:20:30+01:00", Value(xml, "FechaHoraHusoGenRegistro"));
        Assert.Equal(record.Fingerprint, Value(xml, "Huella"));
    }

    [Fact]
    public void La_huella_del_xml_coincide_con_la_calculada_con_sus_valores()
    {
        var previous = new PreviousRecord(new InvoiceId("89890001K", "T2027-000001", new DateOnly(2027, 1, 15)),
            "3C464DAF61ACB827C65FDA19F352A4E3BDC2C640E9E9FC4CC058073F38F12F60");
        var record = Ejemplos.Builder.BuildRegistration(Ejemplos.Ticket("T2027-000002"), previous, Ejemplos.Ahora);

        var xml = XElement.Parse(record.Xml);
        var expected = FingerprintCalculator.ForRegistration(
            Value(xml, "IDEmisorFactura"), Value(xml, "NumSerieFactura"), Value(xml, "FechaExpedicionFactura"),
            Value(xml, "TipoFactura"), Value(xml, "CuotaTotal"), Value(xml, "ImporteTotal"),
            xml.Descendants(Sf + "RegistroAnterior").Single().Element(Sf + "Huella")!.Value,
            Value(xml, "FechaHoraHusoGenRegistro"));
        Assert.Equal(expected, record.Fingerprint);
        Assert.Equal(previous.Fingerprint, xml.Descendants(Sf + "RegistroAnterior").Single().Element(Sf + "Huella")!.Value);
        Assert.Empty(xml.Descendants(Sf + "PrimerRegistro"));
    }

    [Fact]
    public void Cada_registro_encadena_con_el_anterior()
    {
        var first = Ejemplos.Builder.BuildRegistration(Ejemplos.Ticket("T2027-000001"), null, Ejemplos.Ahora);
        var second = Ejemplos.Builder.BuildRegistration(Ejemplos.Ticket("T2027-000002"),
            new PreviousRecord(new InvoiceId("89890001K", "T2027-000001", new DateOnly(2027, 1, 15)), first.Fingerprint),
            Ejemplos.Ahora.AddSeconds(5));

        Assert.NotEqual(first.Fingerprint, second.Fingerprint);
        Assert.Contains(first.Fingerprint, second.Xml);
    }

    [Fact]
    public void Quita_las_fracciones_de_segundo()
    {
        var record = Ejemplos.Builder.BuildRegistration(Ejemplos.Ticket(), null, Ejemplos.Ahora.AddMilliseconds(789));

        Assert.Equal(Ejemplos.Ahora, record.GeneratedAt);
        Assert.Contains("<sf:FechaHoraHusoGenRegistro>2027-01-15T11:20:30+01:00<", record.Xml);
    }

    [Fact]
    public void El_alta_trae_la_url_del_qr() => Assert.Equal(
        "https://prewww2.aeat.es/wlpl/TIKE-CONT/ValidarQR?nif=89890001K&numserie=T2027-000001&fecha=15-01-2027&importe=17.10",
        Ejemplos.Builder.BuildRegistration(Ejemplos.Ticket(), null, Ejemplos.Ahora).QrUrl);

    [Fact]
    public void Linea_exenta_sin_tipo_ni_cuota()
    {
        var xml = XElement.Parse(Ejemplos.Builder.BuildRegistration(Ejemplos.Ticket(), null, Ejemplos.Ahora).Xml);
        var exempt = xml.Descendants(Sf + "DetalleDesglose").Single(d => d.Element(Sf + "OperacionExenta") is not null);

        Assert.Equal("E1", exempt.Element(Sf + "OperacionExenta")!.Value);
        Assert.Null(exempt.Element(Sf + "TipoImpositivo"));
        Assert.Null(exempt.Element(Sf + "CuotaRepercutida"));
        Assert.Null(exempt.Element(Sf + "CalificacionOperacion"));
    }

    [Fact]
    public void Rectificativa_por_diferencias()
    {
        var record = Ejemplos.Builder.BuildRegistration(Ejemplos.Devolucion(), null, Ejemplos.Ahora);

        var xml = XElement.Parse(record.Xml);
        Assert.Empty(AeatSchemas.Validate(xml));
        Assert.Equal("R5", Value(xml, "TipoFactura"));
        Assert.Equal("I", Value(xml, "TipoRectificativa"));
        Assert.Equal("T2027-000001", xml.Descendants(Sf + "IDFacturaRectificada").Single().Element(Sf + "NumSerieFactura")!.Value);
        Assert.Equal("-12.10", Value(xml, "ImporteTotal"));
        Assert.Empty(xml.Descendants(Sf + "ImporteRectificacion"));
    }

    [Fact]
    public void Rectificativa_por_sustitucion_lleva_los_importes_rectificados()
    {
        var invoice = Ejemplos.Devolucion() with
        {
            CorrectionType = CorrectionType.Substitution,
            TaxLines = [TaxLine.Taxed(5m, 21m, 1.05m)],
            RectifiedBase = 10m,
            RectifiedTax = 2.10m,
        };
        var xml = XElement.Parse(Ejemplos.Builder.BuildRegistration(invoice, null, Ejemplos.Ahora).Xml);

        Assert.Equal("S", Value(xml, "TipoRectificativa"));
        Assert.Equal("10.00", Value(xml, "BaseRectificada"));
        Assert.Equal("2.10", Value(xml, "CuotaRectificada"));
    }

    [Fact]
    public void Subsanacion_de_un_alta_que_no_llego_a_la_aeat()
    {
        var invoice = Ejemplos.Ticket() with { IsCorrection = true, PreviousRejection = PreviousRejection.NotInAeat };
        var xml = XElement.Parse(Ejemplos.Builder.BuildRegistration(invoice, null, Ejemplos.Ahora).Xml);

        Assert.Equal("S", Value(xml, "Subsanacion"));
        Assert.Equal("X", Value(xml, "RechazoPrevio"));
    }

    [Fact]
    public void Anulacion()
    {
        var previous = new PreviousRecord(new InvoiceId("89890001K", "T2027-000001", new DateOnly(2027, 1, 15)),
            "F7B94CFD8924EDFF273501B01EE5153E4CE8F259766F88CF6ACB8935802A2B97");
        var cancellation = new CancellationData { Invoice = previous.Invoice };

        var record = Ejemplos.Builder.BuildCancellation(cancellation, previous, Ejemplos.Ahora);

        var xml = XElement.Parse(record.Xml);
        Assert.Equal("RegistroAnulacion", xml.Name.LocalName);
        Assert.Empty(AeatSchemas.Validate(xml));
        Assert.Null(record.QrUrl);
        Assert.Equal(FingerprintCalculator.ForCancellation("89890001K", "T2027-000001", "15-01-2027",
            previous.Fingerprint, "2027-01-15T11:20:30+01:00"), record.Fingerprint);
    }

    [Fact]
    public void El_sistema_informatico_es_solo_verifactu()
    {
        var xml = XElement.Parse(Ejemplos.Builder.BuildRegistration(Ejemplos.Ticket(), null, Ejemplos.Ahora).Xml);

        Assert.Equal("QI", Value(xml, "IdSistemaInformatico"));
        Assert.Equal("S", Value(xml, "TipoUsoPosibleSoloVerifactu"));
        Assert.Equal("N", Value(xml, "TipoUsoPosibleMultiOT"));
        Assert.Equal("N", Value(xml, "IndicadorMultiplesOT"));
    }

    [Fact]
    public void Sistema_para_varios_obligados_tributarios()
    {
        var sistema = Ejemplos.Sistema with { SupportsMultipleTaxpayers = true, HasMultipleTaxpayers = true };
        var xml = XElement.Parse(new RecordXmlBuilder(sistema, AeatEnvironment.Test).BuildRegistration(Ejemplos.Ticket(), null, Ejemplos.Ahora).Xml);

        Assert.Equal("S", Value(xml, "TipoUsoPosibleMultiOT"));
        Assert.Equal("S", Value(xml, "IndicadorMultiplesOT"));
    }

    [Fact]
    public void El_sobre_de_envio_cumple_el_esquema()
    {
        var alta = Ejemplos.Builder.BuildRegistration(Ejemplos.Ticket(), null, Ejemplos.Ahora);
        var anulacion = Ejemplos.Builder.BuildCancellation(
            new CancellationData { Invoice = new InvoiceId("89890001K", "T2027-000001", new DateOnly(2027, 1, 15)) },
            new PreviousRecord(new InvoiceId("89890001K", "T2027-000001", new DateOnly(2027, 1, 15)), alta.Fingerprint),
            Ejemplos.Ahora.AddSeconds(1));

        var envelope = SoapMessages.Registration(Ejemplos.Titular, [alta.Xml, anulacion.Xml]);

        var request = envelope.Root!.Element(AeatSchemas.Soap + "Body")!.Elements().Single();
        Assert.Empty(AeatSchemas.Validate(request));
        Assert.Equal(2, request.Elements(AeatSchemas.Submission + "RegistroFactura").Count());
    }

    [Fact]
    public void El_sobre_de_consulta_cumple_el_esquema()
    {
        var pageKey = new InvoiceId("89890001K", "T2027-000001", new DateOnly(2027, 1, 15));
        var request = SoapMessages.Query(Ejemplos.Titular, 2027, 1, pageKey)
            .Root!.Element(AeatSchemas.Soap + "Body")!.Elements().Single();

        Assert.Empty(AeatSchemas.Validate(request));
        Assert.Equal("01", request.Descendants(Sf + "Periodo").Single().Value);
        Assert.Single(request.Descendants(AeatSchemas.Query + "ClavePaginacion"));
    }

    [Fact]
    public void El_esquema_detecta_un_registro_mal_formado()
    {
        var xml = XElement.Parse(Ejemplos.Builder.BuildRegistration(Ejemplos.Ticket(), null, Ejemplos.Ahora).Xml);
        xml.Element(Sf + "TipoFactura")!.Value = "X9";

        Assert.NotEmpty(AeatSchemas.Validate(xml));
    }

    private static string Value(XElement xml, string name) => xml.Descendants(Sf + name).First().Value;
}
