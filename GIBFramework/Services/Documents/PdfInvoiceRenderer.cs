using System.Globalization;
using GIBFramework.Services.Documents.Pdf;

namespace GIBFramework.Services.Documents;

public sealed record PdfLogo(string ContentType, byte[] Data);

public static class PdfInvoiceRenderer
{
    private const double Left = 40;
    private const double Right = 555;
    private const double Bottom = 790;
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private static readonly Dictionary<string, string> Units = new(StringComparer.Ordinal)
    {
        ["C62"] = "Adet", ["NIU"] = "Adet", ["KGM"] = "Kg", ["GRM"] = "g", ["LTR"] = "Lt", ["MTR"] = "m", ["MTK"] = "m2", ["MTQ"] = "m3",
        ["HUR"] = "Saat", ["DAY"] = "Gün", ["MON"] = "Ay", ["ANN"] = "Yıl", ["PA"] = "Paket", ["BX"] = "Kutu", ["SET"] = "Set", ["KWH"] = "kWh",
    };

    private sealed record Column(string Title, double X, double Width, bool Numeric);

    private static readonly Column[] Columns =
    [
        new("No", Left, 24, false),
        new("Ürün kodu", Left + 24, 72, false),
        new("Mal / hizmet açıklaması", Left + 96, 137, false),
        new("Miktar", Left + 233, 46, true),
        new("Birim fiyat", Left + 279, 57, true),
        new("İskonto", Left + 336, 48, true),
        new("KDV oranı", Left + 384, 38, true),
        new("KDV tutarı", Left + 422, 45, true),
        new("Tutar", Left + 467, 48, true),
    ];

    public static byte[] Render(Invoice invoice, PdfLogo? logo, IReadOnlyList<bool[]>? qr, string documentLabel, string number)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        var pdf = new PdfBuilder { Title = $"{documentLabel} {number}", Author = invoice.Supplier.Title };
        var logoImage = logo is null ? null : PdfImages.Load(logo.Data, logo.ContentType);
        var logoName = logoImage is null ? null : pdf.AddImage(logoImage);
        var cur = invoice.Currency;
        string M(decimal v) => v.ToString("N2", Tr) + (cur == "TRY" ? " TL" : " " + cur);

        var page = pdf.AddPage();
        var s = invoice.Supplier;
        var c = invoice.Customer;
        double y = 40;
        const double partyWidth = 196;

        if (logoName is not null)
        {
            var scale = Math.Min(150.0 / logoImage!.Width, 42.0 / logoImage.Height);
            page.Image(logoName, Left, y, logoImage.Width * scale, logoImage.Height * scale);
            y += (logoImage.Height * scale) + 8;
        }

        page.Line(Left, y, Left + partyWidth, y, 1.25, 0);
        var leftY = y + 6;
        foreach (var line in PdfText.Wrap(s.Title, partyWidth, 9, bold: true))
        {
            page.Text(Left, leftY, line, 9, bold: true);
            leftY += 11;
        }

        foreach (var line in Lines(
            PdfText.Wrap(s.FullAddress, partyWidth, 8),
            PdfText.Wrap($"Vergi dairesi: {s.TaxOffice}", partyWidth, 8),
            [$"{(s.TaxId.Length == 10 ? "VKN" : "TCKN")}: {s.TaxId}"],
            new[] { ("Telefon", s.Phone), ("Faks", s.Fax), ("E-posta", s.Email), ("Web sitesi", s.Website),
                ("Ticaret sicil no", s.TradeRegistryNumber), ("MERSİS no", s.MersisNumber) }
                .Where(field => !string.IsNullOrWhiteSpace(field.Item2))
                .SelectMany(field => PdfText.Wrap($"{field.Item1}: {field.Item2}", partyWidth, 8))))
        {
            page.Text(Left, leftY, line, 8);
            leftY += 10;
        }

        const double titleCenter = 300;
        page.Text(titleCenter - PdfText.Measure(documentLabel, 13, true) / 2, 121, documentLabel, 13, bold: true, gray: 0);
        if (invoice.DocumentNumber is null)
        {
            const string draft = "Taslak - henüz düzenlenmedi";
            page.Text(titleCenter - PdfText.Measure(draft, 8, false) / 2, 139, draft, 8, gray: 0.38);
        }

        if (qr is { Count: > 0 })
        {
            page.Qr(qr, Right - 96, 78, 96);
        }

