using System.Reflection;
using System.Text.RegularExpressions;
using Invoicing.Models;

namespace Invoicing.Templates;

/// <summary>
/// Convierte una plantilla HTML de factura en la factura de verdad, cambiando sus marcadores por los datos.
/// Las disposiciones de la librería son también plantillas (en <c>Templates/</c>), así que una plantilla
/// importada funciona igual y se puede partir de una de ellas.
/// </summary>
/// <remarks>
/// Marcadores (el texto se escapa siempre, así que un dato con «&lt;» no rompe la factura):
/// <list type="bullet">
/// <item><c>{{number}}</c>, <c>{{date}}</c>…: se cambian por el dato (lista completa en <see cref="Placeholders"/>).</item>
/// <item><c>{{#lines}}…{{/lines}}</c> y <c>{{#taxes}}…{{/taxes}}</c>: lo de dentro se repite por cada
/// línea o cada tipo de IVA, con sus propios marcadores.</item>
/// <item><c>{{#dato}}…{{/dato}}</c> con cualquier otro dato: lo de dentro solo sale si el dato no está vacío
/// (p. ej. <c>{{#logo}}&lt;img src="{{logo}}"&gt;{{/logo}}</c>), y <c>{{^dato}}…{{/dato}}</c> solo si lo está.</item>
/// </list>
/// Los nombres en español de las primeras versiones (<c>{{numero}}</c>, <c>{{#lineas}}</c>…) siguen valiendo
/// (<see cref="Aliases"/>), para no romper las plantillas ya importadas.
/// Se quitan los scripts y los atributos <c>on…</c> de la plantilla: una factura no necesita JavaScript.
/// </remarks>
public static partial class InvoiceTemplate
{
    /// <summary>Marcadores que se pueden usar en una plantilla, con lo que ponen. Se enseñan en la pantalla de diseño.</summary>
    public static IReadOnlyList<(string Name, string Description)> Placeholders { get; } =
    [
        ("number", "Número de la factura (FC2026-000012)"),
        ("date", "Fecha de la factura (18/11/2026)"),
        ("issuer.name", "Nombre del emisor (titular)"),
        ("issuer.nif", "NIF del emisor"),
        ("issuer.address", "Dirección del emisor"),
        ("customer.name", "Nombre del cliente"),
        ("customer.nif", "NIF del cliente"),
        ("customer.address", "Dirección del cliente"),
        ("base", "Base imponible total"),
        ("tax", "IVA total"),
        ("total", "Total de la factura"),
        ("notes", "Observaciones de la factura"),
        ("footer", "Pie de página del diseño"),
        ("logo", "Logotipo, para usarlo en <img src=\"{{logo}}\">"),
        ("color.background", "Color de fondo del diseño (#RRGGBB)"),
        ("color.accent", "Color de los títulos del diseño (#RRGGBB)"),
        ("#lines", "Bloque que se repite por cada línea: {{description}}, {{quantity}}, {{price}}, {{tax_rate}}, {{amount}}"),
        ("#taxes", "Bloque que se repite por cada tipo de IVA: {{rate}}, {{base}}, {{tax_amount}}, {{exempt}}"),
    ];

    /// <summary>
    /// Nombres antiguos (en español) y el marcador al que equivalen. Valen igual que los nuevos, también en
    /// <c>{{#…}}</c>, <c>{{^…}}</c> y <c>{{/…}}</c>.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Aliases { get; } = new Dictionary<string, string>
    {
        ["numero"] = "number",
        ["fecha"] = "date",
        ["emisor.nombre"] = "issuer.name",
        ["emisor.nif"] = "issuer.nif",
        ["emisor.direccion"] = "issuer.address",
        ["cliente.nombre"] = "customer.name",
        ["cliente.nif"] = "customer.nif",
        ["cliente.direccion"] = "customer.address",
        ["iva"] = "tax",
        ["observaciones"] = "notes",
        ["pie"] = "footer",
        ["color.fondo"] = "color.background",
        ["color.acento"] = "color.accent",
        ["lineas"] = "lines",
        ["concepto"] = "description",
        ["cantidad"] = "quantity",
        ["precio"] = "price",
        ["tipo_iva"] = "tax_rate",
        ["importe"] = "amount",
        ["impuestos"] = "taxes",
        ["tipo"] = "rate",
        ["cuota"] = "tax_amount",
        ["exenta"] = "exempt",
    };

    private static readonly HashSet<string> LineFields = ["description", "quantity", "price", "tax_rate", "amount"];
    private static readonly HashSet<string> TaxFields = ["rate", "base", "tax_amount", "exempt"];

