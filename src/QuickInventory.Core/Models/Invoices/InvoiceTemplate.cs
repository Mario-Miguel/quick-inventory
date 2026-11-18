using System.Reflection;
using System.Text.RegularExpressions;

namespace QuickInventory.Core.Models;

/// <summary>
/// Convierte una plantilla HTML de factura en la factura de verdad, cambiando sus marcadores por los datos.
/// Las disposiciones de la aplicación son también plantillas (en <c>Invoices/Templates</c>), así que una
/// plantilla importada funciona igual y se puede partir de una de ellas.
/// </summary>
/// <remarks>
/// Marcadores (el texto se escapa siempre, así que un dato con «&lt;» no rompe la factura):
/// <list type="bullet">
/// <item><c>{{numero}}</c>, <c>{{fecha}}</c>…: se cambian por el dato (lista completa en <see cref="Placeholders"/>).</item>
/// <item><c>{{#lineas}}…{{/lineas}}</c> y <c>{{#impuestos}}…{{/impuestos}}</c>: lo de dentro se repite por cada
/// línea o cada tipo de IVA, con sus propios marcadores.</item>
/// <item><c>{{#dato}}…{{/dato}}</c> con cualquier otro dato: lo de dentro solo sale si el dato no está vacío
/// (p. ej. <c>{{#logo}}&lt;img src="{{logo}}"&gt;{{/logo}}</c>).</item>
/// </list>
/// Se quitan los scripts y los atributos <c>on…</c> de la plantilla: una factura no necesita JavaScript.
/// </remarks>
public static partial class InvoiceTemplate
{
    /// <summary>Marcadores que se pueden usar en una plantilla, con lo que ponen. Se enseñan en la pantalla de diseño.</summary>
    public static IReadOnlyList<(string Name, string Description)> Placeholders { get; } =
    [
        ("numero", "Número de la factura (FC2026-000012)"),
        ("fecha", "Fecha de la factura (18/11/2026)"),
        ("emisor.nombre", "Nombre del museo (titular)"),
        ("emisor.nif", "NIF del museo"),
        ("emisor.direccion", "Dirección del museo"),
        ("cliente.nombre", "Nombre del cliente"),
        ("cliente.nif", "NIF del cliente"),
        ("cliente.direccion", "Dirección del cliente"),
        ("base", "Base imponible total"),
        ("iva", "IVA total"),
        ("total", "Total de la factura"),
        ("observaciones", "Observaciones de la factura"),
        ("pie", "Pie de página del diseño"),
        ("logo", "Logotipo, para usarlo en <img src=\"{{logo}}\">"),
        ("color.fondo", "Color de fondo del diseño (#RRGGBB)"),
        ("color.acento", "Color de los títulos del diseño (#RRGGBB)"),
        ("#lineas", "Bloque que se repite por cada línea: {{concepto}}, {{cantidad}}, {{precio}}, {{tipo_iva}}, {{importe}}"),
        ("#impuestos", "Bloque que se repite por cada tipo de IVA: {{tipo}}, {{base}}, {{cuota}}, {{exenta}}"),
    ];

    private static readonly HashSet<string> LineFields = ["concepto", "cantidad", "precio", "tipo_iva", "importe"];
    private static readonly HashSet<string> TaxFields = ["tipo", "base", "cuota", "exenta"];

