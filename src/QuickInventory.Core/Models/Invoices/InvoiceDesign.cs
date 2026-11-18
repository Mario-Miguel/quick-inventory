using System.Text.RegularExpressions;

namespace QuickInventory.Core.Models;

/// <summary>Disposiciones de la factura que vienen con la aplicación.</summary>
public enum InvoiceLayout
{
    /// <summary>Logotipo y datos del museo a la izquierda; número y fecha a la derecha.</summary>
    LogoLeft,
    /// <summary>Logotipo centrado arriba y una franja de color con el título.</summary>
    LogoCentered,
    /// <summary>Sin cabecera grande y con letra más pequeña: cabe más en la hoja.</summary>
    Compact,
}

/// <summary>
/// Diseño de las facturas a clientes. Hay una sola fila, común a todos los equipos; se configura en
/// Admin → «Diseño de factura». Se puede usar una disposición de la aplicación (<see cref="Layout"/>) con
/// el logotipo, los colores y el pie elegidos, o importar una plantilla HTML propia
/// (<see cref="CustomTemplate"/>), que sustituye a la disposición. Ver <see cref="InvoiceTemplate"/>.
/// </summary>
public partial class InvoiceDesign
{
    public int Id { get; set; }

    public InvoiceLayout Layout { get; set; } = InvoiceLayout.LogoLeft;
    /// <summary>Color del fondo de la hoja, "#RRGGBB".</summary>
    public string BackgroundColor { get; set; } = DefaultBackgroundColor;
    /// <summary>Color de los títulos y las franjas, "#RRGGBB"; null = el color principal del cliente.</summary>
    public string? AccentColor { get; set; }
    /// <summary>Texto fijo al pie de todas las facturas (p. ej. el IBAN para la transferencia).</summary>
    public string? FooterText { get; set; }

    public byte[]? Logo { get; set; }
    /// <summary>"image/png" o "image/jpeg".</summary>
    public string? LogoContentType { get; set; }
    public string? LogoFileName { get; set; }

    /// <summary>Plantilla HTML importada, o null para usar la disposición <see cref="Layout"/>.</summary>
    public string? CustomTemplate { get; set; }
    public string? CustomTemplateFileName { get; set; }

    public bool HasCustomTemplate => CustomTemplate is not null;

    public const string DefaultBackgroundColor = "#FFFFFF";
    public const int MaxFooterLength = 500;
    public const int MaxFileNameLength = 260;
    /// <summary>1 MB: de sobra para un logotipo, y no pesa al abrir cada factura.</summary>
    public const int MaxLogoSize = 1024 * 1024;
    /// <summary>Unos 200 KB de HTML: una plantilla no necesita más (el logotipo va aparte).</summary>
    public const int MaxTemplateLength = 200_000;

    public static IReadOnlyList<string> LogoExtensions { get; } = [".png", ".jpg", ".jpeg"];
    public static IReadOnlyList<string> TemplateExtensions { get; } = [".html", ".htm"];

    /// <summary>Nombre de cada disposición para el usuario.</summary>
    public static string Name(InvoiceLayout layout) => layout switch
    {
        InvoiceLayout.LogoLeft => "Logotipo a la izquierda",
        InvoiceLayout.LogoCentered => "Logotipo centrado",
        InvoiceLayout.Compact => "Compacta",
        _ => layout.ToString(),
    };

    public static bool IsColor(string? value) => value is not null && ColorRegex().IsMatch(value);

    public void Normalize()
    {
        BackgroundColor = BackgroundColor?.Trim().ToUpperInvariant() ?? DefaultBackgroundColor;
        AccentColor = string.IsNullOrWhiteSpace(AccentColor) ? null : AccentColor.Trim().ToUpperInvariant();
        FooterText = string.IsNullOrWhiteSpace(FooterText) ? null : FooterText.Trim();
        if (Logo is null or { Length: 0 })
        {
            Logo = null;
            LogoContentType = null;
            LogoFileName = null;
        }
        if (string.IsNullOrWhiteSpace(CustomTemplate))
        {
            CustomTemplate = null;
            CustomTemplateFileName = null;
        }
    }

    public void Validate()
    {
        if (!Enum.IsDefined(Layout))
            throw new BusinessRuleException("Elige una disposición de la factura.");
        // Van dentro del CSS de la plantilla: solo se admite "#RRGGBB", sin nada más.
        if (!IsColor(BackgroundColor))
            throw new BusinessRuleException("El color de fondo no es correcto.");
        if (AccentColor is not null && !IsColor(AccentColor))
            throw new BusinessRuleException("El color de los títulos no es correcto.");
        if (FooterText?.Length > MaxFooterLength)
            throw new BusinessRuleException($"El pie de página no puede tener más de {MaxFooterLength} caracteres.");
        if (Logo is not null)
        {
            if (Logo.Length > MaxLogoSize)
                throw new BusinessRuleException($"El logotipo ocupa demasiado. El máximo es {MaxLogoSize / 1024 / 1024} MB.");
            if (LogoContentType is not ("image/png" or "image/jpeg"))
                throw new BusinessRuleException("El logotipo tiene que ser una imagen PNG o JPG.");
        }
        if (CustomTemplate?.Length > MaxTemplateLength)
            throw new BusinessRuleException($"La plantilla es demasiado grande. El máximo son {MaxTemplateLength / 1000} KB.");
        if (LogoFileName?.Length > MaxFileNameLength || CustomTemplateFileName?.Length > MaxFileNameLength)
            throw new BusinessRuleException("El nombre del archivo es demasiado largo.");
    }

    public InvoiceDesign Clone() => (InvoiceDesign)MemberwiseClone();

    [GeneratedRegex("^#[0-9A-F]{6}$", RegexOptions.IgnoreCase)]
    private static partial Regex ColorRegex();
}
