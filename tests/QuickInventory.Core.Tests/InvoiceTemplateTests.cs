using QuickInventory.Core.Models;

namespace QuickInventory.Core.Tests;

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
        var html = Render("<p>{{numero}} · {{ fecha }} · {{cliente.nombre}}</p>");

        Assert.Equal("<p>FC2026-000012 · 18/11/2026 · Colegio &lt;Las Vegas&gt; &amp; Cía</p>", html);
    }

    [Fact]
    public void Repite_los_bloques_de_lineas_e_impuestos()
    {
        var html = Render("{{#lineas}}[{{concepto}} x{{cantidad}}]{{/lineas}}|{{#impuestos}}({{^exenta}}IVA {{/exenta}}{{base}}){{/impuestos}}");

        Assert.Equal($"[Entrada escolar x45][Visita guiada x1]|(IVA {180m:C})({30m:C})", html);
    }

    [Fact]
    public void Dentro_de_un_bloque_siguen_valiendo_los_datos_de_la_factura()
    {
        var html = Render("{{#lineas}}{{numero}};{{/lineas}}");

        Assert.Equal("FC2026-000012;FC2026-000012;", html);
    }

    [Fact]
    public void Las_secciones_solo_salen_si_hay_dato()
    {
        var template = "{{#logo}}<img src=\"{{logo}}\">{{/logo}}{{^logo}}sin logo{{/logo}}{{#observaciones}}Obs{{/observaciones}}";

        Assert.Equal("sin logo", Render(template));

        var withLogo = new InvoiceDesign { Logo = [1, 2, 3], LogoContentType = "image/png" };
        Assert.Equal("<img src=\"data:image/png;base64,AQID\">", Render(template, withLogo));
    }

    [Fact]
    public void Usa_el_color_del_cliente_si_el_diseno_no_tiene_y_nunca_un_color_invalido()
    {
        Assert.Equal("#2F5D2A #FFFFFF", Render("{{color.acento}} {{color.fondo}}"));

        var design = new InvoiceDesign { AccentColor = "#112233", BackgroundColor = "red;background:url(x)" };
        Assert.Equal("#112233 #FFFFFF", Render("{{color.acento}} {{color.fondo}}", design));
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
        var unknown = InvoiceTemplate.UnknownPlaceholders("{{numero}} {{numro}} {{#lineas}}{{concepto}}{{/lineas}} {{cliente.telefono}}");

        Assert.Equal(["numro", "cliente.telefono"], unknown);
    }

    [Theory]
    [InlineData(InvoiceLayout.LogoLeft)]
    [InlineData(InvoiceLayout.LogoCentered)]
    [InlineData(InvoiceLayout.Compact)]
    public void Las_disposiciones_de_la_aplicacion_son_plantillas_completas(InvoiceLayout layout)
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
