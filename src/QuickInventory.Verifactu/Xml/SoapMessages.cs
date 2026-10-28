using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace QuickInventory.Verifactu;

/// <summary>Sobres SOAP 1.1 (document/literal) de las dos operaciones del servicio.</summary>
internal static class SoapMessages
{
    private static readonly XNamespace Soap = AeatSchemas.Soap;
    private static readonly XNamespace Sf = AeatSchemas.Information;
    private static readonly XNamespace Lr = AeatSchemas.Submission;
    private static readonly XNamespace Con = AeatSchemas.Query;

    /// <summary>RegFactuSistemaFacturacion: cabecera y de 1 a 1.000 registros ya generados.</summary>
    public static XDocument Registration(InvoiceIssuer issuer, IEnumerable<string> records) => Envelope(
        new XElement(Lr + "RegFactuSistemaFacturacion",
            new XAttribute(XNamespace.Xmlns + "sum", Lr),
            new XAttribute(XNamespace.Xmlns + "sf", Sf),
            new XElement(Lr + "Cabecera", Obligatory(issuer)),
            records.Select(r => new XElement(Lr + "RegistroFactura", XElement.Parse(r)))));

    /// <summary>ConsultaFactuSistemaFacturacion de un mes, desde la página que empieza en <paramref name="pageKey"/>.</summary>
    public static XDocument Query(InvoiceIssuer issuer, int year, int month, InvoiceId? pageKey) => Envelope(
        new XElement(Con + "ConsultaFactuSistemaFacturacion",
            new XAttribute(XNamespace.Xmlns + "con", Con),
            new XAttribute(XNamespace.Xmlns + "sf", Sf),
            new XElement(Con + "Cabecera",
                new XElement(Sf + "IDVersion", "1.0"),
                Obligatory(issuer)),
            new XElement(Con + "FiltroConsulta",
                new XElement(Con + "PeriodoImputacion",
                    new XElement(Sf + "Ejercicio", year.ToString("0000")),
                    new XElement(Sf + "Periodo", month.ToString("00"))),
                pageKey is null ? null : new XElement(Con + "ClavePaginacion",
                    new XElement(Sf + "IDEmisorFactura", pageKey.IssuerNif),
                    new XElement(Sf + "NumSerieFactura", pageKey.Number),
                    new XElement(Sf + "FechaExpedicionFactura", VerifactuFormat.Date(pageKey.IssueDate))))));

    /// <summary>XML en UTF-8 sin BOM, sin sangrías.</summary>
    public static byte[] Serialize(XDocument document)
    {
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false) }))
            document.Save(writer);
        return stream.ToArray();
    }

    private static XElement Obligatory(InvoiceIssuer issuer) => new(Sf + "ObligadoEmision",
        new XElement(Sf + "NombreRazon", VerifactuFormat.Text(issuer.LegalName)),
        new XElement(Sf + "NIF", VerifactuFormat.Text(issuer.Nif)));

    private static XDocument Envelope(XElement body) => new(
        new XDeclaration("1.0", "UTF-8", null),
        new XElement(Soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soapenv", Soap),
            new XElement(Soap + "Header"),
            new XElement(Soap + "Body", body)));
}
