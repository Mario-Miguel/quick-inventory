namespace QuickInventory.Verifactu.Tests;

/// <summary>Lo que rechazaría (o aceptaría con errores) la AEAT no llega a generarse.</summary>
public class RecordValidatorTests
{
    [Fact]
    public void Simplificada_de_mas_de_3000_euros() =>
        Rechaza(Ejemplos.Ticket() with { TaxLines = [TaxLine.Taxed(2500m, 21m, 525m)] }, "3000 €");

    [Fact]
    public void Simplificada_de_3000_euros_justos() =>
        Ejemplos.Builder.BuildRegistration(Ejemplos.Ticket() with { TaxLines = [TaxLine.Exempt(3000m, TaxExemption.E1)] }, null, Ejemplos.Ahora);

    [Fact]
    public void Cuota_que_no_corresponde_al_tipo() =>
        Rechaza(Ejemplos.Ticket() with { TaxLines = [TaxLine.Taxed(10m, 21m, 2.50m)] }, "no corresponde");

    [Fact]
    public void Admite_un_centimo_de_redondeo() =>
        // 0,83 × 21 % = 0,1743: la cuota redondeada a partir de un precio con IVA de 1 € es 0,17.
        Ejemplos.Builder.BuildRegistration(Ejemplos.Ticket() with { TaxLines = [TaxLine.Taxed(0.83m, 21m, 0.17m)] }, null, Ejemplos.Ahora);

    [Fact]
    public void Linea_exenta_con_tipo() =>
        Rechaza(Ejemplos.Ticket() with { TaxLines = [new TaxLine { TaxBase = 5m, TaxRate = 21m, TaxAmount = 1.05m, Exemption = TaxExemption.E1 }] }, "exenta");

    [Fact]
    public void Linea_sin_tipo_ni_exencion() =>
        Rechaza(Ejemplos.Ticket() with { TaxLines = [new TaxLine { TaxBase = 5m }] }, "causa de exención");

    [Fact]
    public void Mas_de_12_lineas() =>
        Rechaza(Ejemplos.Ticket() with { TaxLines = Enumerable.Repeat(TaxLine.Exempt(1m, TaxExemption.E1), 13).ToList() }, "12 líneas");

    [Fact]
    public void Sin_lineas() => Rechaza(Ejemplos.Ticket() with { TaxLines = [] }, "12 líneas");

    [Fact]
    public void Importe_con_tres_decimales() =>
        Rechaza(Ejemplos.Ticket() with { TaxLines = [TaxLine.Exempt(1.005m, TaxExemption.E1)] }, "2 decimales");

    [Fact]
    public void Tipo_de_iva_que_no_existe() =>
        Rechaza(Ejemplos.Ticket() with { TaxLines = [TaxLine.Taxed(10m, 16m, 1.60m)] }, "16 %");

    [Fact]
    public void El_5_por_ciento_ya_no_se_admite() =>
        Rechaza(Ejemplos.Ticket() with { TaxLines = [TaxLine.Taxed(10m, 5m, 0.50m)] }, "5 %");

    [Fact]
    public void El_7_5_por_ciento_en_su_trimestre()
    {
        var invoice = Ejemplos.Ticket() with { IssueDate = new DateOnly(2024, 11, 20), TaxLines = [TaxLine.Taxed(10m, 7.5m, 0.75m)] };
        Ejemplos.Builder.BuildRegistration(invoice, null, Ejemplos.Ahora);
    }

    [Fact]
    public void Fecha_futura() => Rechaza(Ejemplos.Ticket() with { IssueDate = new DateOnly(2027, 1, 16) }, "posterior a hoy");

    [Fact]
    public void Fecha_anterior_a_verifactu() => Rechaza(Ejemplos.Ticket() with { IssueDate = new DateOnly(2024, 10, 27) }, "28-10-2024");

    [Fact]
    public void Numero_con_caracteres_prohibidos() => Rechaza(Ejemplos.Ticket("T2027<1"), "número de factura");

    [Fact]
    public void Tipo_de_factura_no_admitido() => Rechaza(Ejemplos.Ticket() with { Type = InvoiceType.F1 }, "F1");

    [Fact]
    public void Simplificada_con_datos_de_rectificacion() =>
        Rechaza(Ejemplos.Ticket() with { CorrectionType = CorrectionType.Differences }, "rectificación");

    [Fact]
    public void Rectificativa_sin_tipo() => Rechaza(Ejemplos.Devolucion() with { CorrectionType = null }, "sustitución o por diferencias");

    [Fact]
    public void Sustitucion_sin_importes_rectificados() =>
        Rechaza(Ejemplos.Devolucion() with { CorrectionType = CorrectionType.Substitution, TaxLines = [TaxLine.Taxed(5m, 21m, 1.05m)] }, "base y la cuota rectificadas");

    [Fact]
    public void Rechazo_previo_sin_subsanacion() =>
        Rechaza(Ejemplos.Ticket() with { PreviousRejection = PreviousRejection.Rejected }, "subsanación");

    [Fact]
    public void Descripcion_vacia() => Rechaza(Ejemplos.Ticket() with { Description = "  " }, "descripción");

    [Fact]
    public void Huella_anterior_en_minusculas()
    {
        var previous = new PreviousRecord(new InvoiceId("89890001K", "T2027-000001", new DateOnly(2027, 1, 15)),
            "3c464daf61acb827c65fda19f352a4e3bdc2c640e9e9fc4cc058073f38f12f60");

        var ex = Assert.Throws<VerifactuValidationException>(() => Ejemplos.Builder.BuildRegistration(Ejemplos.Ticket("T2027-000002"), previous, Ejemplos.Ahora));
        Assert.Contains("huella", ex.Message);
    }

    [Theory]
    [InlineData("Q")]
    [InlineData("Qi")]
    [InlineData("ÑA")]
    public void Identificador_de_sistema_incorrecto(string id)
    {
        var ex = Assert.Throws<VerifactuValidationException>(() => new RecordXmlBuilder(Ejemplos.Sistema with { SystemId = id }, AeatEnvironment.Test));
        Assert.Contains("identificador", ex.Message);
    }

    [Fact]
    public void Varios_obligados_en_un_sistema_que_no_lo_permite()
    {
        var ex = Assert.Throws<VerifactuValidationException>(() => new RecordXmlBuilder(Ejemplos.Sistema with { HasMultipleTaxpayers = true }, AeatEnvironment.Test));
        Assert.Contains("varios obligados", ex.Message);
    }

    private static void Rechaza(InvoiceData invoice, string message)
    {
        var ex = Assert.Throws<VerifactuValidationException>(() => Ejemplos.Builder.BuildRegistration(invoice, null, Ejemplos.Ahora));
        Assert.Contains(message, ex.Message);
    }
}
