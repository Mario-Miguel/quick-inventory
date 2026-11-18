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
/// El Excel lleva además las facturas de proveedores (con sus líneas y el IVA soportado), y se
/// puede exportar dentro de un ZIP junto con los archivos de esas facturas.
/// </summary>
public static class SalesExport
{
    /// <summary>
    /// CSV pensado para abrirlo con Excel en español: separado por ";", con coma decimal
    /// y con BOM para que reconozca las tildes. Lleva las columnas propias de todos los
    /// tipos de venta; en las ventas a las que no aplican se quedan vacías.
    /// </summary>
    public static byte[] ToCsv(IEnumerable<Sale> sales)
    {
        var spanish = CultureInfo.GetCultureInfo("es-ES");
        var saleFields = SaleTypes.AllFields;

        string[] headers =
        [
            "ID", "Nº factura", "Fecha", "Hora", "Punto de venta", "Método de pago", "Productos",
            .. saleFields.Select(f => f.Header),
            "Anulada", "IVA", "Base imponible", "Cuota IVA", "Total",
        ];

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(';', headers.Select(Escape)));

        foreach (var sale in sales.OrderBy(s => s.SalesPoint).ThenBy(s => s.Date))
        {
            string[] fields =
            [
                sale.Id.ToString(spanish),
                sale.InvoiceId ?? "",
                sale.Date.ToString("dd/MM/yyyy", spanish),
                sale.Date.ToString("HH:mm", spanish),
                ActiveProfile.Name(sale.SalesPoint),
                PaymentMethods.Name(sale.PaymentMethod),
                Products(sale),
                .. saleFields.Select(f => f.Text(sale)),
                YesNo(sale.Canceled),
                TaxNames(sale),
                TaxBase(sale)?.ToString("0.00", spanish) ?? "",
                TaxAmount(sale)?.ToString("0.00", spanish) ?? "",
                sale.Total.ToString("0.00", spanish),
            ];
            csv.AppendLine(string.Join(';', fields.Select(Escape)));
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    /// <summary>
    /// Libro de Excel con una hoja por punto de venta ("Taquilla" y "Tienda"), para llevar
    /// las cuentas por separado, otra con el desglose del IVA, otra con las facturas de
    /// proveedores y otra con su IVA soportado. Cada hoja acaba con una fila con su total.
    /// </summary>
    /// <param name="purchaseInvoices">Facturas de proveedores del mes, por la fecha de la factura (hojas de facturas e IVA soportado).</param>
    /// <param name="paidInvoices">
    /// Facturas de proveedores pagadas en el mes, por la fecha de pago: son el dinero que sale, y van en la
    /// hoja "Total" junto a las ventas para que sea un resumen de todo el dinero que entra y sale.
    /// </param>
    /// <param name="filePaths">
    /// Ruta relativa al Excel del archivo de cada factura de proveedor (por su Id). Si se pasa,
    /// la columna "Archivo" lleva un enlace al archivo; si no, solo su nombre.
    /// </param>
    public static byte[] ToExcel(IEnumerable<Sale> sales, IEnumerable<PurchaseInvoice> purchaseInvoices,
        IEnumerable<PurchaseInvoice> paidInvoices, IReadOnlyDictionary<int, string>? filePaths = null)
    {
        using var book = new XLWorkbook();
        AddSheet(book, null, [.. sales], [.. paidInvoices]);

        // Una hoja por tipo de venta. Salen siempre, aunque no tengan ventas, para que el
        // libro tenga siempre la misma forma.
        foreach (var saleType in SaleTypes.All)
            AddSheet(book, saleType, [.. sales.Where(s => s.SalesPoint == saleType.SalesPoint)]);

        AddTaxSheet(book, [.. sales]);
        AddPurchaseInvoicesSheet(book, [.. purchaseInvoices], filePaths);
        AddPurchaseTaxSheet(book, [.. purchaseInvoices]);

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

    /// <param name="saleType">Tipo de venta de la hoja, o null para la hoja "Total" con todas.</param>
    /// <param name="paidInvoices">
    /// Solo en la hoja "Total": facturas de proveedores pagadas, que salen mezcladas por fecha con las
    /// ventas y con el importe en negativo (es dinero que sale). Debajo va el resumen del dinero del mes.
    /// </param>
    private static void AddSheet(XLWorkbook book, SaleType? saleType, List<Sale> sales, List<PurchaseInvoice>? paidInvoices = null)
    {
        var sheet = book.Worksheets.Add(saleType is null ? "Total" : ActiveProfile.Name(saleType.SalesPoint));

        // La hoja de un tipo lleva solo sus columnas propias; la de "Total", las de todos.
        var saleFields = saleType?.Fields ?? SaleTypes.AllFields;

        // Cada venta ocupa una fila con sus datos y su total, y debajo una fila por producto
        // con el detalle del precio y del descuento.
        List<ExcelColumn> columns =
        [
            // En "Total" se mezclan ventas de los dos puntos de venta y pagos a proveedores.
            .. saleType is null ? [new ExcelColumn(MovementHeader, s => $"Venta de {ActiveProfile.Name(s.SalesPoint)}", null)] : Array.Empty<ExcelColumn>(),
            new("ID", s => s.Id, null),
            new("Nº factura", s => s.InvoiceId ?? "", null),
            new("Fecha", s => s.Date.Date, null, "dd/mm/yyyy"),
            new("Hora", s => s.Date.ToString("HH:mm"), null),
            new("Método de pago", s => PaymentMethods.Name(s.PaymentMethod), null),
            .. saleFields.Select(f => new ExcelColumn(f.Header, s => f.Text(s), null)),
            new("Anulada", s => YesNo(s.Canceled), null),
            new("Producto", s => Products(s), (_, l) => l.Description),
            new("Cantidad", s => s.Lines.Sum(l => l.Amount), (_, l) => l.Amount),
            // Unidades para el recuento: un cupón de 2 entradas vendido 3 veces son 6.
            new(saleType?.CountedUnitsName ?? "Unidades contadas",
                s => s.Lines.Sum(l => l.CountedUnits), (_, l) => l.CountedUnits),
            new("Precio", s => s.Lines.Sum(l => l.Amount * l.BasePrice), (_, l) => l.BasePrice, EurosFormat),
            new("Descuento", s => Discounts(s), (s, l) => Discount(s, l)),
            new("Descontado", s => s.Lines.Sum(l => l.DiscountTotal), (_, l) => l.DiscountTotal, EurosFormat),
            // La base y la cuota solo van en la fila de la venta: se calculan por tipo sobre el total,
            // no línea a línea, y es lo que cuenta en la factura.
            new("IVA", s => TaxNames(s), (_, l) => TaxNames(l)),
            new(TaxBaseHeader, s => Cell(TaxBase(s)), null, EurosFormat),
            new(TaxAmountHeader, s => Cell(TaxAmount(s)), null, EurosFormat),
            new("Total", s => s.Total, (_, l) => l.Subtotal, EurosFormat),
        ];

        for (var column = 0; column < columns.Count; column++)
            sheet.Cell(1, column + 1).Value = columns[column].Header;

        int Col(string header) => columns.FindIndex(c => c.Header == header) + 1;

        // Ventas y pagos a proveedores, por fecha. Los pagos solo tienen día, así que van al principio de ese día.
        var movements = sales.Select(s => (Date: s.Date, Sale: (Sale?)s, Invoice: (PurchaseInvoice?)null))
            .Concat((paidInvoices ?? []).Select(i => (Date: i.PaymentDate!.Value, Sale: (Sale?)null, Invoice: (PurchaseInvoice?)i)))
            .OrderBy(m => m.Date)
            .ToList();

        var row = 2;
        foreach (var (_, movementSale, invoice) in movements)
        {
            if (invoice is not null)
            {
                // Pago a proveedor: importes en negativo para que el total de la hoja sea lo que queda.
                sheet.Cell(row, Col(MovementHeader)).Value = "Factura de proveedor";
                sheet.Cell(row, Col("ID")).Value = invoice.Id;
                sheet.Cell(row, Col("Nº factura")).Value = invoice.Number;
                sheet.Cell(row, Col("Fecha")).Value = invoice.PaymentDate!.Value;
                sheet.Cell(row, Col("Método de pago")).Value = PaymentMethods.Describe(invoice.PaymentMethod, invoice.PaymentSalesPoint);
                sheet.Cell(row, Col("Anulada")).Value = YesNo(false);
                sheet.Cell(row, Col("Producto")).Value = $"{invoice.Provider?.LegalName}: {invoice.Summary}";
                sheet.Cell(row, Col("Cantidad")).Value = invoice.Lines.Sum(l => l.Quantity);
                sheet.Cell(row, Col("IVA")).Value = string.Join(" + ", invoice.TaxBreakdown().Select(i => i.TaxRate.Name));
                sheet.Cell(row, Col(TaxBaseHeader)).Value = -invoice.BaseAmount;
                sheet.Cell(row, Col(TaxAmountHeader)).Value = -invoice.TaxAmount;
                sheet.Cell(row, Col("Total")).Value = -invoice.Amount;
                sheet.Row(row).Style.Font.Bold = true;
                sheet.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#FDF0E6");
                row++;
                continue;
            }

            var sale = movementSale!;
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

        // Totales de las ventas exportadas de este punto de venta, sin contar las anuladas. Las filas de
        // productos tienen "Anulada" vacía, así que tampoco se suman.
        var canceled = sheet.Column(columns.FindIndex(c => c.Header == "Anulada") + 1).ColumnLetter();
        var firstSummed = columns.FindIndex(c => c.Header == TaxBaseHeader) + 1;

        sheet.Cell(row, firstSummed - 1).Value = "Total";
        for (var column = firstSummed; column <= columns.Count; column++)
        {
            var letter = sheet.Column(column).ColumnLetter();
            sheet.Cell(row, column).FormulaA1 =
                $"SUMIFS({letter}2:{letter}{row - 1},{canceled}2:{canceled}{row - 1},\"{YesNo(false)}\")";
        }
        sheet.Row(row).Style.Font.Bold = true;

        for (var column = 0; column < columns.Count; column++)
            if (columns[column].Format is { } format)
                sheet.Column(column + 1).Style.NumberFormat.Format = format;

        if (paidInvoices is not null)
            AddMoneySummary(sheet, row + 2, sales, paidInvoices);

        sheet.Outline.SummaryVLocation = XLOutlineSummaryVLocation.Top;
        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
    }

    private const string MovementHeader = "Movimiento";

    /// <summary>
    /// Resumen del dinero del mes al pie de la hoja "Total": lo que entra por las ventas de cada punto
    /// de venta y lo que sale por las facturas de proveedores pagadas, por forma de pago, y el saldo.
    /// No cuenta las ventas anuladas.
    /// </summary>
    private static void AddMoneySummary(IXLWorksheet sheet, int firstRow, List<Sale> sales, List<PurchaseInvoice> paidInvoices)
    {
        var methods = Enum.GetValues<PaymentMethod>();
        var row = firstRow;

        sheet.Cell(row, 1).Value = "Resumen del dinero del mes";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 1).Style.Font.FontSize = 13;
        row++;

        var header = sheet.Row(row);
        sheet.Cell(row, 1).Value = "Concepto";
        for (var i = 0; i < methods.Length; i++)
            sheet.Cell(row, i + 2).Value = PaymentMethods.Name(methods[i]);
        sheet.Cell(row, methods.Length + 2).Value = "Total";
        header.Style.Font.Bold = true;
        row++;

        var firstValueRow = row;
        void AddLine(string concept, Func<PaymentMethod, decimal> amount)
        {
            sheet.Cell(row, 1).Value = concept;
            for (var i = 0; i < methods.Length; i++)
                sheet.Cell(row, i + 2).Value = amount(methods[i]);
            var first = sheet.Column(2).ColumnLetter();
            var last = sheet.Column(methods.Length + 1).ColumnLetter();
            sheet.Cell(row, methods.Length + 2).FormulaA1 = $"SUM({first}{row}:{last}{row})";
            row++;
        }

        var valid = sales.Where(s => !s.Canceled).ToList();
        foreach (var saleType in SaleTypes.All)
            AddLine($"+ Ventas de {ActiveProfile.Name(saleType.SalesPoint)}",
                m => valid.Where(s => s.SalesPoint == saleType.SalesPoint && s.PaymentMethod == m).Sum(s => s.Total));
        AddLine("− Facturas de proveedores pagadas",
            m => -paidInvoices.Where(i => i.PaymentMethod == m).Sum(i => i.Amount));

        sheet.Cell(row, 1).Value = "Saldo";
        for (var column = 2; column <= methods.Length + 2; column++)
        {
            var letter = sheet.Column(column).ColumnLetter();
            sheet.Cell(row, column).FormulaA1 = $"SUM({letter}{firstValueRow}:{letter}{row - 1})";
        }
        sheet.Row(row).Style.Font.Bold = true;

        sheet.Range(firstRow + 1, 2, row, methods.Length + 2).Style.NumberFormat.Format = EurosFormat;
        sheet.Range(firstRow + 1, 1, row, methods.Length + 2).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.Range(row, 1, row, methods.Length + 2).Style.Border.TopBorder = XLBorderStyleValues.Thin;
    }

    private const string TaxBaseHeader = "Base imponible";
    private const string TaxAmountHeader = "Cuota IVA";

    /// <summary>
    /// Hoja "IVA": base, cuota y total por punto de venta y tipo de IVA, sumando el desglose de cada
    /// venta (que es lo que figura en cada factura). No cuenta las ventas anuladas. Sale siempre,
    /// aunque no haya ventas, para que el libro tenga siempre la misma forma.
    /// </summary>
    private static void AddTaxSheet(XLWorkbook book, List<Sale> sales)
    {
        var sheet = book.Worksheets.Add("IVA");

        string[] headers = ["Punto de venta", "IVA", "Ventas", TaxBaseHeader, TaxAmountHeader, "Total"];
        for (var column = 0; column < headers.Length; column++)
            sheet.Cell(1, column + 1).Value = headers[column];

        var groups = sales
            .Where(s => !s.Canceled)
            .SelectMany(s => TaxBreakdown.Calculate(s.Lines).Select(item => (s.SalesPoint, Item: item)))
            .GroupBy(x => (x.SalesPoint, x.Item.Name))
            .OrderBy(g => g.Key.SalesPoint)
            .ThenBy(g => g.Min(x => x.Item.TaxRate is not null ? 0 : x.Item.TaxExemption is not null ? 1 : 2))
            .ThenByDescending(g => g.Max(x => x.Item.TaxRate));

        var row = 2;
        foreach (var group in groups)
        {
            var items = group.Select(x => x.Item).ToList();
            var hasTax = items.All(i => i.TaxBase is not null);
            XLCellValue[] values =
            [
                ActiveProfile.Name(group.Key.SalesPoint),
                group.Key.Name,
                items.Count,
                hasTax ? items.Sum(i => i.TaxBase!.Value) : Blank.Value,
                hasTax ? items.Sum(i => i.TaxAmount!.Value) : Blank.Value,
                items.Sum(i => i.Total),
            ];
            for (var column = 0; column < values.Length; column++)
                sheet.Cell(row, column + 1).Value = values[column];
            row++;
        }

        sheet.Cell(row, 3).Value = "Total";
        foreach (var column in new[] { 4, 5, 6 })
        {
            var letter = sheet.Column(column).ColumnLetter();
            sheet.Cell(row, column).FormulaA1 = $"SUM({letter}2:{letter}{row - 1})";
            sheet.Column(column).Style.NumberFormat.Format = EurosFormat;
        }
        sheet.Row(row).Style.Font.Bold = true;

        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
    }

    /// <summary>
    /// ZIP con el Excel (<paramref name="excelName"/>) y una carpeta "Facturas" con los archivos
    /// de las facturas de proveedores. En el Excel cada archivo es un enlace relativo, así que
    /// funciona en cualquier ordenador una vez descomprimido el ZIP.
    /// </summary>
    public static byte[] ToZipWithInvoices(IEnumerable<Sale> sales, IEnumerable<PurchaseInvoice> purchaseInvoices,
        IEnumerable<PurchaseInvoice> paidInvoices, IEnumerable<InvoiceFile> files, string excelName)
    {
        // Nombres sin espacios ni tildes ("Facturas/Factura-12.pdf") para que el enlace funcione
        // igual en todos los Excel. El nombre original sigue saliendo en la celda.
        var fileList = files.Where(f => f.PurchaseInvoiceId is not null).ToList();
        var paths = fileList.ToDictionary(f => f.PurchaseInvoiceId!.Value, f => $"Facturas/Factura-{f.PurchaseInvoiceId}{f.Extension}");

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            AddEntry(zip, excelName, ToExcel(sales, purchaseInvoices, paidInvoices, paths));
            foreach (var file in fileList)
                AddEntry(zip, paths[file.PurchaseInvoiceId!.Value], file.Content);
        }
        return stream.ToArray();
    }

    private static void AddEntry(ZipArchive zip, string name, byte[] content)
    {
        using var entry = zip.CreateEntry(name).Open();
        entry.Write(content);
    }

    /// <summary>
    /// Hoja "Facturas proveedores": una fila por factura con sus totales y, debajo y agrupadas, sus
    /// líneas. Sale siempre, aunque no haya facturas, para que el libro tenga siempre la misma forma.
    /// El total no cuenta las anuladas.
    /// </summary>
    private static void AddPurchaseInvoicesSheet(XLWorkbook book, List<PurchaseInvoice> invoices,
        IReadOnlyDictionary<int, string>? filePaths)
    {
        var sheet = book.Worksheets.Add("Facturas proveedores");

        string[] headers =
        [
            "ID", "Fecha", "Proveedor", "NIF", "Nº factura", "Concepto", "Cantidad", "Precio unidad",
            "IVA", TaxBaseHeader, TaxAmountHeader, "Total", "Pagada", "Fecha de pago", "Forma de pago",
            "Archivo", "Notas", "Anulada",
        ];
        int Col(string header) => Array.IndexOf(headers, header) + 1;
        for (var column = 0; column < headers.Length; column++)
            sheet.Cell(1, column + 1).Value = headers[column];

        var row = 2;
        foreach (var invoice in invoices.OrderBy(i => i.InvoiceDate).ThenBy(i => i.Id))
        {
            var invoiceRow = sheet.Row(row);
            sheet.Cell(row, Col("ID")).Value = invoice.Id;
            sheet.Cell(row, Col("Fecha")).Value = invoice.InvoiceDate;
            sheet.Cell(row, Col("Proveedor")).Value = invoice.Provider?.LegalName ?? "";
            sheet.Cell(row, Col("NIF")).Value = invoice.Provider?.Nif ?? "";
            sheet.Cell(row, Col("Nº factura")).Value = invoice.Number;
            sheet.Cell(row, Col("Concepto")).Value = invoice.Summary;
            sheet.Cell(row, Col("Cantidad")).Value = invoice.Lines.Sum(l => l.Quantity);
            sheet.Cell(row, Col("IVA")).Value = string.Join(" + ", invoice.TaxBreakdown().Select(i => i.TaxRate.Name));
            sheet.Cell(row, Col(TaxBaseHeader)).Value = invoice.BaseAmount;
            sheet.Cell(row, Col(TaxAmountHeader)).Value = invoice.TaxAmount;
            sheet.Cell(row, Col("Total")).Value = invoice.Amount;
            sheet.Cell(row, Col("Pagada")).Value = YesNo(invoice.IsPaid);
            sheet.Cell(row, Col("Fecha de pago")).Value = invoice.PaymentDate is { } paid ? paid : Blank.Value;
            sheet.Cell(row, Col("Forma de pago")).Value = PaymentMethods.Describe(invoice.PaymentMethod, invoice.PaymentSalesPoint);
            sheet.Cell(row, Col("Archivo")).Value = invoice.InvoiceFileName;
            sheet.Cell(row, Col("Notas")).Value = invoice.Notes ?? "";
            sheet.Cell(row, Col("Anulada")).Value = YesNo(invoice.Canceled);

            if (filePaths is not null && filePaths.TryGetValue(invoice.Id, out var path))
            {
                var link = sheet.Cell(row, Col("Archivo"));
                link.SetHyperlink(new XLHyperlink(new Uri(path, UriKind.Relative)));
                link.Style.Font.FontColor = XLColor.Blue;
                link.Style.Font.Underline = XLFontUnderlineValues.Single;
            }
            invoiceRow.Style.Font.Bold = true;
            invoiceRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF2F7");
            row++;

            // Las líneas no llevan cuota ni total: el IVA se calcula por tipo sobre la suma de las bases.
            var firstLineRow = row;
            foreach (var line in invoice.Lines)
            {
                sheet.Cell(row, Col("Concepto")).Value = line.IsFree ? line.Description : $"{line.Description} (inventario)";
                sheet.Cell(row, Col("Cantidad")).Value = line.Quantity;
                sheet.Cell(row, Col("Precio unidad")).Value = line.UnitPrice;
                sheet.Cell(row, Col("IVA")).Value = line.TaxRate.Name;
                sheet.Cell(row, Col(TaxBaseHeader)).Value = line.BaseAmount;
                row++;
            }
            if (row > firstLineRow)
                sheet.Rows(firstLineRow, row - 1).Group();

            if (invoice.Canceled)
                sheet.Rows(invoiceRow.RowNumber(), row - 1).Style.Font.FontColor = XLColor.Gray;
        }

        // Las filas de las líneas tienen "Anulada" vacía, así que no se suman.
        var canceled = sheet.Column(Col("Anulada")).ColumnLetter();
        sheet.Cell(row, Col(TaxBaseHeader) - 1).Value = "Total";
        foreach (var header in new[] { TaxBaseHeader, TaxAmountHeader, "Total" })
        {
            var letter = sheet.Column(Col(header)).ColumnLetter();
            sheet.Cell(row, Col(header)).FormulaA1 =
                $"SUMIFS({letter}2:{letter}{row - 1},{canceled}2:{canceled}{row - 1},\"{YesNo(false)}\")";
        }
        sheet.Row(row).Style.Font.Bold = true;

        foreach (var header in new[] { "Fecha", "Fecha de pago" })
            sheet.Column(Col(header)).Style.NumberFormat.Format = "dd/mm/yyyy";
        foreach (var header in new[] { "Precio unidad", TaxBaseHeader, TaxAmountHeader, "Total" })
            sheet.Column(Col(header)).Style.NumberFormat.Format = EurosFormat;
        sheet.Outline.SummaryVLocation = XLOutlineSummaryVLocation.Top;
        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
    }

    /// <summary>
    /// Hoja "IVA soportado": base, cuota y total de las facturas de proveedores por tipo de IVA,
    /// sumando el desglose de cada factura. No cuenta las anuladas. Sale siempre, aunque no haya facturas.
    /// </summary>
    private static void AddPurchaseTaxSheet(XLWorkbook book, List<PurchaseInvoice> invoices)
    {
        var sheet = book.Worksheets.Add("IVA soportado");

        string[] headers = ["IVA", "Facturas", TaxBaseHeader, TaxAmountHeader, "Total"];
        for (var column = 0; column < headers.Length; column++)
            sheet.Cell(1, column + 1).Value = headers[column];

        var groups = invoices
            .Where(i => !i.Canceled)
            .SelectMany(i => i.TaxBreakdown())
            .GroupBy(item => item.TaxRate)
            .OrderByDescending(g => g.Key.Value);

        var row = 2;
        foreach (var group in groups)
        {
            XLCellValue[] values =
            [
                group.Key.Name,
                group.Count(),
                group.Sum(i => i.BaseAmount),
                group.Sum(i => i.TaxAmount),
                group.Sum(i => i.Total),
            ];
            for (var column = 0; column < values.Length; column++)
                sheet.Cell(row, column + 1).Value = values[column];
            row++;
        }

        sheet.Cell(row, 2).Value = "Total";
        foreach (var column in new[] { 3, 4, 5 })
        {
            var letter = sheet.Column(column).ColumnLetter();
            sheet.Cell(row, column).FormulaA1 = $"SUM({letter}2:{letter}{row - 1})";
            sheet.Column(column).Style.NumberFormat.Format = EurosFormat;
        }
        sheet.Row(row).Style.Font.Bold = true;

        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
    }

    /// <summary>
    /// Descuentos aplicados a una línea, por ejemplo "RGCC/CNSO (-30 %) + Visita grupal (-0,50 €)".
    /// Junta el descuento de la propia línea con los ajustes de su tipo de venta.
    /// Vacío si se cobró sin descuento.
    /// </summary>
    private static string Discount(Sale sale, SalesLine line)
    {
        List<string> discounts = [];
        if (line.DiscountName is not null)
            discounts.Add($"{line.DiscountName} (-{line.DiscountPercent:0.##} %)");
        discounts.AddRange(SaleTypes.Adjustments(sale));
        return string.Join(" + ", discounts);
    }

    /// <summary>Resumen de lo vendido, por ejemplo "2 x Entrada general, 1 x Sidra".</summary>
    public static string Products(Sale sale) =>
        string.Join(", ", sale.Lines.Select(l => $"{l.Amount} x {l.Description}"));
    
    public static string Discounts(Sale sale) => 
    string.Join(" + ", sale.Lines.Select(l=>Discount(sale, l)).Where(d => d!="").Distinct());

    /// <summary>Tipos de IVA de una venta, por ejemplo "21 % + Exento (E1)".</summary>
    public static string TaxNames(Sale sale) => TaxNames(sale.Lines);

    private static string TaxNames(SalesLine line) => TaxNames([line]);

    private static string TaxNames(IEnumerable<SalesLine> lines) =>
        string.Join(" + ", TaxBreakdown.Calculate(lines).Select(i => i.Name));

    /// <summary>Base imponible de la venta, o null si alguna línea no tiene IVA indicado (ventas antiguas).</summary>
    private static decimal? TaxBase(Sale sale) => SumIfAllHaveTax(sale, i => i.TaxBase);

    /// <summary>Cuota de IVA de la venta, o null si alguna línea no tiene IVA indicado (ventas antiguas).</summary>
    private static decimal? TaxAmount(Sale sale) => SumIfAllHaveTax(sale, i => i.TaxAmount);

    private static decimal? SumIfAllHaveTax(Sale sale, Func<TaxBreakdownItem, decimal?> value)
    {
        var items = TaxBreakdown.Calculate(sale.Lines);
        return items.Any(i => value(i) is null) ? null : items.Sum(i => value(i)!.Value);
    }

    private static XLCellValue Cell(decimal? value) => value is { } v ? v : Blank.Value;

    private static string YesNo(bool value) => value ? "Sí" : "No";

    /// <summary>Pone entre comillas los campos que llevan ";", comillas o saltos de línea.</summary>
    private static string Escape(string field) =>
        field.IndexOfAny([';', '"', '\n', '\r']) >= 0
            ? $"\"{field.Replace("\"", "\"\"")}\""
            : field;
}
