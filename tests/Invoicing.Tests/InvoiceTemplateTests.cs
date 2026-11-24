namespace Invoicing.Tests;

public class InvoiceTemplateTests
{
    private static CustomerInvoice Invoice()
    {
        var invoice = new CustomerInvoice
        {
            SeriesCode = "FC2026",
            Number = 12,
            InvoiceDate = new DateTime(2026, 11, 18),
            IssuerName = "Museo",
            IssuerNif = "G33123456",
            CustomerName = "Colegio <Las Vegas> & Cía",
            CustomerNif = "B12345674",
            CustomerAddress = "C/ Mayor 1",
            Lines =
            [
                new() { Description = "Entrada escolar", Quantity = 45, UnitPrice = 4m, TaxRate = TaxRate.TR2100 },
                new() { Description = "Visita guiada", Quantity = 1, UnitPrice = 30m, TaxRate = null, TaxExemption = TaxExemption.E1 },
            ],
        };
        invoice.CalculateTotals();
        return invoice;
    }

    private static string Render(string template, InvoiceDesign? design = null) =>
        InvoiceTemplate.Render(template, Invoice(), design ?? new InvoiceDesign(), "#2F5D2A");

    [Fact]
    public void Cambia_los_marcadores_y_escapa_el_texto()
    {
        var html = Render("<p>{{number}} · {{ date }} · {{customer.name}}</p>");

        Assert.Equal("<p>FC2026-000012 · 18/11/2026 · Colegio &lt;Las Vegas&gt; &amp; Cía</p>", html);
    }

    [Fact]
    public void Repite_los_bloques_de_lineas_e_impuestos()
    {
        var html = Render("{{#lines}}[{{description}} x{{quantity}}]{{/lines}}|{{#taxes}}({{^exempt}}IVA {{/exempt}}{{base}}){{/taxes}}");

        Assert.Equal($"[Entrada escolar x45][Visita guiada x1]|(IVA {180m:C})({30m:C})", html);
    }

    [Fact]
    public void Dentro_de_un_bloque_siguen_valiendo_los_datos_de_la_factura()
    {
        var html = Render("{{#lines}}{{number}};{{/lines}}");

        Assert.Equal("FC2026-000012;FC2026-000012;", html);
    }

    [Fact]
    public void Las_secciones_solo_salen_si_hay_dato()
    {
        var template = "{{#logo}}<img src=\"{{logo}}\">{{/logo}}{{^logo}}sin logo{{/logo}}{{#notes}}Obs{{/notes}}";

        Assert.Equal("sin logo", Render(template));

        var withLogo = new InvoiceDesign { Logo = [1, 2, 3], LogoContentType = "image/png" };
        Assert.Equal("<img src=\"data:image/png;base64,AQID\">", Render(template, withLogo));
    }

    [Fact]
    public void Usa_el_color_del_cliente_si_el_diseno_no_tiene_y_nunca_un_color_invalido()
    {
        Assert.Equal("#2F5D2A #FFFFFF", Render("{{color.accent}} {{color.background}}"));

        var design = new InvoiceDesign { AccentColor = "#112233", BackgroundColor = "red;background:url(x)" };
        Assert.Equal("#112233 #FFFFFF", Render("{{color.accent}} {{color.background}}", design));
    }

    [Fact]
    public void Quita_scripts_y_eventos()
    {
        var html = Render("<script>alert(1)</script><img src=\"x\" onerror=\"alert(1)\"><a href=\"javascript:alert(1)\">a</a><iframe src=\"x\"></iframe>ok");

        Assert.DoesNotContain("script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror", html);
        Assert.DoesNotContain("javascript:", html);
        Assert.DoesNotContain("iframe", html);
        Assert.EndsWith("ok", html);
    }

    [Fact]
    public void Avisa_de_marcadores_desconocidos()
    {
        var unknown = InvoiceTemplate.UnknownPlaceholders("{{number}} {{numbr}} {{#lines}}{{description}}{{/lines}} {{customer.phone}}");

        Assert.Equal(["numbr", "customer.phone"], unknown);
    }

