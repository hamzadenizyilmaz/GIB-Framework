using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace GIBFramework.Services.Documents.Pdf;

public sealed record PdfImage(int Width, int Height, string ColorSpace, string Filter, byte[] Data, byte[]? AlphaFlate = null, string? DecodeParms = null);

public sealed class PdfPage
{
    public const double Width = 595.28;
    public const double Height = 841.89;

    private readonly StringBuilder _content = new();

    internal PdfPage()
    {
    }

    internal HashSet<string> Images { get; } = [];

    internal string Content => _content.ToString();

    public void Text(double x, double top, string text, double size, bool bold = false, double gray = 0.12, (double R, double G, double B)? color = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var (r, g, b) = color ?? (gray, gray, gray);
        _content.Append(CultureInfo.InvariantCulture, $"BT /{(bold ? "F2" : "F1")} {N(size)} Tf {N(r)} {N(g)} {N(b)} rg {N(x)} {N(Height - top - size)} Td ")
            .Append(PdfText.Literal(text)).Append(" Tj ET\n");
    }

    public void TextRight(double right, double top, string text, double size, bool bold = false, double gray = 0.12) =>
        Text(right - PdfText.Measure(text, size, bold), top, text, size, bold, gray);

    public void Line(double x1, double top1, double x2, double top2, double width = 0.6, double gray = 0.85)
    {
        _content.Append(CultureInfo.InvariantCulture, $"{N(gray)} G {N(width)} w {N(x1)} {N(Height - top1)} m {N(x2)} {N(Height - top2)} l S\n");
    }

    public void Rect(double x, double top, double w, double h, (double R, double G, double B) fill)
    {
        _content.Append(CultureInfo.InvariantCulture, $"{N(fill.R)} {N(fill.G)} {N(fill.B)} rg {N(x)} {N(Height - top - h)} {N(w)} {N(h)} re f\n");
    }

    public void StrokeRect(double x, double top, double w, double h, double width = 0.6, double gray = 0.85)
    {
        _content.Append(CultureInfo.InvariantCulture, $"{N(gray)} G {N(width)} w {N(x)} {N(Height - top - h)} {N(w)} {N(h)} re S\n");
    }

    public void Image(string name, double x, double top, double w, double h)
    {
        Images.Add(name);
        _content.Append(CultureInfo.InvariantCulture, $"q {N(w)} 0 0 {N(h)} {N(x)} {N(Height - top - h)} cm /{name} Do Q\n");
    }

    public void Qr(IReadOnlyList<bool[]> modules, double x, double top, double size)
    {
        ArgumentNullException.ThrowIfNull(modules);
        if (modules.Count == 0)
        {
            return;
        }

        var cell = size / modules.Count;
        _content.Append("0 0 0 rg\n");
        for (var row = 0; row < modules.Count; row++)
        {
            for (var col = 0; col < modules[row].Length; col++)
            {
                if (modules[row][col])
                {
                    _content.Append(CultureInfo.InvariantCulture, $"{N(x + (col * cell))} {N(Height - top - ((row + 1) * cell))} {N(cell + 0.02)} {N(cell + 0.02)} re ");
                }
            }
        }

        _content.Append("f\n");
    }

    private static string N(double v) => Math.Round(v, 3).ToString("0.###", CultureInfo.InvariantCulture);
}

public sealed class PdfBuilder
{
    private readonly List<PdfPage> _pages = [];
    private readonly Dictionary<string, PdfImage> _images = new(StringComparer.Ordinal);

