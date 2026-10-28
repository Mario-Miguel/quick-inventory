using System.Windows;
using System.Windows.Media;
using Microsoft.AspNetCore.Components.WebView.Wpf;

namespace QuickInventory.Desktop;

public partial class MainWindow : Window
{
    /// <param name="rootComponent">Componente Blazor que ocupa toda la ventana.</param>
    public MainWindow(Type rootComponent)
    {
        InitializeComponent();
        WebView.RootComponents.Add(new RootComponent { Selector = "#app", ComponentType = rootComponent });

        // Título y fondo (el que se ve mientras carga la página) del cliente.
        Title = App.Client.AppName;
        Background = new BrushConverter().ConvertFromString(App.Client.Colors.Background) as Brush;
    }
}
