using System.Globalization;

namespace GIBFramework.Infrastructure.GibPortal;

public static class GibPortalDraftMapper
{
    public static IReadOnlyList<string> Unsupported(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        var problems = new List<string>();
        if (invoice.DocumentType != EDocumentType.EArsiv)
        {
            problems.Add("GİB e-Arşiv Portal yalnızca e-Arşiv Fatura düzenler (alıcı e-Fatura kullanıcısıysa e-Fatura gerekir).");
        }

        if (invoice.Customer.IsEFaturaRegistered)
        {
            problems.Add("Alıcı e-Fatura kayıtlı kullanıcısı; portal e-Arşiv faturası kabul etmez.");
        }

        if (invoice.Currency != "TRY" && invoice.ExchangeRate is not > 0)
        {
            problems.Add("Dövizli faturada TL kuru zorunludur.");
        }

        if (invoice.TypeCode != InvoiceTypeCode.SATIS)
        {
            problems.Add($"{invoice.TypeCode} tipi fatura portal üzerinden henüz desteklenmiyor (yalnızca SATIS).");
        }

        if (invoice.Lines.Any(l => l.VatRate <= 0 || l.VatExemptionCode is not null || l.WithholdingCode is not null))
        {
            problems.Add("İstisna/tevkifat içeren satırlar portal üzerinden henüz desteklenmiyor.");
        }

        return problems;
    }

    public static Dictionary<string, object?> Map(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        var c = invoice.Customer;
        var isPerson = c.TaxId.Length == 11;
        var gross = invoice.Lines.Sum(l => Round(l.Quantity * l.UnitPrice));
        var t = invoice.Totals;

        return new Dictionary<string, object?>
        {
            ["faturaUuid"] = string.Empty,
            ["belgeNumarasi"] = string.Empty,
            ["faturaTarihi"] = GibPortalClient.PortalDate(invoice.IssueDate),
            ["saat"] = invoice.IssueTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            ["paraBirimi"] = invoice.Currency,
            ["dovzTLkur"] = invoice.Currency == "TRY" ? "0" : Invariant.Number(invoice.ExchangeRate ?? 0m),
            ["faturaTipi"] = invoice.TypeCode.ToString(),
            ["hangiTip"] = GibPortalProtocol.InvoiceKind,
            ["vknTckn"] = c.TaxId,
            ["aliciUnvan"] = isPerson ? string.Empty : c.Title,
            ["aliciAdi"] = isPerson ? c.FirstName ?? FirstName(c.Title) : string.Empty,
            ["aliciSoyadi"] = isPerson ? c.FamilyName ?? LastName(c.Title) : string.Empty,
            ["binaAdi"] = string.Empty,
            ["binaNo"] = c.BuildingNumber ?? string.Empty,
            ["kapiNo"] = string.Empty,
            ["kasabaKoy"] = string.Empty,
            ["vergiDairesi"] = c.TaxOffice ?? string.Empty,
            ["ulke"] = string.IsNullOrWhiteSpace(c.Country) ? "Türkiye" : c.Country,
            ["bulvarcaddesokak"] = c.Street ?? string.Empty,
            ["mahalleSemtIlce"] = string.Join(" / ", new[] { c.Neighborhood, c.District }.Where(s => !string.IsNullOrWhiteSpace(s))),
            ["sehir"] = string.IsNullOrWhiteSpace(c.City) ? " " : c.City,
            ["postaKodu"] = c.PostalCode ?? string.Empty,
            ["tel"] = c.Phone ?? string.Empty,
            ["fax"] = string.Empty,
            ["eposta"] = c.Email ?? string.Empty,
            ["websitesi"] = string.Empty,
            ["iadeTable"] = Array.Empty<object>(),
            ["ozelMatrahTutari"] = "0",
            ["ozelMatrahOrani"] = 0,
            ["ozelMatrahVergiTutari"] = "0",
            ["vergiCesidi"] = " ",
            ["malHizmetTable"] = invoice.Lines.Select(Line).ToArray(),
            ["tip"] = "İskonto",
            ["matrah"] = Amount(t.TaxExclusiveAmount),
            ["malhizmetToplamTutari"] = Amount(gross),
            ["toplamIskonto"] = Amount(t.AllowanceTotalAmount),
            ["hesaplanankdv"] = Amount(t.VatTotal),
            ["vergilerToplami"] = Amount(t.VatTotal),
            ["vergilerDahilToplamTutar"] = Amount(t.TaxInclusiveAmount),
            ["odenecekTutar"] = Amount(t.PayableAmount),
            ["not"] = string.Join(" ", invoice.Notes),
            ["siparisNumarasi"] = invoice.OrderNumber ?? string.Empty,
            ["siparisTarihi"] = string.Empty,
            ["irsaliyeNumarasi"] = invoice.DespatchNumber ?? string.Empty,
            ["irsaliyeTarihi"] = invoice.DespatchDate is { } dd ? GibPortalClient.PortalDate(dd) : string.Empty,
            ["fisNo"] = string.Empty,
            ["fisTarihi"] = string.Empty,
            ["fisSaati"] = " ",
            ["fisTipi"] = " ",
            ["zRaporNo"] = string.Empty,
            ["okcSeriNo"] = string.Empty,
        };
    }

    private static Dictionary<string, object> Line(InvoiceLine l)
    {
        var gross = Round(l.Quantity * l.UnitPrice);
        return new Dictionary<string, object>
        {
            ["malHizmet"] = l.Name,
            ["miktar"] = l.Quantity,
            ["birim"] = l.UnitCode,
            ["birimFiyat"] = Amount(l.UnitPrice),
            ["fiyat"] = Amount(gross),
            ["iskontoArttm"] = "İskonto",
            ["iskontoOrani"] = gross == 0 ? 0 : decimal.Round(l.DiscountAmount / gross * 100m, 2, MidpointRounding.AwayFromZero),
            ["iskontoTutari"] = Amount(l.DiscountAmount),
            ["iskontoNedeni"] = string.Empty,
            ["malHizmetTutari"] = Amount(l.LineExtensionAmount),
            ["kdvOrani"] = Invariant.Number(l.VatRate),
            ["kdvTutari"] = Amount(l.VatAmount),
            ["vergiOrani"] = 0,
            ["vergininKdvTutari"] = "0",
            ["ozelMatrahTutari"] = "0.00",
            ["hesaplananotvtevkifatakatkisi"] = "0.00",
        };
    }

    private static string Amount(decimal value) => Invariant.Amount(value);

    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string FirstName(string full)
    {
        var parts = full.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length <= 1 ? full : string.Join(' ', parts[..^1]);
    }

    private static string LastName(string full)
    {
        var parts = full.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? string.Empty : parts[^1];
    }
}
