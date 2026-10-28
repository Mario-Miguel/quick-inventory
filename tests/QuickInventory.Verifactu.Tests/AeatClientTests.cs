using System.Net;
using System.Text;
using System.Xml.Linq;

namespace QuickInventory.Verifactu.Tests;

/// <summary>El cliente contra un servidor simulado: qué envía y cómo trata cada respuesta.</summary>
public class AeatClientTests
{
    private static readonly Uri Url = new("https://aeat.test/VerifactuSOAP");

    [Fact]
    public async Task Envia_un_sobre_soap_valido()
    {
        var server = new FakeServer(HttpStatusCode.OK, Ejemplos.Respuesta("envio-correcto.xml"));
        var record = Ejemplos.Builder.BuildRegistration(Ejemplos.Ticket(), null, Ejemplos.Ahora);

        var result = await new AeatClient(server.Client, Url).SendAsync(Ejemplos.Titular, [record.Xml]);

        Assert.Equal(SubmissionStatus.Correct, result.Status);
        var request = Assert.Single(server.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("text/xml; charset=utf-8", request.ContentType);
        Assert.Equal("\"\"", request.SoapAction);

        var body = XDocument.Parse(request.Body).Root!.Element(AeatSchemas.Soap + "Body")!.Elements().Single();
        Assert.Empty(AeatSchemas.Validate(body));
        // El registro va tal cual se generó, con su huella.
        Assert.Contains(record.Fingerprint, request.Body);
    }

    [Fact]
    public async Task Rechaza_envios_vacios_o_de_mas_de_1000_registros()
    {
        var client = new AeatClient(new FakeServer(HttpStatusCode.OK, "").Client, Url);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SendAsync(Ejemplos.Titular, []));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SendAsync(Ejemplos.Titular, Enumerable.Repeat("<x/>", 1001).ToList()));
    }

    [Fact]
    public async Task Un_fault_con_http_500_lanza_la_excepcion_de_la_aeat()
    {
        var server = new FakeServer(HttpStatusCode.InternalServerError, Ejemplos.Respuesta("fault-cliente.xml"));
        var record = Ejemplos.Builder.BuildRegistration(Ejemplos.Ticket(), null, Ejemplos.Ahora);

        var ex = await Assert.ThrowsAsync<AeatFaultException>(() => new AeatClient(server.Client, Url).SendAsync(Ejemplos.Titular, [record.Xml]));
        Assert.Equal(4104, ex.ErrorCode);
    }

    [Fact]
    public async Task Un_error_http_sin_soap_se_puede_reintentar()
    {
        var server = new FakeServer(HttpStatusCode.ServiceUnavailable, "<html>Mantenimiento</html>");
        var record = Ejemplos.Builder.BuildRegistration(Ejemplos.Ticket(), null, Ejemplos.Ahora);

        var ex = await Assert.ThrowsAsync<AeatConnectionException>(() => new AeatClient(server.Client, Url).SendAsync(Ejemplos.Titular, [record.Xml]));
        Assert.Contains("503", ex.Message);
    }

    [Fact]
    public async Task Sin_conexion()
    {
        var server = new FakeServer(new HttpRequestException("No such host is known."));
        var record = Ejemplos.Builder.BuildRegistration(Ejemplos.Ticket(), null, Ejemplos.Ahora);

        await Assert.ThrowsAsync<AeatConnectionException>(() => new AeatClient(server.Client, Url).SendAsync(Ejemplos.Titular, [record.Xml]));
    }

    [Fact]
    public async Task La_consulta_de_un_mes_pide_todas_las_paginas()
    {
        var server = new FakeServer(HttpStatusCode.OK, Ejemplos.Respuesta("consulta-paginada.xml"), Ejemplos.Respuesta("consulta.xml"));

        var records = new List<StoredRecord>();
        await foreach (var record in new AeatClient(server.Client, Url).QueryMonthAsync(Ejemplos.Titular, 2027, 1))
            records.Add(record);

        Assert.Equal(2, records.Count);
        Assert.Equal(2, server.Requests.Count);
        Assert.DoesNotContain("ClavePaginacion", server.Requests[0].Body);
        // La segunda petición sigue desde la clave que devolvió la primera.
        Assert.Contains("ClavePaginacion", server.Requests[1].Body);
        Assert.Contains("T2027-000001", server.Requests[1].Body);
    }

    private sealed record Request(HttpMethod Method, string? ContentType, string? SoapAction, string Body);

    /// <summary>Devuelve las respuestas indicadas en orden (la última se repite) y guarda las peticiones.</summary>
    private sealed class FakeServer : HttpMessageHandler
    {
        private readonly HttpStatusCode status;
        private readonly Queue<string> bodies;
        private readonly Exception? error;

        public List<Request> Requests { get; } = [];
        public HttpClient Client => new(this);

        public FakeServer(HttpStatusCode status, params string[] bodies)
        {
            this.status = status;
            this.bodies = new Queue<string>(bodies);
        }

        public FakeServer(Exception error)
        {
            bodies = new Queue<string>();
            this.error = error;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new Request(
                request.Method,
                request.Content?.Headers.ContentType?.ToString(),
                request.Headers.TryGetValues("SOAPAction", out var values) ? values.Single() : null,
                request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));

            if (error is not null) throw error;
            var body = bodies.Count > 1 ? bodies.Dequeue() : bodies.Peek();
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "text/xml") };
        }
    }
}
