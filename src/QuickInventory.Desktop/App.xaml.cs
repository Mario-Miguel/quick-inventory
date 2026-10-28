using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using QuickInventory.Core;
using QuickInventory.Core.Models;
using QuickInventory.Data;
using QuickInventory.UI;

namespace QuickInventory.Desktop;

public partial class App : Application
{
    /// <summary>Personalización del cliente para el que se ha compilado (ver ClientSettingsLoader).</summary>
    public static ClientSettings Client { get; private set; } = new();

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

        var updater = new VelopackUpdater();
        IServiceProvider services;
        try
        {
            Client = ClientSettingsLoader.Load();
            services = ConfigureServices(LoadConfiguration(), Client, updater);
            DataConfiguration.InitDatabase(services);
        }
        catch (Exception ex)
        {
            // Los errores previstos (sin conexión, contraseña...) ya traen un mensaje para el usuario.
            var reason = ex is BusinessRuleException ? ex.Message : $"No se ha podido abrir la base de datos.\n\n{ex.Message}";
            if (OfferUpdate(updater, reason)) return;
            MessageBox.Show(
                $"{reason}\n\nSi el problema sigue, revisa la conexión configurada en:\n{UserSettingsPath}",
                Client.AppName,
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
    /// <item>appsettings.json junto al .exe (opcional; no está en el repo porque lleva la contraseña).</item>
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

    /// <summary>
    /// Si la app no puede arrancar, puede que la versión nueva lo arregle; sobre todo cuando otro equipo
    /// ya se actualizó y la base es de una versión más nueva. Se ofrece aquí porque sin arrancar no se
    /// llega al botón de la barra superior. Devuelve true si la app se está actualizando (ya se cierra sola).
    /// </summary>
    private static bool OfferUpdate(IAppUpdater updater, string reason)
    {
        if (!updater.IsAvailable) return false;
        try
        {
            // Task.Run: esperar directamente en el hilo de la ventana podría bloquearlo para siempre.
            var check = Task.Run(updater.CheckAsync);
            if (!check.Wait(TimeSpan.FromSeconds(15)) || check.Result is not { } version) return false;

            var answer = MessageBox.Show(
                $"{reason}\n\nHay una versión nueva de la aplicación ({version}) que puede solucionarlo. " +
                "¿Actualizar ahora? Tardará un poco y la aplicación se volverá a abrir sola.",
                Client.AppName,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return false;

            Task.Run(() => updater.UpdateAndRestartAsync()).Wait();
            return true;
        }
        catch (Exception)
        {
            // Sin internet o sin poder descargar: se avisa del error original como siempre.
            return false;
        }
    }

    private static IServiceProvider ConfigureServices(IConfiguration configuration, ClientSettings client, IAppUpdater updater)
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
        services.AddSingleton(client);
        services.AddSingleton(updater);
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
            Client.AppName,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true;
    }
}
