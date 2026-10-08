using System.Text;
using System.Text.Json;
using QRCoder;

namespace GIBFramework.Services.Qr;

public sealed class QrCodeService
{
    public const string StandardVersion = "GIB-KAREKOD-1.2";

    public string BuildPayload(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        var number = invoice.DocumentNumber ?? throw new DomainException("QR_NO_NUMBER", "Karekod için belge numarası atanmış olmalıdır.");

        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartObject();
            w.WriteString("vkntckn", invoice.Supplier.TaxId);
            w.WriteString("avkntckn", invoice.Customer.TaxId);
            w.WriteString("senaryo", invoice.Profile.ToString());
            w.WriteString("tip", invoice.TypeCode.ToString());
            w.WriteString("tarih", Invariant.Date(invoice.IssueDate));
            w.WriteString("no", number);
            w.WriteString("ettn", invoice.Uuid.ToString("D"));
            w.WriteString("parabirimi", invoice.Currency);
            w.WriteString("malhizmettoplam", Invariant.Amount(invoice.Totals.LineExtensionAmount));
            foreach (var s in invoice.Totals.VatSubtotals.Where(s => s.Percent > 0))
            {
                var rate = Invariant.Number(s.Percent);
                w.WriteString($"kdvmatrah({rate})", Invariant.Amount(s.TaxableAmount));
                w.WriteString($"hesaplanankdv({rate})", Invariant.Amount(s.TaxAmount));
            }

            w.WriteString("vergidahil", Invariant.Amount(invoice.Totals.TaxInclusiveAmount));
            w.WriteString("odenecek", Invariant.Amount(invoice.Totals.PayableAmount));
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public IReadOnlyList<bool[]> Modules(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M, forceUtf8: true);
        return [.. data.ModuleMatrix.Select(row => Enumerable.Range(0, row.Length).Select(i => row[i]).ToArray())];
    }

    public byte[] RenderPng(string payload, int pixelsPerModule = 6)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M, forceUtf8: true);
        return new PngByteQRCode(data).GetGraphic(pixelsPerModule);
    }
}
