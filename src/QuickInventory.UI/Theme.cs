using MudBlazor;
using QuickInventory.Core.Models;

namespace QuickInventory.UI;

public static class Theme
{
    /// <summary>Tema con los colores del cliente (ver <see cref="ClientSettings"/>).</summary>
    public static MudTheme For(ClientColors colors) => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = colors.Primary,
            Secondary = colors.Secondary,
            AppbarBackground = colors.Primary,
            Background = colors.Background,
            Warning = "#D97706",
        },
        Typography = new Typography
        {
            // Fuente del sistema: no depende de internet.
            Default = new DefaultTypography
            {
                FontFamily = ["Segoe UI", "Arial", "sans-serif"],
            },
            Button = new ButtonTypography
            {
                FontFamily = ["Segoe UI", "Arial", "sans-serif"],
                TextTransform = "none",   // botones en minúsculas: se leen mejor
            },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",
        },
    };
}
