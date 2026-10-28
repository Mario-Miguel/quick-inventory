using Velopack;

namespace QuickInventory.Desktop;

/// <summary>Punto de entrada (en lugar del Main que genera App.xaml; ver StartupObject en el .csproj).</summary>
public static class Program
{
    [STAThread]
    public static void Main()
    {
        // Lo primero de todo: al instalar, actualizar o desinstalar, Velopack arranca el .exe con
        // argumentos propios, hace su trabajo aquí y termina sin abrir la ventana.
        VelopackApp.Build().Run();

        new App().Run();
    }
}