    public string Title { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public IReadOnlyList<PdfPage> Pages => _pages;

    public PdfPage AddPage()
    {
        var page = new PdfPage();
        _pages.Add(page);
        return page;
    }

    public string AddImage(PdfImage image)
    {
        var name = "Im" + (_images.Count + 1).ToString(CultureInfo.InvariantCulture);
        _images[name] = image;
        return name;
    }

    public byte[] Build()
    {
        var objects = new List<byte[]>();
        int Reserve()
        {
            objects.Add([]);
            return objects.Count;
        }

        void Set(int id, string body) => objects[id - 1] = Encoding.ASCII.GetBytes(body);

        void SetStream(int id, string dict, byte[] data)
        {
            using var ms = new MemoryStream();
            var head = Encoding.ASCII.GetBytes($"<< {dict} /Length {data.Length.ToString(CultureInfo.InvariantCulture)} >>\nstream\n");
            ms.Write(head);
            ms.Write(data);
            ms.Write("\nendstream"u8);
            objects[id - 1] = ms.ToArray();
        }

        var catalog = Reserve();
        var pagesId = Reserve();
        var encoding = Reserve();
        var font1 = Reserve();
        var font2 = Reserve();
        var info = Reserve();
        Set(encoding, "<< /Type /Encoding /BaseEncoding /WinAnsiEncoding /Differences [208 /Gbreve 221 /Idotaccent /Scedilla 240 /gbreve 253 /dotlessi /scedilla] >>");
        Set(font1, $"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding {encoding} 0 R >>");
        Set(font2, $"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding {encoding} 0 R >>");
        Set(info, $"<< /Title {PdfText.Utf16(Title)} /Author {PdfText.Utf16(Author)} /Producer {PdfText.Utf16("GIB Framework")} /CreationDate (D:{DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}Z) >>");

        var imageIds = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (name, image) in _images)
        {
            var id = Reserve();
            var smask = string.Empty;
            if (image.AlphaFlate is not null)
            {
                var maskId = Reserve();
                SetStream(maskId, $"/Type /XObject /Subtype /Image /Width {image.Width} /Height {image.Height} /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /FlateDecode", image.AlphaFlate);
                smask = $" /SMask {maskId} 0 R";
            }

            var parms = image.DecodeParms is null ? string.Empty : $" /DecodeParms {image.DecodeParms}";
            SetStream(id, $"/Type /XObject /Subtype /Image /Width {image.Width} /Height {image.Height} /ColorSpace /{image.ColorSpace} /BitsPerComponent 8 /Filter /{image.Filter}{parms}{smask}", image.Data);
            imageIds[name] = id;
        }

        var kids = new List<int>();
        foreach (var page in _pages)
        {
            var pageId = Reserve();
            var contentId = Reserve();
            SetStream(contentId, "/Filter /FlateDecode", Deflate(Encoding.Latin1.GetBytes(page.Content)));
            var xobjects = page.Images.Count == 0 ? string.Empty
                : " /XObject << " + string.Join(" ", page.Images.Select(n => $"/{n} {imageIds[n]} 0 R")) + " >>";
            Set(pageId, $"<< /Type /Page /Parent {pagesId} 0 R /MediaBox [0 0 595.28 841.89] /Resources << /Font << /F1 {font1} 0 R /F2 {font2} 0 R >>{xobjects} >> /Contents {contentId} 0 R >>");
            kids.Add(pageId);
        }

        Set(pagesId, $"<< /Type /Pages /Kids [{string.Join(" ", kids.Select(k => $"{k} 0 R"))}] /Count {kids.Count} >>");
        Set(catalog, $"<< /Type /Catalog /Pages {pagesId} 0 R >>");

        using var output = new MemoryStream();
        output.Write(Encoding.Latin1.GetBytes("%PDF-1.4\n%âãÏÓ\n"));
        var offsets = new List<long>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(output.Position);
            output.Write(Encoding.ASCII.GetBytes($"{i + 1} 0 obj\n"));
            output.Write(objects[i]);
            output.Write("\nendobj\n"u8);
        }

        var xref = output.Position;
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets)
        {
            sb.Append(o.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        sb.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Count + 1} /Root {catalog} 0 R /Info {info} 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        output.Write(Encoding.ASCII.GetBytes(sb.ToString()));
        return output.ToArray();
    }

    public static byte[] Deflate(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        {
            z.Write(data);
        }

        return ms.ToArray();
    }
}

public static class PdfText
{
    private static readonly int[] Regular =
    [
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556,
        1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556,
        333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556,
        556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584,
    ];

