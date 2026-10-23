using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using QuickInventory.Core;
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

        IServiceProvider services;
        try
        {
            services = ConfigureServices(LoadConfiguration());
            DataConfiguration.InitDatabase(services);
        }
        catch (Exception ex)
        {
            // Los errores previstos (sin conexión, contraseña...) ya traen un mensaje para el usuario.
            var reason = ex is BusinessRuleException ? ex.Message : $"No se ha podido abrir la base de datos.\n\n{ex.Message}";
            MessageBox.Show(
                $"{reason}\n\nSi el problema sigue, revisa la conexión configurada en:\n{UserSettingsPath}",
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

    /// <summary>
    /// Configuración por capas; cada una sustituye a la anterior:
    /// <list type="number">
    /// <item>appsettings.json junto al .exe (PostgreSQL local de desarrollo).</item>
    /// <item>%LOCALAPPDATA%\QuickInventory\appsettings.json, para la conexión de cada equipo.</item>
    /// <item>Variables de entorno QUICKINVENTORY_..., p. ej. QUICKINVENTORY_ConnectionStrings__Museo.</item>
    /// </list>
    /// </summary>
    private static IConfiguration LoadConfiguration() => new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true)
        .AddJsonFile(UserSettingsPath, optional: true)
        .AddEnvironmentVariables("QUICKINVENTORY_")
        .Build();

    private static string UserSettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QuickInventory",
        "appsettings.json");

    private static IServiceProvider ConfigureServices(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Museo");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new BusinessRuleException("No hay ninguna base de datos configurada.");

        var services = new ServiceCollection();

        services.AddWpfBlazorWebView();
#if DEBUG
        // Permite abrir las herramientas de desarrollo con F12 mientras programas.
        services.AddBlazorWebViewDeveloperTools();
#endif

        services.AddMudServices();
        services.AddData(connectionString);

        // Perfil activo (Taquilla / Tienda): uno por ventana.
        services.AddScoped<ActiveProfile>();

        // Diálogo "Guardar como" de Windows para los archivos exportados.
        services.AddSingleton<IFileSaver, DesktopFileSaver>();
        // Diálogo "Abrir" de Windows para adjuntar archivos (facturas).
        services.AddSingleton<IFilePicker, DesktopFilePicker>();

        return services.BuildServiceProvider();
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
