using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace QuickInventory.Verifactu;

/// <summary>
/// Lee las respuestas SOAP de la AEAT. Busca los elementos por su nombre, sin mirar el espacio de nombres:
/// los esquemas reparten los campos entre varios y así no se rompe si la AEAT cambia algún prefijo.
/// </summary>
public static partial class ResponseParser
{
    /// <summary>Respuesta a un envío. Lanza <see cref="AeatFaultException"/> si es un SOAP Fault.</summary>
    public static SubmissionResult ParseSubmission(string xml)
    {
        var response = Body(xml, "RespuestaRegFactuSistemaFacturacion");
        var presentation = response.Child("DatosPresentacion");

        return new SubmissionResult(
            Csv: response.Child("CSV")?.Text(),
            SubmittedAt: presentation?.Child("TimestampPresentacion") is { } timestamp ? ParseDateTime(timestamp.Text()) : null,
            WaitSeconds: int.TryParse(response.Child("TiempoEsperaEnvio")?.Text(), out var wait) ? wait : 0,
            Status: response.Required("EstadoEnvio").Text() switch
            {
                "Correcto" => SubmissionStatus.Correct,
                "ParcialmenteCorrecto" => SubmissionStatus.PartiallyCorrect,
                "Incorrecto" => SubmissionStatus.Incorrect,
                var other => throw Unknown("EstadoEnvio", other),
            },
            Lines: response.Children("RespuestaLinea").Select(ParseLine).ToList(),
            ResponseXml: xml);
    }

    /// <summary>Una página de la respuesta a una consulta. Lanza <see cref="AeatFaultException"/> si es un SOAP Fault.</summary>
    public static QueryResult ParseQuery(string xml)
    {
        var response = Body(xml, "RespuestaConsultaFactuSistemaFacturacion");
        var records = response.Children("RegistroRespuestaConsultaFactuSistemaFacturacion").Select(r =>
        {
            var data = r.Child("DatosRegistroFacturacion");
            var state = r.Required("EstadoRegistro");
            return new StoredRecord(
                Invoice: ParseId(r.Required("IDFactura")),
                Type: Enum.TryParse<InvoiceType>(data?.Child("TipoFactura")?.Text(), out var type) ? type : null,
                TotalAmount: data?.Child("ImporteTotal") is { } total ? VerifactuFormat.ParseAmount(total.Text()) : null,
                Fingerprint: data?.Child("Huella")?.Text(),
                GeneratedAt: data?.Child("FechaHoraHusoGenRegistro") is { } generated ? ParseDateTime(generated.Text()) : null,
                RequestId: r.Child("DatosPresentacion")?.Child("IdPeticion")?.Text(),
                Status: ParseStoredStatus(state.Required("EstadoRegistro").Text()),
                ErrorCode: ParseCode(state.Child("CodigoErrorRegistro")),
                ErrorDescription: state.Child("DescripcionErrorRegistro")?.Text());
        }).ToList();

        var nextPage = response.Child("ClavePaginacion");
        return new QueryResult(
            HasMore: response.Child("IndicadorPaginacion")?.Text() == "S",
            Records: records,
            NextPageKey: nextPage is null ? null : ParseId(nextPage));
    }

    private static SubmissionLine ParseLine(XElement line)
    {
        var duplicate = line.Child("RegistroDuplicado");
        return new SubmissionLine(
            Invoice: ParseId(line.Required("IDFactura")),
            IsCancellation: line.Child("Operacion")?.Child("TipoOperacion")?.Text() == "Anulacion",
            ExternalRef: line.Child("RefExterna")?.Text(),
            Status: line.Required("EstadoRegistro").Text() switch
            {
                "Correcto" => RecordStatus.Correct,
                "AceptadoConErrores" => RecordStatus.AcceptedWithErrors,
                "Incorrecto" => RecordStatus.Incorrect,
                var other => throw Unknown("EstadoRegistro", other),
            },
            ErrorCode: ParseCode(line.Child("CodigoErrorRegistro")),
            ErrorDescription: line.Child("DescripcionErrorRegistro")?.Text(),
            Duplicate: duplicate is null ? null : new DuplicateRecord(
                RequestId: duplicate.Child("IdPeticionRegistroDuplicado")?.Text() ?? "",
                Status: ParseStoredStatus(duplicate.Required("EstadoRegistroDuplicado").Text()),
                ErrorCode: ParseCode(duplicate.Child("CodigoErrorRegistro")),
                ErrorDescription: duplicate.Child("DescripcionErrorRegistro")?.Text()));
    }

