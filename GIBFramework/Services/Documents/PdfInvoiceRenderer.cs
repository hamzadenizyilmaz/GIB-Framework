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
    private static readonly (double R, double G, double B) Band = (0.957, 0.969, 0.984);

    private static readonly Dictionary<string, string> Units = new(StringComparer.Ordinal)
    {
        ["C62"] = "Adet", ["NIU"] = "Adet", ["KGM"] = "Kg", ["GRM"] = "g", ["LTR"] = "Lt", ["MTR"] = "m", ["MTK"] = "m2", ["MTQ"] = "m3",
        ["HUR"] = "Saat", ["DAY"] = "Gün", ["MON"] = "Ay", ["ANN"] = "Yıl", ["PA"] = "Paket", ["BX"] = "Kutu", ["SET"] = "Set", ["KWH"] = "kWh",
    };

    private sealed record Column(string Title, double X, double Width, bool Numeric);

    private static readonly Column[] Columns =
    [
        new("#", Left, 18, false),
        new("Mal / hizmet", Left + 20, 190, false),
        new("Miktar", Left + 212, 58, true),
        new("Birim fiyat", Left + 272, 70, true),
        new("İskonto", Left + 344, 52, true),
        new("KDV", Left + 398, 50, true),
        new("Tutar", Left + 450, 65, true),
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

        if (logoName is not null)
        {
            var scale = Math.Min(150.0 / logoImage!.Width, 52.0 / logoImage.Height);
            page.Image(logoName, Left, y, logoImage.Width * scale, logoImage.Height * scale);
            y += (logoImage.Height * scale) + 8;
        }

        var leftY = y;
        foreach (var line in PdfText.Wrap(s.Title, 300, 11, bold: true))
        {
            page.Text(Left, leftY, line, 11, bold: true);
            leftY += 14;
        }

        foreach (var line in Lines(
            PdfText.Wrap(s.FullAddress, 300, 8.5),
            [$"{(s.TaxId.Length == 10 ? "VKN" : "TCKN")}: {s.TaxId}{(string.IsNullOrWhiteSpace(s.TaxOffice) ? string.Empty : "  ·  Vergi Dairesi: " + s.TaxOffice)}"],
            [string.Join("  ·  ", new[] { s.Phone, s.Email }.Where(x => !string.IsNullOrWhiteSpace(x)))]))
        {
            page.Text(Left, leftY, line, 8.5, gray: 0.42);
            leftY += 11.5;
        }

        page.TextRight(Right, 40, documentLabel.ToUpper(Tr), 16, bold: true);
        page.TextRight(Right, 61, number, 10.5, bold: true, gray: 0.3);
        double rightY = 78;
        if (invoice.DocumentNumber is null)
        {
            page.TextRight(Right, rightY, "Taslak – henüz düzenlenmedi", 8.5, gray: 0.45);
            rightY += 12;
        }

        if (qr is { Count: > 0 })
        {
            page.Qr(qr, Right - 86, rightY + 4, 86);
            rightY += 96;
        }

        y = Math.Max(leftY, rightY) + 12;
        page.Line(Left, y, Right, y, 1, 0.88);
        y += 12;

        var boxTop = y;
        var buyer = new List<(string Text, bool Bold, double Size, double Gray)> { ("SAYIN", true, 7.5, 0.45) };
        buyer.AddRange(PdfText.Wrap(c.Title, 225, 10, bold: true).Select(l => (l, true, 10.0, 0.12)));
        buyer.AddRange(PdfText.Wrap(c.FullAddress, 225, 8.5).Select(l => (l, false, 8.5, 0.25)));
        buyer.Add(($"{(c.TaxId.Length == 10 ? "VKN" : "TCKN")}: {c.TaxId}", false, 8.5, 0.42));
        if (!string.IsNullOrWhiteSpace(c.TaxOffice))
        {
            buyer.Add(($"Vergi Dairesi: {c.TaxOffice}", false, 8.5, 0.42));
        }

        if (!string.IsNullOrWhiteSpace(c.Email))
        {
            buyer.Add((c.Email, false, 8.5, 0.42));
        }

        var meta = new List<(string Label, string Value)>
        {
            ("Senaryo", invoice.Profile.ToString()),
            ("Fatura tipi", invoice.TypeCode.ToString()),
            ("Düzenleme", invoice.IssueDate.ToString("dd.MM.yyyy", Tr) + " " + invoice.IssueTime.ToString("HH:mm", Tr)),
            ("ETTN", invoice.Uuid.ToString()),
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

        var buyerHeight = buyer.Sum(b => b.Size + 3.5) + 16;
        var metaHeight = (meta.Count * 13) + 30;
        var boxHeight = Math.Max(buyerHeight, metaHeight);
        page.StrokeRect(Left, boxTop, 250, boxHeight, 0.6, 0.86);
        page.StrokeRect(Left + 265, boxTop, Right - Left - 265, boxHeight, 0.6, 0.86);
        var by = boxTop + 9;
        foreach (var (text, bold, size, gray) in buyer)
        {
            page.Text(Left + 10, by, text, size, bold, gray);
            by += size + 3.5;
        }

        var my = boxTop + 9;
        page.Text(Left + 275, my, "BELGE BİLGİLERİ", 7.5, bold: true, gray: 0.45);
        my += 14;
        foreach (var (label, value) in meta)
        {
            page.Text(Left + 275, my, label, 8.5, gray: 0.45);
            page.Text(Left + 345, my, value, label == "ETTN" ? 7.6 : 8.5, gray: 0.15);
            my += 13;
        }

        y = boxTop + boxHeight + 16;
        y = TableHeader(page, y);
        foreach (var l in invoice.Lines.OrderBy(l => l.LineNo))
        {
            var nameLines = PdfText.Wrap(l.Name, Columns[1].Width - 4, 8.8, bold: true).ToList();
            var descLines = string.IsNullOrWhiteSpace(l.Description) ? new List<string>() : PdfText.Wrap(l.Description, Columns[1].Width - 4, 7.8).ToList();
            var rowHeight = Math.Max(24, (nameLines.Count * 11) + (descLines.Count * 10) + 10);
            if (y + rowHeight > Bottom)
            {
                page = pdf.AddPage();
                y = TableHeader(page, 40);
            }

            var ty = y + 6;
            page.Text(Columns[0].X + 2, ty, l.LineNo.ToString(CultureInfo.InvariantCulture), 8.5, gray: 0.4);
            foreach (var n in nameLines)
            {
                page.Text(Columns[1].X, ty, n, 8.8, bold: true);
                ty += 11;
            }

            foreach (var d in descLines)
            {
                page.Text(Columns[1].X, ty, d, 7.8, gray: 0.45);
                ty += 10;
            }

            var unit = Units.TryGetValue(l.UnitCode, out var u) ? u : l.UnitCode;
            Cell(page, Columns[2], y + 6, $"{l.Quantity.ToString("0.####", Tr)} {unit}", 8.5);
            Cell(page, Columns[3], y + 6, M(l.UnitPrice), 8.5);
            Cell(page, Columns[4], y + 6, l.DiscountAmount > 0 ? M(l.DiscountAmount) : "–", 8.5);
            Cell(page, Columns[5], y + 6, "%" + l.VatRate.ToString("0.##", Tr), 8.5);
            Cell(page, Columns[5], y + 17, M(l.VatAmount), 7.2, gray: 0.45);
            Cell(page, Columns[6], y + 6, M(l.LineExtensionAmount), 8.8, bold: true);
            y += rowHeight;
            page.Line(Left, y, Right, y, 0.5, 0.9);
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
        totals.AddRange(t.VatSubtotals.Select(v => ($"{v.TaxName} (%{v.Percent.ToString("0.##", Tr)})", M(v.TaxAmount), false)));
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
            if (grand)
            {
                page.Line(Right - 250, y - 2, Right, y - 2, 1.2, 0.15);
                y += 3;
            }

            var size = grand ? 11 : 8.8;
            page.Text(Right - 250, y, label, size, grand, grand ? 0.1 : 0.3);
            page.TextRight(Right, y, value, size, grand, grand ? 0.1 : 0.2);
            y += grand ? 18 : 14;
        }

        if (invoice.Notes.Count > 0)
        {
            y += 8;
            var noteLines = invoice.Notes.SelectMany(n => PdfText.Wrap(n, Right - Left, 8.3)).ToList();
            if (y + 16 > Bottom)
            {
                page = pdf.AddPage();
                y = 40;
            }

            page.Text(Left, y, "Notlar", 9, bold: true);
            y += 14;
            foreach (var n in noteLines)
            {
                if (y + 11 > Bottom)
                {
                    page = pdf.AddPage();
                    y = 40;
                }

                page.Text(Left, y, n, 8.3, gray: 0.3);
                y += 11;
            }
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
        page.Rect(Left, y, Right - Left, 20, Band);
        foreach (var col in Columns)
        {
            if (col.Numeric)
            {
                page.TextRight(col.X + col.Width - 2, y + 6.5, col.Title.ToUpper(Tr), 7, bold: true, gray: 0.42);
            }
            else
            {
                page.Text(col.X + 2, y + 6.5, col.Title.ToUpper(Tr), 7, bold: true, gray: 0.42);
            }
        }

        page.Line(Left, y + 20, Right, y + 20, 1, 0.75);
        return y + 20;
    }

    private static void Cell(PdfPage page, Column column, double top, string text, double size, bool bold = false, double gray = 0.15) =>
        page.TextRight(column.X + column.Width - 2, top, text, size, bold, gray);

    private static IEnumerable<string> Lines(params IEnumerable<string>[] groups) =>
        groups.SelectMany(g => g).Where(l => !string.IsNullOrWhiteSpace(l));
}