        y = leftY + 4;
        page.Line(Left, y, Left + partyWidth, y, 1.25, 0);
        y += 10;
        page.Line(Left, y, Left + partyWidth, y, 1.25, 0);
        var buyer = new List<(string Text, bool Bold, double Size, double Gray)> { ("SAYIN", true, 8, 0) };
        buyer.AddRange(PdfText.Wrap(c.Title, partyWidth, 9, bold: true).Select(l => (l, true, 9.0, 0.12)));
        buyer.AddRange(PdfText.Wrap(c.FullAddress, partyWidth, 8).Select(l => (l, false, 8.0, 0.12)));
        buyer.Add(($"{(c.TaxId.Length == 10 ? "VKN" : "TCKN")}: {c.TaxId}", false, 8.5, 0.42));
        if (!string.IsNullOrWhiteSpace(c.TaxOffice))
        {
            buyer.Add(($"Vergi Dairesi: {c.TaxOffice}", false, 8.5, 0.42));
        }

        if (!string.IsNullOrWhiteSpace(c.Email))
        {
            buyer.AddRange(PdfText.Wrap($"E-posta: {c.Email}", partyWidth, 8).Select(l => (l, false, 8.0, 0.12)));
        }

        var meta = new List<(string Label, string Value)>
        {
            ("Özelleştirme no", Ubl.UblInvoiceWriter.CustomizationId),
            ("Senaryo", invoice.Profile.ToString()),
            ("Fatura tipi", invoice.TypeCode.ToString()),
            (invoice.DocumentNumber is null ? "Taslak no" : "Fatura no", number),
            ("Düzenleme tarihi", invoice.IssueDate.ToString("dd.MM.yyyy", Tr)),
            ("Düzenleme saati", invoice.IssueTime.ToString("HH:mm:ss", Tr)),
        };
        if (!string.IsNullOrWhiteSpace(invoice.OrderNumber))
        {
            meta.Add(("Sipariş no", invoice.OrderNumber));
        }

        if (!string.IsNullOrWhiteSpace(invoice.DespatchNumber))
        {
            meta.Add(("İrsaliye no", invoice.DespatchNumber));
        }

        meta.Add(("Para birimi", cur + (invoice.ExchangeRate is { } rate ? $" (kur {rate.ToString("N4", Tr)})" : string.Empty)));

        var by = y + 6;
        foreach (var (text, bold, size, gray) in buyer)
        {
            page.Text(Left, by, text, size, bold, gray);
            by += size + 2.5;
        }
        page.Line(Left, by + 2, Left + partyWidth, by + 2, 1.25, 0);
        page.Text(Left, by + 10, $"ETTN: {invoice.Uuid:D}", 8, bold: true, gray: 0);

        var my = Math.Max(195, y);
        const double metaLeft = Right - 177;
        const double metaDivider = metaLeft + 77;
        foreach (var (label, value) in meta)
        {
            var values = PdfText.Wrap(value, Right - metaDivider - 6, 7.6).ToArray();
            var height = Math.Max(13, values.Length * 10 + 3);
            page.StrokeRect(metaLeft, my, Right - metaLeft, height, 0.6, 0);
            page.Line(metaDivider, my, metaDivider, my + height, 0.6, 0);
            page.Text(metaLeft + 3, my + 3, label, 7.6, bold: true, gray: 0);
            for (var row = 0; row < values.Length; row++) page.Text(metaDivider + 3, my + 3 + row * 10, values[row], 7.6);
            my += height;
        }