    /// <summary>Sirve para IDFactura y ClavePaginacion: IDEmisorFactura, NumSerieFactura y FechaExpedicionFactura.</summary>
    private static InvoiceId ParseId(XElement id) => new(
        id.Required("IDEmisorFactura").Text(),
        id.Required("NumSerieFactura").Text(),
        VerifactuFormat.ParseDate(id.Required("FechaExpedicionFactura").Text()));

    /// <summary>Los esquemas usan "Correcta"/"Correcto", "AceptadaConErrores"/"AceptadoConErrores" y "Anulada"/"Anulado".</summary>
    private static StoredRecordStatus ParseStoredStatus(string value) => value switch
    {
        "Correcta" or "Correcto" => StoredRecordStatus.Correct,
        "AceptadaConErrores" or "AceptadoConErrores" => StoredRecordStatus.AcceptedWithErrors,
        "Anulada" or "Anulado" => StoredRecordStatus.Cancelled,
        _ => throw Unknown("EstadoRegistro", value),
    };

    private static int? ParseCode(XElement? element) => int.TryParse(element?.Text(), out var code) ? code : null;

    private static DateTimeOffset ParseDateTime(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);

    /// <summary>Devuelve el elemento esperado dentro del Body, o lanza el Fault si lo hay.</summary>
    private static XElement Body(string xml, string expected)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (XmlException ex)
        {
            throw new VerifactuException("La respuesta de la AEAT no es un XML válido.", ex);
        }

        var body = document.Root?.Child("Body") ?? throw new VerifactuException("La respuesta de la AEAT no es un mensaje SOAP.");
        if (body.Child("Fault") is { } fault)
            throw ParseFault(fault, xml);
        return body.Child(expected) ?? throw new VerifactuException($"La respuesta de la AEAT no trae {expected}.");
    }

    /// <summary>"env:Client" o "env:Server"; el faultstring suele empezar por "Codigo[4104]."</summary>
    internal static AeatFaultException ParseFault(XElement fault, string xml)
    {
        var faultCode = fault.Child("faultcode")?.Text() ?? "";
        var faultString = fault.Child("faultstring")?.Text() ?? "";
        var isClient = faultCode.Split(':')[^1].StartsWith("Client", StringComparison.OrdinalIgnoreCase);
        var match = FaultCodeRegex().Match(faultString);
        return new AeatFaultException(isClient, match.Success ? int.Parse(match.Groups[1].Value) : null, faultString, xml);
    }

    /// <summary>Busca el Fault en una respuesta cualquiera (p. ej. con un error HTTP). Null si no lo hay.</summary>
    internal static AeatFaultException? TryParseFault(string xml)
    {
        try
        {
            var fault = XDocument.Parse(xml).Root?.Child("Body")?.Child("Fault");
            return fault is null ? null : ParseFault(fault, xml);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static VerifactuException Unknown(string field, string value) =>
        new($"La respuesta de la AEAT trae un valor desconocido en {field}: «{value}».");

    [GeneratedRegex(@"Codigo\[(\d+)\]")]
    private static partial Regex FaultCodeRegex();

    private static XElement? Child(this XElement element, string name) =>
        element.Elements().FirstOrDefault(e => e.Name.LocalName == name);

    private static IEnumerable<XElement> Children(this XElement element, string name) =>
        element.Elements().Where(e => e.Name.LocalName == name);

    private static XElement Required(this XElement element, string name) =>
        element.Child(name) ?? throw new VerifactuException($"La respuesta de la AEAT no trae {name}.");

    private static string Text(this XElement element) => element.Value.Trim();
}
