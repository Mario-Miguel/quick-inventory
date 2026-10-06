using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using QuickInventory.Core.Models;

namespace QuickInventory.UI;

/// <summary>
/// Convierte una lista de ventas en un archivo CSV o Excel, con una fila por venta.
/// En el Excel, la taquilla y la tienda van en hojas separadas; en el CSV, que solo
/// tiene una tabla, van agrupadas y se distinguen por la columna "Punto de venta".
/// </summary>
public static class SalesExport
{
    private static readonly string[] Headers =
        ["ID", "Fecha", "Hora", "Punto de venta", "Método de pago", "Productos", "Visita guiada", "Visita grupal", "Anulada", "Total"];

    /// <summary>
    /// CSV pensado para abrirlo con Excel en español: separado por ";", con coma decimal
    /// y con BOM para que reconozca las tildes.
    /// </summary>
    public static byte[] ToCsv(IEnumerable<Sale> sales)
    {
        var spanish = CultureInfo.GetCultureInfo("es-ES");
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(';', Headers.Select(Escape)));

        foreach (var sale in sales.OrderBy(s => s.SalesPoint).ThenBy(s => s.Date))
        {
            string[] fields =
            [
                sale.Id.ToString(spanish),
                sale.Date.ToString("dd/MM/yyyy", spanish),
                sale.Date.ToString("HH:mm", spanish),
                ActiveProfile.Name(sale.SalesPoint),
                PaymentMethods.Name(sale.PaymentMethod),
                Products(sale),
                YesNo(sale.GuidedVisit),
                YesNo(sale.GroupVisit),
                YesNo(sale.Canceled),
                sale.Total.ToString("0.00", spanish),
            ];
            csv.AppendLine(string.Join(';', fields.Select(Escape)));
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    /// <summary>
    /// Libro de Excel con una hoja por punto de venta ("Taquilla" y "Tienda"), para llevar
    /// las cuentas por separado. Cada hoja acaba con una fila con su total.
    /// </summary>
    public static byte[] ToExcel(IEnumerable<Sale> sales)
    {
        using var book = new XLWorkbook();

        // Taquilla y Tienda salen siempre, aunque no tengan ventas, para que el libro tenga
        // siempre la misma forma. Admin no vende: solo sale si por algún motivo tiene ventas.
        foreach (var salesPoint in Enum.GetValues<SalesPoint>())
        {
            var ofSalesPoint = sales.Where(s => s.SalesPoint == salesPoint).ToList();
            if (salesPoint == SalesPoint.Admin && ofSalesPoint.Count == 0) continue;
            AddSheet(book, salesPoint, ofSalesPoint);
        }

        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>Columna de una hoja de Excel: título y cómo sacar el valor de cada venta.</summary>
    private sealed record ExcelColumn(string Header, Func<Sale, XLCellValue> Value, string? Format = null);

    private static void AddSheet(XLWorkbook book, SalesPoint salesPoint, List<Sale> sales)
    {
        var sheet = book.Worksheets.Add(ActiveProfile.Name(salesPoint));

        // Las visitas guiadas y grupales solo tienen sentido en la taquilla.
        List<ExcelColumn> columns =
        [
            new("ID", s => s.Id),
            new("Fecha", s => s.Date.Date, "dd/mm/yyyy"),
            new("Hora", s => s.Date.ToString("HH:mm")),
            new("Método de pago", s => PaymentMethods.Name(s.PaymentMethod)),
            new("Productos", s => Products(s)),
        ];
        if (salesPoint == SalesPoint.TicketOffice)
        {
            columns.Add(new("Visita guiada", s => YesNo(s.GuidedVisit)));
            columns.Add(new("Visita grupal", s => YesNo(s.GroupVisit)));
        }
        columns.Add(new("Anulada", s => YesNo(s.Canceled)));
        columns.Add(new("Total", s => s.Total, "#,##0.00 €"));

        for (var column = 0; column < columns.Count; column++)
            sheet.Cell(1, column + 1).Value = columns[column].Header;

        var row = 2;
        foreach (var sale in sales)
        {
            for (var column = 0; column < columns.Count; column++)
                sheet.Cell(row, column + 1).Value = columns[column].Value(sale);
            row++;
        }

        // Total de las ventas exportadas de este punto de venta, sin contar las anuladas.
        var totalColumn = columns.Count;
        sheet.Cell(row, totalColumn - 1).Value = "Total";
        sheet.Cell(row, totalColumn).Value = sales.Where(s => !s.Canceled).Sum(s => s.Total);
        sheet.Row(row).Style.Font.Bold = true;

        for (var column = 0; column < columns.Count; column++)
            if (columns[column].Format is { } format)
                sheet.Column(column + 1).Style.NumberFormat.Format = format;

        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
    }

    /// <summary>Resumen de lo vendido, por ejemplo "2 x Entrada general, 1 x Sidra".</summary>
    public static string Products(Sale sale) =>
        string.Join(", ", sale.Lines.Select(l => $"{l.Amount} x {l.Description}"));

    private static string YesNo(bool value) => value ? "Sí" : "No";

    /// <summary>Pone entre comillas los campos que llevan ";", comillas o saltos de línea.</summary>
    private static string Escape(string field) =>
        field.IndexOfAny([';', '"', '\n', '\r']) >= 0
            ? $"\"{field.Replace("\"", "\"\"")}\""
            : field;
}