    /// <summary>Plantilla de una disposición de la librería.</summary>
    public static string Builtin(InvoiceLayout layout)
    {
        var name = $"{typeof(InvoiceTemplate).Namespace}.{layout}.html";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Falta la plantilla {name}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>La plantilla que usa el diseño: la importada o, si no hay, la de su disposición.</summary>
    public static string For(InvoiceDesign design) => design.CustomTemplate ?? Builtin(design.Layout);

    /// <summary>
    /// HTML de la factura con el diseño. <paramref name="defaultAccentColor"/> es el color que se usa si el
    /// diseño no tiene uno (p. ej. el color principal de la aplicación).
    /// </summary>
    public static string Render(CustomerInvoice invoice, InvoiceDesign design, string defaultAccentColor) =>
        Render(For(design), invoice, design, defaultAccentColor);

    public static string Render(string template, CustomerInvoice invoice, InvoiceDesign design, string defaultAccentColor)
    {
        var values = new Dictionary<string, string?>
        {
            ["number"] = Encode(invoice.InvoiceId),
            ["date"] = Encode(invoice.InvoiceDate.ToString("dd/MM/yyyy")),
            ["issuer.name"] = Encode(invoice.IssuerName),
            ["issuer.nif"] = Encode(invoice.IssuerNif),
            ["issuer.address"] = Encode(invoice.IssuerAddress),
            ["customer.name"] = Encode(invoice.CustomerName),
            ["customer.nif"] = Encode(invoice.CustomerNif),
            ["customer.address"] = Encode(invoice.CustomerAddress),
            ["base"] = Encode(invoice.BaseAmount.ToString("C")),
            ["tax"] = Encode(invoice.TaxAmount.ToString("C")),
            ["total"] = Encode(invoice.Amount.ToString("C")),
            ["notes"] = Encode(invoice.Notes),
            ["footer"] = Encode(design.FooterText),
            ["logo"] = design.Logo is null ? null : $"data:{design.LogoContentType};base64,{Convert.ToBase64String(design.Logo)}",
            // Solo se admiten colores "#RRGGBB" (lo comprueba InvoiceDesign), así que no hay forma de colar CSS.
            ["color.background"] = InvoiceDesign.IsColor(design.BackgroundColor) ? design.BackgroundColor : InvoiceDesign.DefaultBackgroundColor,
            ["color.accent"] = InvoiceDesign.IsColor(design.AccentColor) ? design.AccentColor
                : InvoiceDesign.IsColor(defaultAccentColor) ? defaultAccentColor : "#000000",
        };

        var html = Sanitize(ApplyAliases(template));

        html = Block("lines", html, invoice.Lines, l => new Dictionary<string, string?>
        {
            ["description"] = Encode(l.Description),
            ["quantity"] = Encode(l.Quantity.ToString()),
            ["price"] = Encode(l.UnitPrice.ToString("C")),
            ["tax_rate"] = Encode(l.TaxRate?.Name ?? "Exenta"),
            ["amount"] = Encode(l.BaseAmount.ToString("C")),
        }, values);

        html = Block("taxes", html, invoice.TaxBreakdown(), t => new Dictionary<string, string?>
        {
            ["rate"] = Encode(t.TaxRate is null ? $"Exenta ({t.TaxExemption?.Description})" : t.Name),
            ["base"] = Encode(t.TaxBase?.ToString("C")),
            ["tax_amount"] = Encode(t.TaxAmount?.ToString("C")),
            // Para {{#exempt}}…{{/exempt}}: lo que solo tiene sentido en las exentas (o al revés, con {{^exempt}}).
            ["exempt"] = t.TaxRate is null ? "sí" : null,
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
                .Select(n => Aliases.GetValueOrDefault(n, n))
                .Where(n => !known.Contains(n) && !LineFields.Contains(n) && !TaxFields.Contains(n))
                .Distinct()
        ];
    }

    /// <summary>Cambia los nombres antiguos (<see cref="Aliases"/>) por los actuales, también en las secciones.</summary>
    private static string ApplyAliases(string template) =>
        AnyMarkerRegex().Replace(template, m =>
            Aliases.TryGetValue(m.Groups["name"].Value, out var name)
                ? "{{" + m.Groups["kind"].Value + name + "}}"
                : m.Value);

    /// <summary>
    /// Quita lo que podría ejecutar código: scripts, marcos, objetos incrustados y atributos <c>on…</c>.
    /// También las referencias externas (enlaces a hojas de estilo, <c>&lt;base&gt;</c>), que no cargarían igual
    /// en todos los equipos. Es una red de seguridad: las plantillas solo las debería importar un administrador.
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

    [GeneratedRegex(@"\{\{\s*(?<kind>[#^/]?)\s*(?<name>[\w.]+)\s*\}\}")]
    private static partial Regex AnyMarkerRegex();

    [GeneratedRegex(@"<script\b[\s\S]*?</script\s*>|<script\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptRegex();

    [GeneratedRegex(@"</?(iframe|frame|frameset|object|embed|applet|link|meta|base|form)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex DangerousTagRegex();

    [GeneratedRegex(@"\son[a-z]+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase)]
    private static partial Regex EventAttributeRegex();

    [GeneratedRegex(@"javascript\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex JavascriptUrlRegex();
}
