using System.Globalization;
using System.IO.Compression;
using System.Text;
using ClosedXML.Excel;
using QuickInventory.Core.Models;

namespace QuickInventory.UI;

/// <summary>
/// Convierte una lista de ventas en un archivo CSV o Excel. En el CSV hay una fila por
/// venta y la taquilla y la tienda se distinguen por la columna "Punto de venta". En el
/// Excel van en hojas separadas, y cada venta lleva debajo el detalle de sus productos.
/// El Excel lleva además una hoja con los reembolsos, y se puede exportar dentro de un ZIP
/// junto con sus facturas.
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
    /// las cuentas por separado, y otra con los reembolsos. Cada hoja acaba con una fila con su total.
    /// </summary>
    /// <param name="invoicePaths">
    /// Ruta relativa al Excel de la factura de cada reembolso (por su Id). Si se pasa, la
    /// columna "Factura adjunta" lleva un enlace al archivo; si no, solo dice si tiene.
    /// </param>
    public static byte[] ToExcel(IEnumerable<Sale> sales, IEnumerable<CashMovement> refunds,
        IReadOnlyDictionary<int, string>? invoicePaths = null)
    {
        using var book = new XLWorkbook();
        AddSheet(book, null, [.. sales]);

        // Taquilla y Tienda salen siempre, aunque no tengan ventas, para que el libro tenga
        // siempre la misma forma. Admin no vende: solo sale si por algún motivo tiene ventas.
        foreach (var salesPoint in Enum.GetValues<SalesPoint>())
        {
            var ofSalesPoint = sales.Where(s => s.SalesPoint == salesPoint).ToList();
            if (salesPoint == SalesPoint.Admin && ofSalesPoint.Count == 0) continue;
            AddSheet(book, salesPoint, ofSalesPoint);
        }

        AddRefundsSheet(book, [.. refunds], invoicePaths);

        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Columna de una hoja de Excel: título y cómo sacar el valor de la fila de la venta y
    /// de las filas de sus productos. Si una de las dos es null, esa celda se queda vacía.
    /// </summary>
    private sealed record ExcelColumn(
        string Header,
        Func<Sale, XLCellValue>? SaleValue,
        Func<Sale, SalesLine, XLCellValue>? LineValue,
        string? Format = null);

    private const string EurosFormat = "#,##0.00 €";

    private static void AddSheet(XLWorkbook book, SalesPoint? salesPoint, List<Sale> sales)
    {
        var sheet = book.Worksheets.Add(salesPoint!=null?ActiveProfile.Name((SalesPoint)salesPoint) :"Total");

        // Cada venta ocupa una fila con sus datos y su total, y debajo una fila por producto
        // con el detalle del precio y del descuento. Las visitas guiadas y grupales solo
        // tienen sentido en la taquilla.
        List<ExcelColumn> columns =
        [
            new("ID", s => s.Id, null),
            new("Fecha", s => s.Date.Date, null, "dd/mm/yyyy"),
            new("Hora", s => s.Date.ToString("HH:mm"), null),
            new("Método de pago", s => PaymentMethods.Name(s.PaymentMethod), null),
        ];
        if (salesPoint == SalesPoint.TicketOffice)
        {
            columns.Add(new("Visita guiada", s => YesNo(s.GuidedVisit), null));
            columns.Add(new("Visita grupal", s => YesNo(s.GroupVisit), null));
        }
        columns.AddRange(
        [
            new("Anulada", s => YesNo(s.Canceled), null),
            new("Producto", s => Products(s), (_, l) => l.Description),
            new("Cantidad", s => s.Lines.Sum(l => l.Amount), (_, l) => l.Amount),
            new("Precio", s => s.Lines.Sum(l => l.Amount * l.BasePrice), (_, l) => l.BasePrice, EurosFormat),
            new("Descuento", s => Discounts(s), (s, l) => Discount(s, l)),
            new("Descontado", s => s.Lines.Sum(l => l.DiscountTotal), (_, l) => l.DiscountTotal, EurosFormat),
            new("Total", s => s.Total, (_, l) => l.Subtotal, EurosFormat),
        ]);

        for (var column = 0; column < columns.Count; column++)
            sheet.Cell(1, column + 1).Value = columns[column].Header;

        var row = 2;
        foreach (var sale in sales)
        {
            var saleRow = sheet.Row(row);
            for (var column = 0; column < columns.Count; column++)
                if (columns[column].SaleValue is { } value)
                    sheet.Cell(row, column + 1).Value = value(sale);
            saleRow.Style.Font.Bold = true;
            saleRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF2F7");
            row++;

            var firstLineRow = row;
            foreach (var line in sale.Lines)
            {
                for (var column = 0; column < columns.Count; column++)
                    if (columns[column].LineValue is { } value)
                        sheet.Cell(row, column + 1).Value = value(sale, line);
                row++;
            }

            // Los productos se agrupan bajo su venta, para poder plegarlos desde Excel.
            if (row > firstLineRow)
                sheet.Rows(firstLineRow, row - 1).Group();

            if (sale.Canceled)
                sheet.Rows(saleRow.RowNumber(), row - 1).Style.Font.FontColor = XLColor.Gray;
        }

        // Total de las ventas exportadas de este punto de venta, sin contar las anuladas.
        
        var cancelledColumn = columns.FindIndex(c => c.Header == "Anulada") + 1;
        var totalColumn = sheet.Column(columns.Count).ColumnLetter();
        var canceled = sheet.Column(cancelledColumn).ColumnLetter();


        sheet.Cell(row, columns.Count - 1).Value = "Total";
        sheet.Cell(row, totalColumn).FormulaA1 = $"SUMIFS({totalColumn}2:{totalColumn}{row-1},{canceled}2:{canceled}{row-1},\"{YesNo(false)}\")";
        sheet.Row(row).Style.Font.Bold = true;

        for (var column = 0; column < columns.Count; column++)
            if (columns[column].Format is { } format)
                sheet.Column(column + 1).Style.NumberFormat.Format = format;

        sheet.Outline.SummaryVLocation = XLOutlineSummaryVLocation.Top;
        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
    }

    /// <summary>
    /// ZIP con el Excel (<paramref name="excelName"/>) y una carpeta "Facturas" con las
    /// facturas de los reembolsos. En el Excel cada factura es un enlace relativo, así que
    /// funciona en cualquier ordenador una vez descomprimido el ZIP.
    /// </summary>
    public static byte[] ToZipWithInvoices(IEnumerable<Sale> sales, IEnumerable<CashMovement> refunds,
        IEnumerable<InvoiceFile> invoices, string excelName)
    {
        // Nombres sin espacios ni tildes ("Facturas/Reembolso-12.pdf") para que el enlace
        // funcione igual en todos los Excel. El nombre original sigue saliendo en la celda.
        var invoiceList = invoices.ToList();
        var paths = invoiceList.ToDictionary(f => f.CashMovementId, f => $"Facturas/Reembolso-{f.CashMovementId}{f.Extension}");

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            AddEntry(zip, excelName, ToExcel(sales, refunds, paths));
            foreach (var invoice in invoiceList)
                AddEntry(zip, paths[invoice.CashMovementId], invoice.Content);
        }
        return stream.ToArray();
    }

    private static void AddEntry(ZipArchive zip, string name, byte[] content)
    {
        using var entry = zip.CreateEntry(name).Open();
        entry.Write(content);
    }

    /// <summary>
    /// Hoja con un reembolso por fila. Sale siempre, aunque no haya reembolsos, para que el
    /// libro tenga siempre la misma forma. El total no cuenta los anulados.
    /// </summary>
    private static void AddRefundsSheet(XLWorkbook book, List<CashMovement> refunds, IReadOnlyDictionary<int, string>? invoicePaths)
    {
        var sheet = book.Worksheets.Add("Reembolsos");

        string[] headers =
            ["ID", "Fecha", "Hora", "Pagado desde", "Persona", "Concepto", "Nº factura", "Factura adjunta", "Notas", "Anulado", "Importe"];
        for (var column = 0; column < headers.Length; column++)
            sheet.Cell(1, column + 1).Value = headers[column];

        var row = 2;
        foreach (var refund in refunds.OrderBy(r => r.Date))
        {
            XLCellValue[] values =
            [
                refund.Id,
                refund.Date.Date,
                refund.Date.ToString("HH:mm"),
                RefundOrigin(refund),
                refund.Person ?? "",
                refund.Description,
                refund.InvoiceNumber ?? "",
                refund.InvoiceFileName ?? "No",
                refund.Notes ?? "",
                YesNo(refund.Canceled),
                refund.Amount,
            ];
            for (var column = 0; column < values.Length; column++)
                sheet.Cell(row, column + 1).Value = values[column];

            if (invoicePaths is not null && invoicePaths.TryGetValue(refund.Id, out var path))
            {
                var link = sheet.Cell(row, Array.IndexOf(headers, "Factura adjunta") + 1);
                link.SetHyperlink(new XLHyperlink(new Uri(path, UriKind.Relative)));
                link.Style.Font.FontColor = XLColor.Blue;
                link.Style.Font.Underline = XLFontUnderlineValues.Single;
            }

            if (refund.Canceled)
                sheet.Row(row).Style.Font.FontColor = XLColor.Gray;
            row++;
        }

        var amount = sheet.Column(headers.Length).ColumnLetter();
        var canceled = sheet.Column(Array.IndexOf(headers, "Anulado") + 1).ColumnLetter();
        sheet.Cell(row, headers.Length - 1).Value = "Total";
        sheet.Cell(row, amount).FormulaA1 = $"SUMIFS({amount}2:{amount}{row - 1},{canceled}2:{canceled}{row - 1},\"{YesNo(false)}\")";
        sheet.Row(row).Style.Font.Bold = true;

        sheet.Column(2).Style.NumberFormat.Format = "dd/mm/yyyy";
        sheet.Column(headers.Length).Style.NumberFormat.Format = EurosFormat;
        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
    }

    /// <summary>
    /// Descuentos aplicados a una línea, por ejemplo "RGCC/CNSO (-30 %) + Visita grupal (-0,50 €)".
    /// Vacío si se cobró sin descuento.
    /// </summary>
    private static string Discount(Sale sale, SalesLine line)
    {
        List<string> discounts = [];
        if (line.DiscountName is not null)
            discounts.Add($"{line.DiscountName} (-{line.DiscountPercent:0.##} %)");
        if (sale.GroupVisit)
            discounts.Add($"Visita grupal (-{Sale.GroupVisitDiscount:C})");
        return string.Join(" + ", discounts);
    }

    /// <summary>De dónde sale el dinero de un reembolso: "Caja de Taquilla", "Caja de Tienda" o "Transferencia".</summary>
    public static string RefundOrigin(CashMovement refund) =>
        refund.SalesPoint is { } salesPoint
            ? $"Caja de {ActiveProfile.Name(salesPoint)}"
            : PaymentMethods.Name(refund.PaymentMethod);

    /// <summary>Resumen de lo vendido, por ejemplo "2 x Entrada general, 1 x Sidra".</summary>
    public static string Products(Sale sale) =>
        string.Join(", ", sale.Lines.Select(l => $"{l.Amount} x {l.Description}"));
    
    public static string Discounts(Sale sale) => 
    string.Join(" + ", sale.Lines.Select(l=>Discount(sale, l)).Where(d => d!="").Distinct());

    private static string YesNo(bool value) => value ? "Sí" : "No";

    /// <summary>Pone entre comillas los campos que llevan ";", comillas o saltos de línea.</summary>
    private static string Escape(string field) =>
        field.IndexOfAny([';', '"', '\n', '\r']) >= 0
            ? $"\"{field.Replace("\"", "\"\"")}\""
            : field;
}