    [Fact]
    public void Los_nombres_antiguos_en_espanol_siguen_valiendo()
    {
        var english = "{{number}} {{date}} {{issuer.name}} {{customer.nif}} {{tax}} {{#notes}}x{{/notes}}"
            + "{{#lines}}[{{description}} {{quantity}} {{price}} {{tax_rate}} {{amount}}]{{/lines}}"
            + "{{#taxes}}({{rate}} {{base}} {{tax_amount}}{{#exempt}} E{{/exempt}}){{/taxes}} {{color.accent}}";
        var spanish = "{{numero}} {{fecha}} {{emisor.nombre}} {{cliente.nif}} {{iva}} {{#observaciones}}x{{/observaciones}}"
            + "{{#lineas}}[{{concepto}} {{cantidad}} {{precio}} {{tipo_iva}} {{importe}}]{{/lineas}}"
            + "{{#impuestos}}({{tipo}} {{base}} {{cuota}}{{#exenta}} E{{/exenta}}){{/impuestos}} {{color.acento}}";

        Assert.Equal(Render(english), Render(spanish));
        Assert.Contains("FC2026-000012", Render(spanish));
        Assert.Empty(InvoiceTemplate.UnknownPlaceholders(spanish));
    }

    [Fact]
    public void Una_plantilla_antigua_de_la_aplicacion_sigue_saliendo_completa()
    {
        // Así eran las plantillas de serie (y las importadas a partir de ellas) antes de pasar a inglés.
        var old = "{{#logo}}<img src=\"{{logo}}\">{{/logo}}<b>{{numero}}</b> {{cliente.nombre}}"
            + "{{#lineas}}<tr><td>{{concepto}}</td><td>{{importe}}</td></tr>{{/lineas}}"
            + "{{#impuestos}}Base {{tipo}} {{base}}{{^exenta}} IVA {{cuota}}{{/exenta}};{{/impuestos}} Total {{total}}{{#pie}}<p>{{pie}}</p>{{/pie}}";

        var html = Render(old, new InvoiceDesign { FooterText = "IBAN ES00" });

        Assert.DoesNotContain("{{", html);
        Assert.Contains("<td>Entrada escolar</td>", html);
        Assert.Contains($"IVA {37.80m:C}", html);
        Assert.Contains("<p>IBAN ES00</p>", html);
    }

    [Theory]
    [InlineData(InvoiceLayout.LogoLeft)]
    [InlineData(InvoiceLayout.LogoCentered)]
    [InlineData(InvoiceLayout.Compact)]
    public void Las_disposiciones_de_la_libreria_son_plantillas_completas(InvoiceLayout layout)
    {
        var template = InvoiceTemplate.Builtin(layout);
        Assert.Empty(InvoiceTemplate.UnknownPlaceholders(template));

        var html = InvoiceTemplate.Render(template, Invoice(), new InvoiceDesign { Layout = layout, FooterText = "IBAN ES00" }, "#2F5D2A");

        Assert.DoesNotContain("{{", html);
        Assert.Contains("FC2026-000012", html);
        Assert.Contains("Visita guiada", html);
        Assert.Contains("IBAN ES00", html);
        Assert.Contains(Invoice().Amount.ToString("C"), html);
    }

    [Fact]
    public void Valida_el_diseno()
    {
        var design = new InvoiceDesign { BackgroundColor = " #ffffff ", AccentColor = "  " };
        design.Normalize();
        design.Validate();
        Assert.Equal("#FFFFFF", design.BackgroundColor);
        Assert.Null(design.AccentColor);

        Assert.Throws<BusinessRuleException>(new InvoiceDesign { BackgroundColor = "blue" }.Validate);
        Assert.Throws<BusinessRuleException>(new InvoiceDesign { Logo = [1], LogoContentType = "image/svg+xml" }.Validate);
    }
}
