using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuickInventory.Core.Models;
using QuickInventory.Core.Services;

namespace QuickInventory.Data.Services;

/// <summary>
/// Lee los pedidos de la tienda online con el Webservice de PrestaShop (API REST, solo lectura).
/// La fecha y la hora de la visita salen del nombre de cada línea (ver <see cref="PrestaShopProductName"/>).
/// </summary>
public sealed class PrestaShopTicketService(IDbContextFactory<BaseDbContext> factory) : IOnlineTicketService
{
    private static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(30) };
    /// <summary>Con la dirección "demo": pedidos de ejemplo, sin salir a internet.</summary>
    private static readonly HttpClient demo = new(new PrestaShopDemoHandler());

    public async Task<OnlineShopSettings> GetSettingsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.OnlineShopSettings.AsNoTracking().FirstOrDefaultAsync() ?? new OnlineShopSettings();
    }

    public async Task SaveSettingsAsync(OnlineShopSettings settings)
    {
        Validate(settings);

        await using var db = await factory.CreateDbContextAsync();
        var saved = await db.OnlineShopSettings.FirstOrDefaultAsync();
        if (saved is null)
        {
            saved = new OnlineShopSettings();
            db.OnlineShopSettings.Add(saved);
        }
        saved.Url = settings.IsDemo ? OnlineShopSettings.DemoUrl : NormalizeUrl(settings.Url);
        saved.ApiKey = settings.ApiKey.Trim();
        saved.TicketProductIds = string.Join(", ", ProductIds(settings));
        saved.DaysInAdvance = settings.DaysInAdvance;
        await db.SaveChangesAsync();
    }

    public async Task<List<OnlineTicket>> TestConnectionAsync(OnlineShopSettings settings, int orders = 10)
    {
        Validate(settings);
        return await ReadTicketsAsync(settings, $"orders?display=full&sort={Escape("[id_DESC]")}&limit={orders}");
    }

    public async Task<List<OnlineTicket>> GetTicketsAsync(DateOnly from, DateOnly to)
    {
        var settings = await GetSettingsAsync();
        if (!settings.IsConfigured)
        {
            throw new BusinessRuleException(
                "Falta configurar la conexión con la tienda online. Se hace desde el perfil Admin, en «Tienda online».");
        }

        // Una entrada para un día se compra antes de ese día: se leen los pedidos desde
        // DaysInAdvance días antes del primer día hasta el último.
        var ordersFrom = from.ToDateTime(TimeOnly.MinValue).AddDays(-settings.DaysInAdvance);
        var ordersTo = to.ToDateTime(TimeOnly.MaxValue);
        var interval = $"[{ordersFrom.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)},"
                       + $"{ordersTo.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}]";

        var tickets = await ReadTicketsAsync(settings,
            $"orders?display=full&date=1&filter[date_add]={Escape(interval)}&sort={Escape("[id_ASC]")}");

        var result = tickets
            .Where(t => t.Paid)
            .Where(t => (t.VisitDay ?? DateOnly.FromDateTime(t.OrderDate)) is var day && day >= from && day <= to)
            .ToList();
        if (result.Count == 0) return result;

        var orderIds = result.Select(t => t.OrderId).Distinct().ToList();
        await using var db = await factory.CreateDbContextAsync();
        var checkIns = await db.OnlineTicketCheckIns.AsNoTracking()
            .Where(c => orderIds.Contains(c.OrderId))
            .ToDictionaryAsync(c => (c.OrderId, c.OrderLineId), c => c.CheckedInAt);
        foreach (var ticket in result)
        {
            ticket.CheckedInAt = checkIns.TryGetValue((ticket.OrderId, ticket.OrderLineId), out var at) ? at : null;
        }
        return result;
    }

    public async Task SetCheckedInAsync(IReadOnlyCollection<OnlineTicket> tickets, bool checkedIn)
    {
        if (tickets.Count == 0) return;
        var keys = tickets.Select(t => (t.OrderId, t.OrderLineId)).ToHashSet();
        var orderIds = keys.Select(k => k.OrderId).Distinct().ToList();

        await using var db = await factory.CreateDbContextAsync();
        var saved = (await db.OnlineTicketCheckIns.Where(c => orderIds.Contains(c.OrderId)).ToListAsync())
            .Where(c => keys.Contains((c.OrderId, c.OrderLineId)))
            .ToList();

        var now = DateTime.Now;
        if (checkedIn)
        {
            var existing = saved.Select(c => (c.OrderId, c.OrderLineId)).ToHashSet();
            db.OnlineTicketCheckIns.AddRange(keys.Where(k => !existing.Contains(k))
                .Select(k => new OnlineTicketCheckIn { OrderId = k.OrderId, OrderLineId = k.OrderLineId, CheckedInAt = now }));
        }
        else
        {
            db.OnlineTicketCheckIns.RemoveRange(saved);
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new BusinessRuleException(
                "Otro ordenador estaba marcando estas mismas entradas. Pulsa «Actualizar» y vuelve a intentarlo.");
        }

        var times = saved.ToDictionary(c => (c.OrderId, c.OrderLineId), c => c.CheckedInAt);
        foreach (var ticket in tickets)
        {
            ticket.CheckedInAt = checkedIn ? times.GetValueOrDefault((ticket.OrderId, ticket.OrderLineId), now) : null;
        }
    }

    private static async Task<List<OnlineTicket>> ReadTicketsAsync(OnlineShopSettings settings, string query)
    {
        using var json = await GetAsync(settings, query);
        var ticketProducts = ProductIds(settings);
        var tickets = new List<OnlineTicket>();

        if (!TryGetList(json, "orders", out var orders)) return tickets;

        var names = await ReadClientNamesAsync(settings,
            orders.EnumerateArray().Select(o => Number(o, "id_customer")).Where(id => id > 0).ToHashSet());

        foreach (var order in orders.EnumerateArray())
        {
            if (!order.TryGetProperty("associations", out var associations)
                || !associations.TryGetProperty("order_rows", out var rows)
                || rows.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var orderDate = DateTime.TryParseExact(Text(order, "date_add"), "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : DateTime.MinValue;

            foreach (var row in rows.EnumerateArray())
            {
                var productId = Number(row, "product_id");
                if (ticketProducts.Count > 0 && !ticketProducts.Contains(productId)) continue;

                var name = Text(row, "product_name");
                var (type, day, time) = PrestaShopProductName.Parse(name, orderDate);
                tickets.Add(new OnlineTicket
                {
                    OrderId = Number(order, "id"),
                    OrderReference = Text(order, "reference"),
                    ClientName = names.GetValueOrDefault(Number(order, "id_customer"), ""),
                    OrderDate = orderDate,
                    OrderLineId = Number(row, "id"),
                    Paid = Text(order, "valid") is "1" or "true" or "True",
                    ProductId = productId,
                    ProductName = name,
                    Type = type,
                    VisitDay = day,
                    VisitTime = time,
                    Quantity = Number(row, "product_quantity"),
                });
            }
        }
        return tickets;
    }

    /// <summary>Clientes por los que se pregunta en cada petición, para que la dirección no sea muy larga.</summary>
    private const int ClientsPerRequest = 50;

    /// <summary>
    /// Nombre y apellidos de los clientes de PrestaShop, por id. Si la clave no tiene permiso para ver los
    /// clientes (customers → GET) devuelve los que haya podido leer: las entradas se siguen viendo, sin el nombre.
    /// </summary>
    private static async Task<Dictionary<int, string>> ReadClientNamesAsync(OnlineShopSettings settings, IReadOnlyCollection<int> ids)
    {
        var names = new Dictionary<int, string>();
        foreach (var chunk in ids.Order().Chunk(ClientsPerRequest))
        {
            JsonDocument json;
            try
            {
                json = await GetAsync(settings,
                    $"customers?display={Escape("[id,firstname,lastname]")}&filter[id]={Escape($"[{string.Join('|', chunk)}]")}");
            }
            catch (BusinessRuleException)
            {
                return names;
            }

            using (json)
            {
                if (!TryGetList(json, "customers", out var customers)) continue;
                foreach (var customer in customers.EnumerateArray())
                {
                    var name = $"{Text(customer, "firstname").Trim()} {Text(customer, "lastname").Trim()}".Trim();
                    if (name.Length > 0) names[Number(customer, "id")] = name;
                }
            }
        }
        return names;
    }

    /// <summary>Lista de un recurso ({"orders": [...]}). Sin resultados, PrestaShop devuelve [] en vez de la lista vacía.</summary>
    private static bool TryGetList(JsonDocument json, string resource, out JsonElement list)
    {
        list = default;
        return json.RootElement.ValueKind == JsonValueKind.Object
               && json.RootElement.TryGetProperty(resource, out list)
               && list.ValueKind == JsonValueKind.Array;
    }

    private static async Task<JsonDocument> GetAsync(OnlineShopSettings settings, string query)
    {
        // La clave va como ws_key en vez de en la cabecera Authorization: algunos hostings la eliminan.
        var url = $"{NormalizeUrl(settings.Url)}/api/{query}&output_format=JSON&ws_key={Escape(settings.ApiKey.Trim())}";

        HttpResponseMessage response;
        try
        {
            response = await (settings.IsDemo ? demo : http).GetAsync(url);
        }
        catch (HttpRequestException)
        {
            throw new BusinessRuleException(
                "No se ha podido conectar con la tienda online. Comprueba que hay internet y que la dirección es correcta.");
        }
        catch (TaskCanceledException)
        {
            throw new BusinessRuleException("La tienda online tarda demasiado en responder. Inténtalo de nuevo en un rato.");
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                    throw new BusinessRuleException("La tienda online no acepta la clave de la API. Comprueba que está bien copiada.");
                case HttpStatusCode.Forbidden:
                    throw new BusinessRuleException("La clave de la API no tiene permiso para ver los pedidos (orders → GET).");
                case HttpStatusCode.NotFound:
                    throw new BusinessRuleException(
                        "No se encuentra la API en esa dirección. Comprueba la dirección y que el Webservice está activado en PrestaShop.");
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new BusinessRuleException($"La tienda online ha respondido con un error ({(int)response.StatusCode}).");
            }

            try
            {
                return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            }
            catch (JsonException)
            {
                throw new BusinessRuleException(
                    "La tienda online no ha devuelto datos válidos. Comprueba que la dirección es la de la tienda.");
            }
        }
    }

    private static void Validate(OnlineShopSettings settings)
    {
        if (!Uri.TryCreate(NormalizeUrl(settings.Url), UriKind.Absolute, out var uri) || uri.Host.Length == 0)
        {
            throw new BusinessRuleException("Escribe la dirección de la tienda online, por ejemplo https://tienda.ejemplo.com.");
        }
        if (!settings.IsDemo && string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new BusinessRuleException("Escribe la clave de la API de PrestaShop.");
        }
        if (settings.DaysInAdvance is < 1 or > 730)
        {
            throw new BusinessRuleException("Los días de antelación tienen que estar entre 1 y 730.");
        }
        ProductIds(settings);
    }

    /// <summary>"12, 15 ,20" → {12, 15, 20}. Vacío si no se ha indicado ninguno.</summary>
    private static SortedSet<int> ProductIds(OnlineShopSettings settings)
    {
        var ids = new SortedSet<int>();
        foreach (var part in settings.TicketProductIds.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(part, out var id) || id <= 0)
            {
                throw new BusinessRuleException($"«{part}» no es un número de producto. Escribe los números separados por comas.");
            }
            ids.Add(id);
        }
        return ids;
    }

    /// <summary>Acepta "tienda.com", "https://tienda.com/" o "https://tienda.com/api" y deja "https://tienda.com".</summary>
    private static string NormalizeUrl(string url)
    {
        url = url.Trim().TrimEnd('/');
        if (url.EndsWith("/api", StringComparison.OrdinalIgnoreCase)) url = url[..^4];
        if (url.Length > 0 && !url.Contains("://")) url = "https://" + url;
        return url;
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);

    // PrestaShop devuelve casi todo como texto ("12"), pero según la versión algunos campos llegan como número.
    private static string Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString()
            : "";

    private static int Number(JsonElement element, string property) =>
        int.TryParse(Text(element, property), out var number) ? number : 0;
}
