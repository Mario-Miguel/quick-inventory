using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;

namespace QuickInventory.Verifactu;

/// <summary>
/// Cliente del servicio web VERI*FACTU: envía registros ya generados y consulta los que tiene la AEAT.
/// La autenticación es TLS con certificado de cliente (cualificado, con clave privada).
/// </summary>
/// <remarks>
/// No reintenta ni espera por su cuenta: quien lo use tiene que respetar <see cref="SubmissionResult.WaitSeconds"/>
/// entre envíos y reintentar más tarde ante <see cref="AeatConnectionException"/> o un Fault del servidor.
/// </remarks>
public sealed class AeatClient(HttpClient http, Uri serviceUrl)
{
    /// <summary>Máximo de registros por envío.</summary>
    public const int MaxRecordsPerSubmission = 1000;

    /// <summary>Cliente con el certificado indicado contra el entorno indicado.</summary>
    /// <param name="sealCertificate">Si es un certificado de sello de entidad, que usa otros servidores.</param>
    public static AeatClient Create(X509Certificate2 certificate, AeatEnvironment environment, bool sealCertificate = false)
    {
        if (!certificate.HasPrivateKey)
            throw new VerifactuException("El certificado no tiene clave privada: no sirve para identificarse ante la AEAT.");

        var handler = new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
        };
        handler.ClientCertificates.Add(certificate);
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(2) };
        return new AeatClient(http, AeatEndpoints.Service(environment, sealCertificate));
    }

    /// <summary>Envía de 1 a 1.000 registros (el XML guardado de cada uno, tal cual se generó).</summary>
    public async Task<SubmissionResult> SendAsync(InvoiceIssuer issuer, IReadOnlyList<string> records, CancellationToken cancellationToken = default)
    {
        if (records.Count is < 1 or > MaxRecordsPerSubmission)
            throw new ArgumentOutOfRangeException(nameof(records), $"Un envío lleva de 1 a {MaxRecordsPerSubmission} registros.");

        var response = await PostAsync(SoapMessages.Registration(issuer, records), cancellationToken);
        return ResponseParser.ParseSubmission(response);
    }

    /// <summary>Una página de los registros de un mes. Para la siguiente, pase <see cref="QueryResult.NextPageKey"/>.</summary>
    public async Task<QueryResult> QueryAsync(InvoiceIssuer issuer, int year, int month, InvoiceId? pageKey = null,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsync(SoapMessages.Query(issuer, year, month, pageKey), cancellationToken);
        return ResponseParser.ParseQuery(response);
    }

    /// <summary>Todos los registros de un mes, pidiendo las páginas que hagan falta.</summary>
    public async IAsyncEnumerable<StoredRecord> QueryMonthAsync(InvoiceIssuer issuer, int year, int month,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        InvoiceId? pageKey = null;
        do
        {
            var page = await QueryAsync(issuer, year, month, pageKey, cancellationToken);
            foreach (var record in page.Records)
                yield return record;
            pageKey = page.HasMore ? page.NextPageKey : null;
        } while (pageKey is not null);
    }

    /// <summary>
    /// POST SOAP 1.1 (text/xml, SOAPAction vacía). Devuelve el cuerpo de la respuesta; los SOAP Fault, que llegan
    /// con HTTP 500, se convierten en <see cref="AeatFaultException"/>.
    /// </summary>
    private async Task<string> PostAsync(XDocument envelope, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, serviceUrl)
        {
            Content = new ByteArrayContent(SoapMessages.Serialize(envelope)),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };
        request.Headers.TryAddWithoutValidation("SOAPAction", "\"\"");

        HttpResponseMessage response;
        string body;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new AeatConnectionException("No se ha podido conectar con la AEAT.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AeatConnectionException("La AEAT ha tardado demasiado en responder.", ex);
        }

        using (response)
        {
            if (ResponseParser.TryParseFault(body) is { } fault)
                throw fault;
            if (!response.IsSuccessStatusCode)
                throw new AeatConnectionException($"La AEAT ha respondido con un error HTTP {(int)response.StatusCode}.");
            return body;
        }
    }
}
