namespace QuickInventory.Verifactu;

/// <summary>Direcciones de la AEAT (de SistemaFacturacion.wsdl y de la especificación del QR).</summary>
public static class AeatEndpoints
{
    /// <summary>
    /// Servicio VERI*FACTU (envío y consulta). Con certificado de sello de entidad se usan los servidores
    /// www10/prewww10; con el resto (representante, persona física), www1/prewww1.
    /// </summary>
    public static Uri Service(AeatEnvironment environment, bool sealCertificate) => new((environment, sealCertificate) switch
    {
        (AeatEnvironment.Production, false) => "https://www1.agenciatributaria.gob.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP",
        (AeatEnvironment.Production, true) => "https://www10.agenciatributaria.gob.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP",
        (_, false) => "https://prewww1.aeat.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP",
        (_, true) => "https://prewww10.aeat.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP",
    });

    /// <summary>Base de la URL del QR para facturas verificables.</summary>
    public static string QrValidation(AeatEnvironment environment) => environment == AeatEnvironment.Production
        ? "https://www2.agenciatributaria.gob.es/wlpl/TIKE-CONT/ValidarQR"
        : "https://prewww2.aeat.es/wlpl/TIKE-CONT/ValidarQR";
}
