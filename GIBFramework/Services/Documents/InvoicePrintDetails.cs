namespace GIBFramework.Services.Documents;

internal static class InvoicePrintDetails
{
    internal static readonly string[] ReturnColumns = ["Adı", "Soyadı", "Malın cinsi", "Miktar", "Birim fiyat", "Tutar", "Adres", "İmza"];

    public static bool HasReturnSection(Invoice invoice) =>
        invoice.DocumentType == EDocumentType.EArsiv && invoice.TypeCode == InvoiceTypeCode.SATIS
        && invoice.Notes.Any(note => note.StartsWith("Satış yapılan web adresi:", StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<string> BankColumns { get; } = ["Banka adı", "Şube adı", "IBAN", "Para birimi"];

    public static IReadOnlyList<string[]> BankRows(Invoice invoice) =>
        [.. invoice.Notes.Select(ParseBankNote).OfType<string[]>()];

    public static bool IsBankNote(string note) => ParseBankNote(note) is not null;

    private static string[]? ParseBankNote(string note)
    {
        const string prefix = "Banka hesabı: ";
        if (!note.StartsWith(prefix, StringComparison.Ordinal)) return null;
        var fields = note[prefix.Length..].Split(" | ", 4, StringSplitOptions.TrimEntries);
        return fields.Length == BankColumns.Count && fields[2].Length == 26 && fields[2].StartsWith("TR", StringComparison.OrdinalIgnoreCase)
            && fields[2][2..].All(char.IsAsciiDigit) ? fields : null;
    }

    public static string? TryAmountInWords(Invoice invoice)
    {
        if (invoice.Currency != "TRY" || invoice.Totals.PayableAmount < 0 || invoice.Totals.PayableAmount >= 1_000_000_000_000_000m)
            return null;
        var value = decimal.Round(invoice.Totals.PayableAmount, 2, MidpointRounding.AwayFromZero);
        var lira = (long)value;
        var kurus = (long)((value - lira) * 100);
        return $"{Words(lira)} Türk lirası" + (kurus == 0 ? string.Empty : $" {Words(kurus)} kuruş");
    }

    private static string Words(long number)
    {
        if (number == 0) return "sıfır";
        string[] ones = ["", "bir", "iki", "üç", "dört", "beş", "altı", "yedi", "sekiz", "dokuz"];
        string[] tens = ["", "on", "yirmi", "otuz", "kırk", "elli", "altmış", "yetmiş", "seksen", "doksan"];
        string[] scales = ["", "bin", "milyon", "milyar", "trilyon", "katrilyon"];
        var parts = new List<string>();
        for (var i = 0; number > 0; i++, number /= 1000)
        {
            var group = (int)(number % 1000);
            if (group == 0) continue;
            var hundreds = group / 100;
            var part = i == 1 && group == 1 ? "bin" : string.Join(' ',
                new[] { hundreds > 1 ? ones[hundreds] : "", hundreds > 0 ? "yüz" : "", tens[group / 10 % 10], ones[group % 10], scales[i] }
                    .Where(word => word.Length > 0));
            parts.Add(part);
        }
        parts.Reverse();
        return string.Join(' ', parts);
    }
}