        y = Math.Max(by + 28, my + 16);
        y = TableHeader(page, y);
        foreach (var l in invoice.Lines.OrderBy(l => l.LineNo))
        {
            var codeLines = PdfText.Wrap(l.ProductCode ?? "", Columns[1].Width - 4, 7.8).ToList();
            var nameLines = PdfText.Wrap(l.Name, Columns[2].Width - 4, 8.3, bold: true).ToList();
            var descLines = string.IsNullOrWhiteSpace(l.Description) ? new List<string>() : PdfText.Wrap(l.Description, Columns[2].Width - 4, 7.8).ToList();
            var rowHeight = Math.Max(18, Math.Max(codeLines.Count * 10, nameLines.Count * 10 + descLines.Count * 10) + 8);
            if (y + rowHeight > Bottom)
            {
                page = pdf.AddPage();
                y = TableHeader(page, 40);
            }

            var ty = y + 4;
            page.Text(Columns[0].X + 2, ty, l.LineNo.ToString(CultureInfo.InvariantCulture), 8);
            for (var row = 0; row < codeLines.Count; row++) page.Text(Columns[1].X + 2, ty + row * 10, codeLines[row], 7.8);
            foreach (var n in nameLines)
            {
                page.Text(Columns[2].X + 2, ty, n, 8.3, bold: true);
                ty += 10;
            }

            foreach (var d in descLines)
            {
                page.Text(Columns[2].X + 2, ty, d, 7.8);
                ty += 10;
            }

            var unit = Units.TryGetValue(l.UnitCode, out var u) ? u : l.UnitCode;
            Cell(page, Columns[3], y + 4, $"{l.Quantity.ToString("0.####", Tr)} {unit}", 7.5);
            Cell(page, Columns[4], y + 4, M(l.UnitPrice), 7.5);
            Cell(page, Columns[5], y + 4, l.DiscountAmount > 0 ? M(l.DiscountAmount) : "-", 7.5);
            Cell(page, Columns[6], y + 4, "%" + l.VatRate.ToString("0.##", Tr), 7.5);
            Cell(page, Columns[7], y + 4, M(l.VatAmount), 7.5);
            Cell(page, Columns[8], y + 4, M(l.LineExtensionAmount), 7.5, bold: true);
            TableGrid(page, y, rowHeight);
            y += rowHeight;
        }

        var t = invoice.Totals;
        var totals = new List<(string Label, string Value, bool Grand)>
        {
            ("Mal / hizmet toplamı", M(t.LineExtensionAmount + t.AllowanceTotalAmount), false),
        };
        if (t.AllowanceTotalAmount > 0)
        {
            totals.Add(("Toplam iskonto", "−" + M(t.AllowanceTotalAmount), false));
        }

        totals.Add(("Vergi hariç tutar", M(t.TaxExclusiveAmount), false));
        foreach (var vat in t.VatSubtotals)
        {
            var vatRate = vat.Percent.ToString("0.##", Tr);
            totals.Add(($"Hesaplanan KDV matrahı (%{vatRate})", M(vat.TaxableAmount), false));
            totals.Add(($"{vat.TaxName} (%{vatRate})", M(vat.TaxAmount), false));
        }
        if (t.WithholdingTotal > 0)
        {
            totals.Add(("Tevkifat", "−" + M(t.WithholdingTotal), false));
        }

        totals.Add(("Vergiler dahil toplam", M(t.TaxInclusiveAmount), false));
        totals.Add(("Ödenecek tutar", M(t.PayableAmount), true));

        var needed = (totals.Count * 15) + 30;
        if (y + needed > Bottom)
        {
            page = pdf.AddPage();
            y = 40;
        }

        y += 12;
        foreach (var (label, value, grand) in totals)
        {
            const double boundary = Right - 77;
            var lines = PdfText.Wrap(label, 175, 8, true).ToArray();
            var height = Math.Max(14, lines.Length * 10 + 3);
            if (y + height > Bottom) { page = pdf.AddPage(); y = 40; }
            page.StrokeRect(Right - 260, y, 260, height, 0.6, 0);
            page.Line(boundary, y, boundary, y + height, 0.6, 0);
            for (var row = 0; row < lines.Length; row++) page.TextRight(boundary - 4, y + 3 + row * 10, lines[row], 8, bold: true, gray: 0);
            page.TextRight(Right - 4, y + 3, value, 8, grand, 0);
            y += height;
        }

        var words = InvoicePrintDetails.TryAmountInWords(invoice);
        var bankRows = InvoicePrintDetails.BankRows(invoice);
        var printableNotes = bankRows.Count == 0 ? invoice.Notes : invoice.Notes.Where(note => !InvoicePrintDetails.IsBankNote(note)).ToList();
        if (printableNotes.Count > 0 || words is not null)
        {
            y += 8;
            IEnumerable<string> notes = words is null ? printableNotes : new[] { $"Yalnız: {words}" }.Concat(printableNotes);
            var noteLines = notes.SelectMany(n => PdfText.Wrap(n, Right - Left - 10, 8.3)).ToList();
            if (y + 16 > Bottom)
            {
                page = pdf.AddPage();
                y = 40;
            }

            var noteTop = y;
            y += 7;
            foreach (var n in noteLines)
            {
                if (y + 16 > Bottom)
                {
                    page.StrokeRect(Left, noteTop, Right - Left, y + 4 - noteTop, 0.65, 0);
                    page = pdf.AddPage();
                    noteTop = 40;
                    y = 47;
                }

                page.Text(Left + 5, y, n, 8.3, gray: 0);
                y += 11;
            }
            page.StrokeRect(Left, noteTop, Right - Left, y + 4 - noteTop, 0.65, 0);
            y += 4;
        }

        var hasReturns = InvoicePrintDetails.HasReturnSection(invoice);
        if (bankRows.Count > 0 || hasReturns)
        {
            double[] widths = [155, 145, 145, 70];
            void DrawBankRow(IReadOnlyList<string> cells, double height, bool header)
            {
                var x = Left;
                page.StrokeRect(Left, y, Right - Left, height, 0.5, 0.4);
                for (var col = 0; col < cells.Count; col++)
                {
                    var top = y + 4;
                    foreach (var line in PdfText.Wrap(cells[col], widths[col] - 8, 8, header))
                    {
                        page.Text(x + 4, top, line, 8, bold: header);
                        top += 10;
                    }
                    x += widths[col];
                    if (col + 1 < cells.Count) page.Line(x, y, x, y + height, 0.5, 0.4);
                }
                y += height;
            }

            y += 12;
            if (bankRows.Count == 0)
            {
                if (y + 30 > Bottom) { page = pdf.AddPage(); y = 40; }
                DrawBankRow(InvoicePrintDetails.BankColumns, 16, true);
                DrawBankRow(["", "", "", ""], 14, false);
            }
            foreach (var bank in bankRows)
            {
                var rowHeight = 8 + 10 * bank.Select((cell, col) => PdfText.Wrap(cell, widths[col] - 8, 8).Count()).Max();
                if (y + rowHeight + 16 > Bottom)
                {
                    page = pdf.AddPage();
                    y = 40;
                }
                if (bank == bankRows[0] || y == 40)
                    DrawBankRow(InvoicePrintDetails.BankColumns, 16, true);
                DrawBankRow(bank, rowHeight, false);
            }
        }

        if (hasReturns)
        {
            if (y + 114 > Bottom)
            {
                page = pdf.AddPage();
                y = 40;
            }
            y += 14;
            page.Text(Left, y, "İade alanı", 9, bold: true);
            y += 14;
            double[] widths = [52, 53, 60, 48, 58, 49, 141, 54];
            var x = Left;
            for (var i = 0; i < InvoicePrintDetails.ReturnColumns.Length; i++)
            {
                page.Text(x + 4, y + 4, InvoicePrintDetails.ReturnColumns[i], 7.5, bold: true);
                page.Line(x, y, x, y + 84, 0.5, 0);
                x += widths[i];
            }
            page.Line(Right, y, Right, y + 84, 0.5, 0);
            for (var row = 0; row <= 7; row++)
                page.Line(Left, y + row * 12, Right, y + row * 12, 0.5, 0);
        }

        var total = pdf.Pages.Count;
        for (var i = 0; i < total; i++)
        {
            var p = pdf.Pages[i];
            p.Line(Left, 806, Right, 806, 0.5, 0.88);
            p.Text(Left, 812, $"{s.Title}  ·  ETTN {invoice.Uuid}", 7, gray: 0.5);
            p.TextRight(Right, 812, $"Sayfa {i + 1} / {total}", 7, gray: 0.5);
        }

        return pdf.Build();
    }

    private static double TableHeader(PdfPage page, double y)
    {
        TableGrid(page, y, 22);
        foreach (var col in Columns)
        {
            if (col.Numeric)
            {
                page.TextRight(col.X + col.Width - 2, y + 7, col.Title, 7, bold: true, gray: 0);
            }
            else
            {
                page.Text(col.X + 2, y + 7, col.Title, 7, bold: true, gray: 0);
            }
        }

        return y + 22;
    }

    private static void TableGrid(PdfPage page, double top, double height)
    {
        page.StrokeRect(Left, top, Right - Left, height, .55, 0);
        foreach (var column in Columns.Skip(1))
            page.Line(column.X, top, column.X, top + height, .55, 0);
    }

    private static void Cell(PdfPage page, Column column, double top, string text, double size, bool bold = false, double gray = 0.15)
    {
        var width = PdfText.Measure(text, size, bold);
        var fitted = width > column.Width - 4 ? size * (column.Width - 4) / width : size;
        page.TextRight(column.X + column.Width - 2, top, text, fitted, bold, gray);
    }

    private static IEnumerable<string> Lines(params IEnumerable<string>[] groups) =>
        groups.SelectMany(g => g).Where(l => !string.IsNullOrWhiteSpace(l));
}
