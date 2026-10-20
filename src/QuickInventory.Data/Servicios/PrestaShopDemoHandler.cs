using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace QuickInventory.Data.Services;

/// <summary>
/// Hace de Webservice de PrestaShop con pedidos inventados, para ver la pantalla de entradas online
/// sin conectarse a ninguna tienda. Se usa cuando la dirección de la tienda es "demo".
/// </summary>
/// <remarks>
/// Responde con el mismo JSON que PrestaShop y entiende los parámetros que usa
/// <see cref="PrestaShopTicketService"/> (filter[date_add], sort y limit), así que los datos de ejemplo
/// pasan por la misma lectura que los reales. Los pedidos de cada día salen de un Random con el día como
/// semilla: son siempre los mismos, y solo hay pedidos hasta hoy (las visitas futuras ya están compradas).
/// </remarks>
internal sealed class PrestaShopDemoHandler : HttpMessageHandler
{
    /// <summary>Días hacia atrás desde hoy en los que hay pedidos inventados.</summary>
    private const int DaysWithOrders = 365;

    private static readonly CultureInfo Spanish = new("es-ES");
    private static readonly string[] Hours = ["10:00", "11:00", "12:00", "13:00", "16:00", "17:00", "18:00"];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var query = Query(request.RequestUri!);
        IEnumerable<Order> orders = AllOrders();

        // filter[date_add]=[2026-06-01 00:00:00,2026-10-31 23:59:59]
        if (query.TryGetValue("filter[date_add]", out var interval))
        {
            var limits = interval.Trim('[', ']').Split(',');
            var from = DateTime.ParseExact(limits[0], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var to = DateTime.ParseExact(limits[1], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            orders = orders.Where(o => o.Date >= from && o.Date <= to);
        }
        if (query.GetValueOrDefault("sort") == "[id_DESC]") orders = orders.Reverse();
        if (int.TryParse(query.GetValueOrDefault("limit"), out var limit)) orders = orders.Take(limit);

        var list = orders.ToList();
        // Como PrestaShop: sin resultados devuelve [] en vez de {"orders": []}.
        var json = list.Count == 0 ? "[]" : JsonSerializer.Serialize(new { orders = list.Select(ToJson) });
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
    }

    private static Dictionary<string, string> Query(Uri uri) =>
        uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => p.Length > 1 ? Uri.UnescapeDataString(p[1]) : "");

    private sealed record Line(int ProductId, string Name, int Quantity);
    private sealed record Order(int Id, string Reference, DateTime Date, bool Paid, List<Line> Lines);

    private static List<Order> AllOrders()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var orders = new List<Order>();
        for (var day = today.AddDays(-DaysWithOrders); day <= today; day = day.AddDays(1))
        {
            orders.AddRange(OrdersOf(day).Where(o => o.Date <= DateTime.Now));
        }
        return orders;
    }

    /// <summary>Pedidos hechos un día: más en fin de semana y en verano.</summary>
    private static IEnumerable<Order> OrdersOf(DateOnly day)
    {
        var random = new Random(day.DayNumber);
        var count = random.Next(0, 4)
                    + (day.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday ? 3 : 0)
                    + (day.Month is 7 or 8 ? 3 : 0);

        for (var i = 0; i < count; i++)
        {
            var date = day.ToDateTime(new TimeOnly(random.Next(8, 23), random.Next(0, 60), random.Next(0, 60)));
            yield return new Order(
                Id: day.DayNumber * 100 + i,
                Reference: new string(Enumerable.Range(0, 9).Select(_ => (char)('A' + random.Next(26))).ToArray()),
                Date: date,
                // Alguno queda pendiente de pago (transferencia sin hacer): no debe contar.
                Paid: random.NextDouble() > 0.06,
                Lines: LinesOf(random, day));
        }
    }

    private static List<Line> LinesOf(Random random, DateOnly orderDay)
    {
        // La mayoría compra para los próximos días; algunos con semanas de antelación.
        var visit = orderDay.AddDays(random.NextDouble() < 0.6 ? random.Next(0, 4) : random.Next(4, 60));
        if (visit.DayOfWeek == DayOfWeek.Monday) visit = visit.AddDays(1);   // los lunes cierra
        var hour = Hours[random.Next(Hours.Length)];
        // Varios formatos de fecha y hora, como los que puede tener una tienda real.
        var when = $"Fecha: {visit:dd/MM/yyyy}, Hora: {hour}";

        var lines = new List<Line>();
        var roll = random.NextDouble();
        if (roll < 0.12)
        {
            var guided = Hours[random.Next(1, 3)];
            lines.Add(new(12, $"Visita guiada - Día: {visit.ToString("d 'de' MMMM 'de' yyyy", Spanish)}, Turno: {guided[..2]}h",
                random.Next(2, 9)));
        }
        else
        {
            lines.Add(new(10, $"Entrada general - {when}", random.Next(1, 5)));
            if (random.NextDouble() < 0.45) lines.Add(new(11, $"Entrada reducida - {when}", random.Next(1, 4)));
            if (random.NextDouble() < 0.15) lines.Add(new(10, $"Entrada general - Fecha: {visit:dd/MM/yyyy}, Hora: {hour}, Idioma: Inglés", random.Next(1, 3)));
        }

        // Sin día de visita: salen en el aviso de "no aparece el día".
        if (roll > 0.97) lines.Add(new(13, "Bono regalo 2 entradas", 1));
        // Algo que no es una entrada: se puede quitar indicando los productos (10, 11, 12, 13).
        if (random.NextDouble() < 0.05) lines.Add(new(30, "Camiseta del museo - Talla: M", 1));
        return lines;
    }

    // Igual que PrestaShop: casi todo como texto.
    private static object ToJson(Order order) => new Dictionary<string, object>
    {
        ["id"] = order.Id.ToString(CultureInfo.InvariantCulture),
        ["reference"] = order.Reference,
        ["date_add"] = order.Date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        ["valid"] = order.Paid ? "1" : "0",
        ["associations"] = new
        {
            order_rows = order.Lines.Select((line, i) => new Dictionary<string, string>
            {
                ["id"] = (order.Id * 10 + i).ToString(CultureInfo.InvariantCulture),
                ["product_id"] = line.ProductId.ToString(CultureInfo.InvariantCulture),
                ["product_quantity"] = line.Quantity.ToString(CultureInfo.InvariantCulture),
                ["product_name"] = line.Name,
            }),
        },
    };
}
