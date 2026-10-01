using MudBlazor;

namespace QuickInventory.UI;

public static class Theme
{
    public static readonly MudTheme Museo = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#2F5D2A",          // verde manzana oscuro
            Secondary = "#C8922A",        // ámbar sidra
            AppbarBackground = "#2F5D2A",
            Background = "#FAF7F2",
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