    /// <summary>Plantilla de una disposición de la aplicación.</summary>
    public static string Builtin(InvoiceLayout layout)
    {
        var name = $"{typeof(InvoiceTemplate).Namespace}.Templates.{layout}.html";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Falta la plantilla {name}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>La plantilla que usa el diseño: la importada o, si no hay, la de su disposición.</summary>
    public static string For(InvoiceDesign design) => design.CustomTemplate ?? Builtin(design.Layout);

    /// <summary>
    /// HTML de la factura con el diseño. <paramref name="defaultAccentColor"/> es el color que se usa si el
    /// diseño no tiene uno (el principal del cliente).
    /// </summary>
    public static string Render(CustomerInvoice invoice, InvoiceDesign design, string defaultAccentColor) =>
        Render(For(design), invoice, design, defaultAccentColor);

    public static string Render(string template, CustomerInvoice invoice, InvoiceDesign design, string defaultAccentColor)
    {
        var values = new Dictionary<string, string?>
        {
            ["numero"] = Encode(invoice.InvoiceId),
            ["fecha"] = Encode(invoice.InvoiceDate.ToString("dd/MM/yyyy")),
            ["emisor.nombre"] = Encode(invoice.IssuerName),
            ["emisor.nif"] = Encode(invoice.IssuerNif),
            ["emisor.direccion"] = Encode(invoice.IssuerAddress),
            ["cliente.nombre"] = Encode(invoice.CustomerName),
            ["cliente.nif"] = Encode(invoice.CustomerNif),
            ["cliente.direccion"] = Encode(invoice.CustomerAddress),
            ["base"] = Encode(invoice.BaseAmount.ToString("C")),
            ["iva"] = Encode(invoice.TaxAmount.ToString("C")),
            ["total"] = Encode(invoice.Amount.ToString("C")),
            ["observaciones"] = Encode(invoice.Notes),
            ["pie"] = Encode(design.FooterText),
            ["logo"] = design.Logo is null ? null : $"data:{design.LogoContentType};base64,{Convert.ToBase64String(design.Logo)}",
            // Solo se admiten colores "#RRGGBB" (lo comprueba InvoiceDesign), así que no hay forma de colar CSS.
            ["color.fondo"] = InvoiceDesign.IsColor(design.BackgroundColor) ? design.BackgroundColor : InvoiceDesign.DefaultBackgroundColor,
            ["color.acento"] = InvoiceDesign.IsColor(design.AccentColor) ? design.AccentColor
                : InvoiceDesign.IsColor(defaultAccentColor) ? defaultAccentColor : "#000000",
        };

        var html = Sanitize(template);

        html = Block("lineas", html, invoice.Lines, l => new Dictionary<string, string?>
        {
            ["concepto"] = Encode(l.Description),
            ["cantidad"] = Encode(l.Quantity.ToString()),
            ["precio"] = Encode(l.UnitPrice.ToString("C")),
            ["tipo_iva"] = Encode(l.TaxRate?.Name ?? "Exenta"),
            ["importe"] = Encode(l.BaseAmount.ToString("C")),
        }, values);

        html = Block("impuestos", html, invoice.TaxBreakdown(), t => new Dictionary<string, string?>
        {
            ["tipo"] = Encode(t.TaxRate is null ? $"Exenta ({t.TaxExemption?.Description})" : t.Name),
            ["base"] = Encode(t.TaxBase?.ToString("C")),
            ["cuota"] = Encode(t.TaxAmount?.ToString("C")),
            // Para {{#exenta}}…{{/exenta}}: lo que solo tiene sentido en las exentas (o al revés, con {{^exenta}}).
            ["exenta"] = t.TaxRate is null ? "sí" : null,
        }, values);

        return Fill(html, values);
    }

    /// <summary>
    /// Marcadores de la plantilla que no existen (seguramente mal escritos). Se avisa al importarla; al
    /// generar la factura se quedan en blanco.
    /// </summary>
    public static IReadOnlyList<string> UnknownPlaceholders(string template)
    {
        var known = Placeholders.Select(p => p.Name.TrimStart('#')).ToHashSet();
        return
        [
            .. PlaceholderRegex().Matches(template)
                .Select(m => m.Groups["name"].Value)
                .Where(n => !known.Contains(n) && !LineFields.Contains(n) && !TaxFields.Contains(n))
                .Distinct()
        ];
    }

    /// <summary>
    /// Quita lo que podría ejecutar código: scripts, marcos, objetos incrustados y atributos <c>on…</c>.
    /// También las referencias externas (enlaces a hojas de estilo, <c>&lt;base&gt;</c>), que no cargarían igual
    /// en todos los equipos. Es una red de seguridad: las plantillas solo las importa el perfil Admin.
    /// </summary>
    public static string Sanitize(string html)
    {
        html = ScriptRegex().Replace(html, "");
        html = DangerousTagRegex().Replace(html, "");
        html = EventAttributeRegex().Replace(html, "");
        html = JavascriptUrlRegex().Replace(html, "");
        return html;
    }

    private static string Block<T>(string name, string html, IEnumerable<T> items,
        Func<T, Dictionary<string, string?>> itemValues, Dictionary<string, string?> outer) =>
        BlockRegex(name).Replace(html, m =>
            string.Concat(items.Select(item =>
            {
                // Dentro del bloque valen sus datos y, si no, los de la factura.
                var values = new Dictionary<string, string?>(outer);
                foreach (var (key, value) in itemValues(item)) values[key] = value;
                return Fill(m.Groups["body"].Value, values);
            })));

    /// <summary>Resuelve las secciones <c>{{#dato}}…{{/dato}}</c> y <c>{{^dato}}…{{/dato}}</c> y después los marcadores sueltos.</summary>
    private static string Fill(string html, Dictionary<string, string?> values)
    {
        string previous;
        // En bucle por si hay secciones dentro de secciones.
        do
        {
            previous = html;
            html = SectionRegex().Replace(html, m =>
            {
                var hasValue = !string.IsNullOrEmpty(values.GetValueOrDefault(m.Groups["name"].Value));
                var show = m.Groups["kind"].Value == "#" ? hasValue : !hasValue;
                return show ? m.Groups["body"].Value : "";
            });
        } while (html != previous);

        return PlaceholderRegex().Replace(html, m => values.GetValueOrDefault(m.Groups["name"].Value) ?? "");
    }

    /// <summary>
    /// Escapa el texto para HTML (solo los caracteres especiales: las tildes se quedan como están) y respeta
    /// los saltos de línea.
    /// </summary>
    private static string? Encode(string? text) =>
        string.IsNullOrEmpty(text) ? null : text
            .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;")
            .Replace("\r\n", "\n").Replace("\n", "<br>");

    private static Regex BlockRegex(string name) =>
        new(@"\{\{#" + Regex.Escape(name) + @"\}\}(?<body>.*?)\{\{/" + Regex.Escape(name) + @"\}\}", RegexOptions.Singleline);

    [GeneratedRegex(@"\{\{(?<kind>[#^])(?<name>[\w.]+)\}\}(?<body>(?:(?!\{\{[#^]).)*?)\{\{/\k<name>\}\}", RegexOptions.Singleline)]
    private static partial Regex SectionRegex();

    [GeneratedRegex(@"\{\{\s*(?<name>[\w.]+)\s*\}\}")]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"<script\b[\s\S]*?</script\s*>|<script\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptRegex();

    [GeneratedRegex(@"</?(iframe|frame|frameset|object|embed|applet|link|meta|base|form)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex DangerousTagRegex();

    [GeneratedRegex(@"\son[a-z]+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase)]
    private static partial Regex EventAttributeRegex();

    [GeneratedRegex(@"javascript\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex JavascriptUrlRegex();
}
