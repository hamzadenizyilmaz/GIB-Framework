using System.Globalization;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using GIBFramework.Models.TaxOffices;

namespace GIBFramework.Services.TaxOffices;

public sealed record PdfParseResult(IReadOnlyList<TaxOfficeRecord> Records, IReadOnlyList<string> Warnings, int LineCount);

public static partial class TaxOfficePdfParser
{
    public const string ParserVersion = "gib-vd-pdf-2026.06";

    private const double RowTolerance = 3.5;

    public static PdfParseResult Parse(Stream pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        var lines = new List<string>();
        using (var document = PdfDocument.Open(pdf))
        {
            foreach (var page in document.GetPages())
            {
                var rows = new List<(double Y, List<(double X, string Text)> Words)>();
                foreach (var word in page.GetWords().OrderByDescending(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left))
                {
                    var y = word.BoundingBox.Bottom;
                    if (rows.Count > 0 && Math.Abs(rows[^1].Y - y) <= RowTolerance)
                    {
                        rows[^1].Words.Add((word.BoundingBox.Left, word.Text));
                    }
                    else
                    {
                        rows.Add((y, [(word.BoundingBox.Left, word.Text)]));
                    }
                }

                lines.AddRange(rows.Select(r => string.Join(' ', r.Words.OrderBy(w => w.X).Select(w => w.Text))));
            }
        }

        return ParseLines(lines);
    }

    public static PdfParseResult ParseLines(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var records = new Dictionary<string, TaxOfficeRecord>(StringComparer.Ordinal);
        var warnings = new List<string> { "PDF ayrıştırma sonucu onaydan önce resmî liste ile karşılaştırılmalıdır." };
        var branchRows = 0;

        foreach (var raw in lines)
        {
            var line = string.Join(' ', raw.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            var m = Row().Match(line);
            if (!m.Success)
            {
                if (line.EndsWith("Şubesi", StringComparison.Ordinal))
                {
                    branchRows++;
                }

                continue;
            }

            var code = m.Groups["code"].Value;
            var name = m.Groups["name"].Value.Replace("(*)", string.Empty, StringComparison.Ordinal).Trim();
            var provinceCode = m.Groups["pc"].Success ? m.Groups["pc"].Value : code[..2];
            if (!ProvinceCatalog.Names.TryGetValue(provinceCode, out var provinceName))
            {
                warnings.Add($"{code} {name}: il kodu ({provinceCode}) tanınmadı.");
                provinceName = string.Empty;
            }

            if (m.Groups["pc"].Success && code[..2] != provinceCode)
            {
                warnings.Add($"{code} {name}: kodun ilk iki hanesi il koduyla ({provinceCode}) uyuşmuyor.");
            }

            var district = District(m.Groups["district"].Value);
            var upper = TurkishText.NormalizeUpper(name);
            var record = new TaxOfficeRecord(code, name, Classify(upper), provinceCode, provinceName, district, null, upper.Contains("MALMÜDÜRLÜĞÜ", StringComparison.Ordinal));
            if (!records.TryAdd(code, record))
            {
                warnings.Add($"{code}: listede birden fazla kez geçiyor; ilk kayıt kullanıldı.");
            }
        }

        if (branchRows > 0)
        {
            warnings.Add($"{branchRows} kodsuz şube satırı atlandı (vergi dairesi kodu olmayan birimler).");
        }

        return new PdfParseResult([.. records.Values.OrderBy(r => r.GibCode, StringComparer.Ordinal)], warnings, lines.Count);
    }

    private static string? District(string text)
    {
        var t = text.Replace("(**)", " ", StringComparison.Ordinal).Trim();
        var parts = t.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (parts.Count > 1 && parts[0] == "Merkez")
        {
            parts.RemoveAt(0);
        }

        var result = string.Join(' ', parts);
        return result.Length == 0 || result == "Merkez" ? null : result;
    }

    private static TaxOfficeType Classify(string upperName) =>
        upperName.Contains("MALMÜDÜRLÜĞÜ", StringComparison.Ordinal) ? TaxOfficeType.Malmudurlugu
        : upperName.Contains("DEFTERDARLIĞI", StringComparison.Ordinal) ? TaxOfficeType.Defterdarlik
        : upperName.Contains("BAŞKANLIĞI", StringComparison.Ordinal) ? TaxOfficeType.VergiDairesiBaskanligi
        : TaxOfficeType.VergiDairesiMudurlugu;

    [GeneratedRegex(@"^(?:(?<pc>\d{2})\s+\p{Lu}[\p{Lu}.]*\s+)?(?<district>(?:(?!\d{5}\s)\S+\s+)*?)(?<code>\d{5})\s+(?<name>(?:\d+\.?\s+)?\p{L}.+)$")]
    private static partial Regex Row();
}

public static class ProvinceCatalog
{
    public static IReadOnlyDictionary<string, string> Names { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["01"] = "Adana", ["02"] = "Adıyaman", ["03"] = "Afyonkarahisar", ["04"] = "Ağrı", ["05"] = "Amasya", ["06"] = "Ankara",
        ["07"] = "Antalya", ["08"] = "Artvin", ["09"] = "Aydın", ["10"] = "Balıkesir", ["11"] = "Bilecik", ["12"] = "Bingöl",
        ["13"] = "Bitlis", ["14"] = "Bolu", ["15"] = "Burdur", ["16"] = "Bursa", ["17"] = "Çanakkale", ["18"] = "Çankırı",
        ["19"] = "Çorum", ["20"] = "Denizli", ["21"] = "Diyarbakır", ["22"] = "Edirne", ["23"] = "Elazığ", ["24"] = "Erzincan",
        ["25"] = "Erzurum", ["26"] = "Eskişehir", ["27"] = "Gaziantep", ["28"] = "Giresun", ["29"] = "Gümüşhane", ["30"] = "Hakkari",
        ["31"] = "Hatay", ["32"] = "Isparta", ["33"] = "Mersin", ["34"] = "İstanbul", ["35"] = "İzmir", ["36"] = "Kars",
        ["37"] = "Kastamonu", ["38"] = "Kayseri", ["39"] = "Kırklareli", ["40"] = "Kırşehir", ["41"] = "Kocaeli", ["42"] = "Konya",
        ["43"] = "Kütahya", ["44"] = "Malatya", ["45"] = "Manisa", ["46"] = "Kahramanmaraş", ["47"] = "Mardin", ["48"] = "Muğla",
        ["49"] = "Muş", ["50"] = "Nevşehir", ["51"] = "Niğde", ["52"] = "Ordu", ["53"] = "Rize", ["54"] = "Sakarya",
        ["55"] = "Samsun", ["56"] = "Siirt", ["57"] = "Sinop", ["58"] = "Sivas", ["59"] = "Tekirdağ", ["60"] = "Tokat",
        ["61"] = "Trabzon", ["62"] = "Tunceli", ["63"] = "Şanlıurfa", ["64"] = "Uşak", ["65"] = "Van", ["66"] = "Yozgat",
        ["67"] = "Zonguldak", ["68"] = "Aksaray", ["69"] = "Bayburt", ["70"] = "Karaman", ["71"] = "Kırıkkale", ["72"] = "Batman",
        ["73"] = "Şırnak", ["74"] = "Bartın", ["75"] = "Ardahan", ["76"] = "Iğdır", ["77"] = "Yalova", ["78"] = "Karabük",
        ["79"] = "Kilis", ["80"] = "Osmaniye", ["81"] = "Düzce",
    };
}
