using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace QuickInventory.Verifactu;

/// <summary>Espacios de nombres de la AEAT y validación contra sus esquemas (incrustados en la librería).</summary>
public static class AeatSchemas
{
    private const string Base = "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/";

    /// <summary>Tipos comunes: RegistroAlta, RegistroAnulacion, Cabecera...</summary>
    public static readonly XNamespace Information = Base + "SuministroInformacion.xsd";
    /// <summary>Petición de envío (RegFactuSistemaFacturacion).</summary>
    public static readonly XNamespace Submission = Base + "SuministroLR.xsd";
    public static readonly XNamespace SubmissionResponse = Base + "RespuestaSuministro.xsd";
    /// <summary>Petición de consulta (ConsultaFactuSistemaFacturacion).</summary>
    public static readonly XNamespace Query = Base + "ConsultaLR.xsd";
    public static readonly XNamespace QueryResponse = Base + "RespuestaConsultaLR.xsd";
    public static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";

    private static readonly Lazy<XmlSchemaSet> Schemas = new(Load);

    /// <summary>
    /// Valida un elemento (RegistroAlta, RegFactuSistemaFacturacion, ConsultaFactuSistemaFacturacion...)
    /// contra los esquemas. Devuelve los errores; vacío si es válido.
    /// </summary>
    public static IReadOnlyList<string> Validate(XElement element)
    {
        var errors = new List<string>();
        new XDocument(new XElement(element)).Validate(Schemas.Value, (_, e) => errors.Add(e.Message));
        return errors;
    }

    private static XmlSchemaSet Load()
    {
        var set = new XmlSchemaSet { XmlResolver = new EmbeddedResolver() };
        // Los demás (SuministroInformacion, xmldsig) se cargan al resolver sus import.
        foreach (var name in new[] { "SuministroLR.xsd", "RespuestaSuministro.xsd", "ConsultaLR.xsd", "RespuestaConsultaLR.xsd" })
        {
            using var reader = XmlReader.Create(EmbeddedResolver.Open(name), null, EmbeddedResolver.UriFor(name).AbsoluteUri);
            set.Add(null, reader);
        }
        set.Compile();
        return set;
    }

    /// <summary>
    /// Resuelve los schemaLocation con los recursos incrustados, por el nombre del archivo. También el import
    /// de xmldsig, que en el original apunta a una URL del W3C: nunca se descarga nada.
    /// </summary>
    private sealed class EmbeddedResolver : XmlResolver
    {
        public static Uri UriFor(string name) => new($"file:///schemas/{name}");

        public static Stream Open(string name) =>
            typeof(AeatSchemas).Assembly.GetManifestResourceStream($"Schemas.{name}")
            ?? throw new VerifactuException($"Falta el esquema incrustado {name}.");

        public override Uri ResolveUri(Uri? baseUri, string? relativeUri) =>
            UriFor(Path.GetFileName(relativeUri ?? ""));

        public override object GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn) =>
            Open(Path.GetFileName(absoluteUri.AbsolutePath));
    }
}
