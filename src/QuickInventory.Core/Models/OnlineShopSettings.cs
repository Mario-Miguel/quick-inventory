namespace QuickInventory.Core.Models;

/// <summary>
/// Conexión con la tienda online (PrestaShop) donde se venden entradas por internet.
/// Hay una sola fila; se configura desde el perfil Admin.
/// </summary>
public class OnlineShopSettings
{
    public int Id { get; set; }
    /// <summary>Dirección de la tienda, por ejemplo "https://tienda.museodelasidra.com".</summary>
    public string Url { get; set; } = "";
    /// <summary>
    /// Clave del Webservice de PrestaShop. Basta con que tenga permiso de lectura (GET) en "orders".
    /// </summary>
    public string ApiKey { get; set; } = "";
    /// <summary>
    /// Ids de los productos de PrestaShop que son entradas, separados por comas.
    /// Vacío: todos los productos de los pedidos cuentan como entradas.
    /// </summary>
    public string TicketProductIds { get; set; } = "";
    /// <summary>
    /// Con cuántos días de antelación como mucho se compra una entrada. Para ver las visitas
    /// de un mes se leen los pedidos hechos desde ese número de días antes.
    /// </summary>
    public int DaysInAdvance { get; set; } = 120;

    /// <summary>Dirección que en vez de conectarse a la tienda muestra pedidos de ejemplo.</summary>
    public const string DemoUrl = "demo";

    /// <summary>Datos de ejemplo, sin tienda de verdad: no hace falta clave.</summary>
    public bool IsDemo => Url.Trim().Equals(DemoUrl, StringComparison.OrdinalIgnoreCase);

    public bool IsConfigured => IsDemo || (!string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(ApiKey));
}
