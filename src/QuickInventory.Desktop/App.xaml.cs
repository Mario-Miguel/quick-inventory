using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using QuickInventory.Data;
using QuickInventory.UI;

namespace QuickInventory.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Fechas, números y moneda en formato español (1.234,50 €).
        var spanish = new CultureInfo("es-ES");
        CultureInfo.DefaultThreadCurrentCulture = spanish;
        CultureInfo.DefaultThreadCurrentUICulture = spanish;
        CultureInfo.CurrentCulture = spanish;
        CultureInfo.CurrentUICulture = spanish;

        DispatcherUnhandledException += UnhandledErrorHappens;

        var services = ConfigureServices();

        try
        {
            DataConfiguration.InitDatabase(services);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"No se ha podido abrir la base de datos.\n\n{ex.Message}",
                "Museo de la Sidra",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        // La ventana recoge los servicios desde este recurso (ver MainWindow.xaml).
        Resources.Add("services", services);

        new MainWindow().Show();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddWpfBlazorWebView();
#if DEBUG
        // Permite abrir las herramientas de desarrollo con F12 mientras programas.
        services.AddBlazorWebViewDeveloperTools();
#endif

        services.AddMudServices();
        services.AddData(DatabasePath());

        // Perfil activo (Taquilla / Tienda): uno por ventana.
        services.AddScoped<ActiveProfile>();

        // Diálogo "Guardar como" de Windows para los archivos exportados.
        services.AddSingleton<IFileSaver, DesktopFileSaver>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// %LOCALAPPDATA%\QuickInventory\museo.db  — un único archivo con todos los datos.
    /// Para hacer una copia de seguridad basta con copiar ese archivo con la app cerrada.
    /// </summary>
    private static string DatabasePath()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QuickInventory");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "museo.db");
    }

    private void UnhandledErrorHappens(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Ha ocurrido un error inesperado.\n\n{e.Exception.Message}",
            "Museo de la Sidra",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true;
    }
}
