using GIBFramework.Models.TaxOffices;

namespace GIBFramework.Services.TaxOffices;

public static class TaxOfficeCsvParser
{
    public const string ParserVersion = "csv-1.0";

    private static readonly string[] ExpectedHeader =
        ["gibCode", "name", "officeType", "provinceCode", "provinceName", "districtName", "parentGibCode", "isBranch"];

    public static IReadOnlyList<TaxOfficeRecord> Parse(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var header = reader.ReadLine()?.TrimStart('﻿').Split(';');
        if (header is null || !header.SequenceEqual(ExpectedHeader, StringComparer.OrdinalIgnoreCase))
        {
            throw new FormatException("CSV başlığı beklenen formatta değil: " + string.Join(';', ExpectedHeader));
        }

        var records = new List<TaxOfficeRecord>();
        var lineNo = 1;
        while (reader.ReadLine() is { } line)
        {
            lineNo++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var f = line.Split(';');
            if (f.Length != ExpectedHeader.Length)
            {
                throw new FormatException($"{lineNo}. satır {ExpectedHeader.Length} alan içermeli.");
            }

            records.Add(new TaxOfficeRecord(
                GibCode: f[0].Trim(),
                Name: f[1].Trim(),
                OfficeType: Enum.TryParse<TaxOfficeType>(f[2].Trim(), ignoreCase: true, out var type) ? type : TaxOfficeType.Other,
                ProvinceCode: f[3].Trim(),
                ProvinceName: f[4].Trim(),
                DistrictName: NullIfEmpty(f[5]),
                ParentGibCode: NullIfEmpty(f[6]),
                IsBranch: bool.TryParse(f[7].Trim(), out var branch) && branch));
        }

        return records;
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
