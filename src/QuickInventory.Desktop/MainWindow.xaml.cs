using System.Windows;
using System.Windows.Media;

namespace QuickInventory.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Título y fondo (el que se ve mientras carga la página) del cliente.
        Title = App.Client.AppName;
        Background = new BrushConverter().ConvertFromString(App.Client.Colors.Background) as Brush;
    }
}