    private static readonly int[] Bold =
    [
        278, 333, 474, 556, 556, 889, 722, 238, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 333, 333, 584, 584, 584, 611,
        975, 722, 722, 722, 722, 667, 611, 778, 722, 278, 556, 722, 611, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 333, 278, 333, 584, 556,
        333, 556, 611, 556, 611, 556, 333, 611, 611, 278, 278, 556, 278, 889, 611, 611,
        611, 611, 389, 556, 333, 611, 556, 778, 556, 556, 500, 389, 280, 389, 584,
    ];

    private static readonly Dictionary<char, byte> Special = new()
    {
        ['Ğ'] = 0xD0, ['ğ'] = 0xF0, ['İ'] = 0xDD, ['Ş'] = 0xDE, ['ı'] = 0xFD, ['ş'] = 0xFE,
        ['€'] = 0x80, ['…'] = 0x85, ['•'] = 0x95, ['–'] = 0x96, ['—'] = 0x97, ['‘'] = 0x91, ['’'] = 0x92, ['“'] = 0x93, ['”'] = 0x94,
    };

    private static readonly HashSet<byte> Replaced = [0xD0, 0xF0, 0xDD, 0xDE, 0xFD, 0xFE];

    public static string Prepare(string text) =>
        (text ?? string.Empty).Replace("\u20BA", "TL", StringComparison.Ordinal).Replace('\u2212', '-').Replace('\u00A0', ' ')
            .Replace('\t', ' ').Replace("\r", string.Empty, StringComparison.Ordinal).Replace('\n', ' ');

    public static byte[] Encode(string text)
    {
        var s = Prepare(text);
        var bytes = new byte[s.Length];
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            bytes[i] = Special.TryGetValue(c, out var b) ? b
                : c < 0x80 ? (byte)c
                : c is >= ' ' and <= 'ÿ' && !Replaced.Contains((byte)c) ? (byte)c
                : (byte)'?';
        }

        return bytes;
    }

    public static string Literal(string text)
    {
        var sb = new StringBuilder("(");
        foreach (var b in Encode(text))
        {
            switch (b)
            {
                case (byte)'(':
                case (byte)')':
                case (byte)'\\':
                    sb.Append('\\').Append((char)b);
                    break;
                default:
                    if (b < 0x20 || b > 0x7E)
                    {
                        sb.Append('\\').Append(Convert.ToString(b, 8).PadLeft(3, '0'));
                    }
                    else
                    {
                        sb.Append((char)b);
                    }

                    break;
            }
        }

        return sb.Append(')').ToString();
    }

    public static string Utf16(string text)
    {
        var bytes = Encoding.BigEndianUnicode.GetBytes(text ?? string.Empty);
        return "<FEFF" + Convert.ToHexString(bytes) + ">";
    }

    public static double Measure(string text, double size, bool bold)
    {
        var table = bold ? Bold : Regular;
        double units = 0;
        foreach (var c in Prepare(text))
        {
            units += c switch
            {
                >= ' ' and <= '~' => table[c - 32],
                'İ' or 'ı' => 278,
                'Ç' or 'Ü' => 722,
                'Ğ' or 'Ö' => 778,
                'Ş' => 667,
                'ç' or 'ş' => bold ? 556 : 500,
                'ğ' or 'ö' or 'ü' => bold ? 611 : 556,
                _ => char.IsUpper(c) ? 722 : 556,
            };
        }

        return units * size / 1000.0;
    }

    public static IReadOnlyList<string> Wrap(string text, double width, double size, bool bold = false)
    {
        var lines = new List<string>();
        foreach (var paragraph in (text ?? string.Empty).Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
        {
            var current = new StringBuilder();
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = current.Length == 0 ? word : current + " " + word;
                if (Measure(candidate, size, bold) <= width)
                {
                    current.Clear().Append(candidate);
                    continue;
                }

                if (current.Length > 0)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                }

                var piece = word;
                while (Measure(piece, size, bold) > width && piece.Length > 1)
                {
                    var cut = piece.Length - 1;
                    while (cut > 1 && Measure(piece[..cut], size, bold) > width)
                    {
                        cut--;
                    }

                    lines.Add(piece[..cut]);
                    piece = piece[cut..];
                }

                current.Append(piece);
            }

            lines.Add(current.ToString());
        }

        return lines.Count == 0 ? [string.Empty] : lines;
    }
}
