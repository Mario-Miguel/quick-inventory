namespace Invoicing.UI.Components.Dialogs;

/// <summary>Textos de <see cref="NumberingSeriesDialog"/> para un tipo de documento.</summary>
/// <param name="NextPrefix">Lo que va delante del próximo número: "La próxima factura será la".</param>
public sealed record NumberingSeriesTexts(
    string? Intro, string CodeHelper, string LastNumberLabel, string LastNumberHelper, string NextPrefix)
{
    public static readonly NumberingSeriesTexts CustomerInvoices = new(
        "Numera las facturas a clientes (colegios, asociaciones...). Va aparte de las series de otros documentos, como los tickets.",
        "Va delante del número en cada factura. Solo letras sin tilde, números, «-» y «/»",
        "Número de la última factura emitida",
        "Si este año ya se han hecho facturas con esta serie (por ejemplo, con otro programa), escribe el número de la última. Si no, deja 0",
        "La próxima factura será la");
}
