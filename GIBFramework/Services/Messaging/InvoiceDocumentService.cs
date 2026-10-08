using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using GIBFramework.DAL.Messaging;
using GIBFramework.Services.Invoices;

namespace GIBFramework.Services.Messaging;

public sealed class InvoiceDocumentService(
    InvoiceService invoices,
    TenantLogoRepository logos,
    Qr.QrCodeService qr,
    IDataProtectionProvider dataProtection,
    AppOptions app)
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromDays(400);
    public static readonly TimeSpan ArchiveLinkLifetime = TimeSpan.FromDays(3700);

    private static readonly Dictionary<string, string> Units = new(StringComparer.Ordinal)
    {
        ["C62"] = "Adet", ["NIU"] = "Adet", ["KGM"] = "Kg", ["GRM"] = "g", ["LTR"] = "Lt", ["MTR"] = "m", ["MTK"] = "m²", ["MTQ"] = "m³",
        ["HUR"] = "Saat", ["DAY"] = "Gün", ["MON"] = "Ay", ["ANN"] = "Yıl", ["PA"] = "Paket", ["BX"] = "Kutu", ["SET"] = "Set", ["KWH"] = "kWh",
    };

    private ITimeLimitedDataProtector Protector => dataProtection.CreateProtector("GIBFramework.InvoiceLink.v1").ToTimeLimitedDataProtector();

    public string PublicLink(Guid tenantId, Guid invoiceId, TimeSpan? lifetime = null) => $"{app.BaseUrl}/f/{Token(tenantId, invoiceId, lifetime)}";

    public string PdfLink(Guid tenantId, Guid invoiceId, TimeSpan? lifetime = null) => $"{PublicLink(tenantId, invoiceId, lifetime)}/fatura.pdf";

    public string PanelLink(Guid invoiceId) => $"{app.BaseUrl}/#/invoices/{invoiceId}";

    public string Token(Guid tenantId, Guid invoiceId, TimeSpan? lifetime = null)
    {
        var bytes = new byte[32];
        tenantId.TryWriteBytes(bytes.AsSpan(0, 16));
        invoiceId.TryWriteBytes(bytes.AsSpan(16, 16));
        return Base64Url(Protector.Protect(bytes, DateTimeOffset.UtcNow.Add(lifetime ?? LinkLifetime)));
    }

    public (Guid TenantId, Guid InvoiceId)? ReadToken(string token)
    {
        try
        {
            var bytes = Protector.Unprotect(FromBase64Url(token), out _);
            return bytes.Length == 32 ? (new Guid(bytes.AsSpan(0, 16)), new Guid(bytes.AsSpan(16, 16))) : null;
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or FormatException)
        {
            return null;
        }
    }

    public async Task<Invoice?> LoadAsync(Guid tenantId, Guid invoiceId, CancellationToken ct) => await invoices.FindAsync(tenantId, invoiceId, ct);

    public Task<byte[]?> SignedXmlAsync(Invoice invoice, CancellationToken ct) => invoices.TryGetSignedXmlAsync(invoice, ct);

    public static string FileBaseName(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        return invoice.DocumentNumber ?? invoice.DraftNumber ?? invoice.Id.ToString("N");
    }

    public static string DocumentTypeLabel(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        return invoice.DocumentType == EDocumentType.EFatura ? "e-Fatura" : "e-Arşiv Fatura";
    }

    public async Task<byte[]> RenderPdfAsync(Invoice invoice, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        var logo = await logos.GetAsync(invoice.TenantId, ct);
        var modules = invoice.SignedXmlSha256 is null ? null : qr.Modules(qr.BuildPayload(invoice));
        return Documents.PdfInvoiceRenderer.Render(invoice, logo is null ? null : new Documents.PdfLogo(logo.ContentType, logo.Data), modules,
            DocumentTypeLabel(invoice), FileBaseName(invoice));
    }

    public static string Money(decimal value, string currency) =>
        value.ToString("N2", Tr) + (currency == "TRY" ? " ₺" : " " + currency);

    public async Task<string> RenderHtmlAsync(Invoice invoice, bool publicView, string? token, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        var logo = await logos.GetAsync(invoice.TenantId, ct);
        var qr = await invoices.TryGetQrPngAsync(invoice, ct);
        var hasXml = invoice.SignedXmlSha256 is not null;
        var s = invoice.Supplier;
        var c = invoice.Customer;
        var cur = invoice.Currency;
        static string E(string? v) => WebUtility.HtmlEncode(v ?? string.Empty);
        string M(decimal v) => E(Money(v, cur));

        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"tr\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append("<meta name=\"robots\" content=\"noindex,nofollow\">");
        sb.Append("<title>").Append(E($"{DocumentTypeLabel(invoice)} {FileBaseName(invoice)}")).Append("</title><style>");
        sb.Append("""
            *{box-sizing:border-box}body{margin:0;background:#eef2f7;font-family:'Segoe UI',Arial,Helvetica,sans-serif;color:#1f2933;font-size:13px}
            .page{max-width:900px;margin:24px auto;background:#fff;border-radius:12px;box-shadow:0 4px 24px rgba(15,40,80,.08);padding:36px 40px}
            .bar{display:flex;justify-content:space-between;gap:12px;align-items:center;max-width:900px;margin:18px auto 0;padding:0 8px}
            .bar a{display:inline-block;background:#0778e6;color:#fff;text-decoration:none;padding:9px 18px;border-radius:20px;font-weight:600;font-size:13px}
            .bar span{color:#6b7684;font-size:12px}
            .head{display:flex;justify-content:space-between;gap:24px;align-items:flex-start;border-bottom:2px solid #eef1f5;padding-bottom:18px}
            .logo{max-height:64px;max-width:220px}.title{text-align:right}.title h1{margin:0;font-size:22px;letter-spacing:.02em}
            .title .no{font-family:Consolas,monospace;font-size:15px;margin-top:4px}.muted{color:#6b7684}
            .grid{display:grid;grid-template-columns:1fr 1fr;gap:20px;margin:20px 0}.box{border:1px solid #e6ebf1;border-radius:10px;padding:14px 16px}
            .box h2{margin:0 0 8px;font-size:11px;text-transform:uppercase;letter-spacing:.06em;color:#6b7684}.box .name{font-weight:700;font-size:14px;margin-bottom:4px}
            table.meta{width:100%;border-collapse:collapse}table.meta td{padding:3px 0;vertical-align:top}table.meta td:first-child{color:#6b7684;width:42%}
            table.lines{width:100%;border-collapse:collapse;margin-top:8px}table.lines th{background:#f4f7fb;text-align:left;font-size:11px;text-transform:uppercase;letter-spacing:.04em;color:#6b7684;padding:9px 8px}
            table.lines td{padding:9px 8px;border-bottom:1px solid #eef1f5}.num{text-align:right;white-space:nowrap;font-variant-numeric:tabular-nums}
            .totals{display:flex;justify-content:space-between;gap:24px;margin-top:18px;align-items:flex-end}.totals table{border-collapse:collapse;min-width:320px}
            .totals td{padding:5px 0}.totals td.num{padding-left:24px}.totals tr.grand td{border-top:2px solid #1f2933;font-weight:700;font-size:15px;padding-top:9px}
            .qr{width:120px;height:120px;image-rendering:pixelated}.notes{margin-top:18px;border-top:1px solid #eef1f5;padding-top:12px}
            .foot{margin-top:22px;font-size:11px;color:#6b7684;display:flex;justify-content:space-between;gap:12px;flex-wrap:wrap}
            @media (max-width:640px){.page{padding:22px 18px;margin:0;border-radius:0}.grid{grid-template-columns:1fr}.head{flex-direction:column}.title{text-align:left}
            table.lines th:nth-child(5),table.lines td:nth-child(5){display:none}.totals{flex-direction:column;align-items:stretch}.totals table{min-width:0;width:100%}}
            @media print{body{background:#fff}.bar{display:none}.page{box-shadow:none;margin:0;max-width:none;border-radius:0}}
            """);
        sb.Append("</style></head><body>");
        if (publicView && token is not null && !hasXml)
        {
            sb.Append("<div class=\"bar\"><span>").Append(E(s.Title)).Append("</span><span><a href=\"/f/").Append(E(token)).Append("/fatura.pdf\">PDF indir</a></span></div>");
        }

        if (publicView && token is not null && hasXml)
        {
            sb.Append("<div class=\"bar\"><span>").Append(E(s.Title)).Append("</span><span><a href=\"/f/").Append(E(token)).Append("/fatura.pdf\">PDF indir</a> <a href=\"/f/").Append(E(token)).Append("/xml\">UBL-TR XML indir</a></span></div>");
        }

        sb.Append("<div class=\"page\"><div class=\"head\"><div>");
        if (logo is not null)
        {
            sb.Append("<img class=\"logo\" alt=\"").Append(E(s.Title)).Append("\" src=\"data:").Append(E(logo.ContentType)).Append(";base64,")
                .Append(Convert.ToBase64String(logo.Data)).Append("\"><br>");
        }

        sb.Append("<div class=\"name\" style=\"font-weight:700;font-size:15px;margin-top:6px\">").Append(E(s.Title)).Append("</div>");
        sb.Append("<div class=\"muted\">").Append(E(s.FullAddress)).Append("</div>");
        sb.Append("<div class=\"muted\">").Append(s.TaxId.Length == 10 ? "VKN" : "TCKN").Append(": ").Append(E(s.TaxId));
        if (!string.IsNullOrWhiteSpace(s.TaxOffice))
        {
            sb.Append(" · Vergi Dairesi: ").Append(E(s.TaxOffice));
        }

        sb.Append("</div>");
        if (!string.IsNullOrWhiteSpace(s.Email) || !string.IsNullOrWhiteSpace(s.Phone))
        {
            sb.Append("<div class=\"muted\">").Append(E(string.Join(" · ", new[] { s.Phone, s.Email }.Where(x => !string.IsNullOrWhiteSpace(x))))).Append("</div>");
        }

        sb.Append("</div><div class=\"title\"><h1>").Append(E(DocumentTypeLabel(invoice).ToUpper(Tr))).Append("</h1>");
        sb.Append("<div class=\"no\">").Append(E(FileBaseName(invoice))).Append("</div>");
        if (invoice.DocumentNumber is null)
        {
            sb.Append("<div class=\"muted\" style=\"margin-top:4px\">Taslak – henüz düzenlenmedi</div>");
        }

        if (qr is not null)
        {
            sb.Append("<img class=\"qr\" alt=\"Karekod\" src=\"data:image/png;base64,").Append(Convert.ToBase64String(qr)).Append("\">");
        }

        sb.Append("</div></div><div class=\"grid\"><div class=\"box\"><h2>Sayın</h2><div class=\"name\">").Append(E(c.Title)).Append("</div>");
        sb.Append("<div>").Append(E(c.FullAddress)).Append("</div>");
        sb.Append("<div class=\"muted\">").Append(c.TaxId.Length == 10 ? "VKN" : "TCKN").Append(": ").Append(E(c.TaxId));
        if (!string.IsNullOrWhiteSpace(c.TaxOffice))
        {
            sb.Append(" · Vergi Dairesi: ").Append(E(c.TaxOffice));
        }

        sb.Append("</div>");
        if (!string.IsNullOrWhiteSpace(c.Email))
        {
            sb.Append("<div class=\"muted\">").Append(E(c.Email)).Append("</div>");
        }

        sb.Append("</div><div class=\"box\"><h2>Belge bilgileri</h2><table class=\"meta\">");
        void Row(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                sb.Append("<tr><td>").Append(E(label)).Append("</td><td>").Append(E(value)).Append("</td></tr>");
            }
        }

        Row("Senaryo", invoice.Profile.ToString());
        Row("Fatura tipi", invoice.TypeCode.ToString());
        Row("Düzenleme tarihi", invoice.IssueDate.ToString("dd.MM.yyyy", Tr) + " " + invoice.IssueTime.ToString("HH:mm", Tr));
        Row("ETTN", invoice.Uuid.ToString());
        Row("Sipariş no", invoice.OrderNumber);
        Row("İrsaliye no", invoice.DespatchNumber);
        Row("Para birimi", cur + (invoice.ExchangeRate is { } rate ? $" (kur {rate.ToString("N4", Tr)})" : string.Empty));
        sb.Append("</table></div></div>");

        sb.Append("<table class=\"lines\"><thead><tr><th>#</th><th>Mal / hizmet</th><th class=\"num\">Miktar</th><th class=\"num\">Birim fiyat</th><th class=\"num\">İskonto</th><th class=\"num\">KDV</th><th class=\"num\">Tutar</th></tr></thead><tbody>");
        foreach (var l in invoice.Lines.OrderBy(l => l.LineNo))
        {
            sb.Append("<tr><td>").Append(l.LineNo).Append("</td><td><strong>").Append(E(l.Name)).Append("</strong>");
            if (!string.IsNullOrWhiteSpace(l.Description))
            {
                sb.Append("<div class=\"muted\">").Append(E(l.Description)).Append("</div>");
            }

            sb.Append("</td><td class=\"num\">").Append(E(l.Quantity.ToString("0.####", Tr))).Append(' ').Append(E(Units.TryGetValue(l.UnitCode, out var u) ? u : l.UnitCode))
                .Append("</td><td class=\"num\">").Append(M(l.UnitPrice))
                .Append("</td><td class=\"num\">").Append(l.DiscountAmount > 0 ? M(l.DiscountAmount) : "–")
                .Append("</td><td class=\"num\">%").Append(E(l.VatRate.ToString("0.##", Tr))).Append("<div class=\"muted\">").Append(M(l.VatAmount)).Append("</div>")
                .Append("</td><td class=\"num\"><strong>").Append(M(l.LineExtensionAmount)).Append("</strong></td></tr>");
        }

        sb.Append("</tbody></table><div class=\"totals\"><div class=\"muted\">");
        if (invoice.Lines.Count > 0)
        {
            sb.Append(invoice.Lines.Count).Append(" kalem");
        }

        sb.Append("</div><table>");
        var t = invoice.Totals;
        sb.Append("<tr><td>Mal / hizmet toplamı</td><td class=\"num\">").Append(M(t.LineExtensionAmount + t.AllowanceTotalAmount)).Append("</td></tr>");
        if (t.AllowanceTotalAmount > 0)
        {
            sb.Append("<tr><td>Toplam iskonto</td><td class=\"num\">−").Append(M(t.AllowanceTotalAmount)).Append("</td></tr>");
        }

        sb.Append("<tr><td>Vergi hariç tutar</td><td class=\"num\">").Append(M(t.TaxExclusiveAmount)).Append("</td></tr>");
        foreach (var v in t.VatSubtotals)
        {
            sb.Append("<tr><td>").Append(E(v.TaxName)).Append(" (%").Append(E(v.Percent.ToString("0.##", Tr))).Append(")</td><td class=\"num\">").Append(M(v.TaxAmount)).Append("</td></tr>");
        }

        if (t.WithholdingTotal > 0)
        {
            sb.Append("<tr><td>Tevkifat</td><td class=\"num\">−").Append(M(t.WithholdingTotal)).Append("</td></tr>");
        }

        sb.Append("<tr><td>Vergiler dahil toplam</td><td class=\"num\">").Append(M(t.TaxInclusiveAmount)).Append("</td></tr>");
        sb.Append("<tr class=\"grand\"><td>Ödenecek tutar</td><td class=\"num\">").Append(M(t.PayableAmount)).Append("</td></tr></table></div>");
        if (invoice.Notes.Count > 0)
        {
            sb.Append("<div class=\"notes\"><strong>Notlar</strong>");
            foreach (var n in invoice.Notes)
            {
                sb.Append("<div>").Append(E(n)).Append("</div>");
            }

            sb.Append("</div>");
        }

        sb.Append("<div class=\"foot\"><span>").Append(E(s.Title)).Append("</span><span>ETTN ").Append(invoice.Uuid).Append("</span></div></div></body></html>");
        return sb.ToString();
    }

    private static string Base64Url(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        s += (s.Length % 4) switch { 2 => "==", 3 => "=", 0 => string.Empty, _ => throw new FormatException() };
        return Convert.FromBase64String(s);
    }
}
