using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace GIBFramework.Models.Documents;

public readonly record struct DocumentNumber
{
    public const int MaxSequence = 999_999_999;

    private DocumentNumber(string prefix, int year, long sequence)
    {
        Prefix = prefix;
        Year = year;
        Sequence = sequence;
    }

    public string Prefix { get; }

    public int Year { get; }

    public long Sequence { get; }

    public string Value => string.Create(CultureInfo.InvariantCulture, $"{Prefix}{Year:D4}{Sequence:D9}");

    public static DocumentNumber Create(string prefix, int year, long sequence)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        if (!IsValidPrefix(prefix))
        {
            throw new DomainException("DOCNO_PREFIX", "Birim kodu 3 karakter olmalı ve yalnızca A-Z / 0-9 içermelidir.");
        }

        if (year is < 2000 or > 9999)
        {
            throw new DomainException("DOCNO_YEAR", "Belge yılı geçersiz.");
        }

        if (sequence is < 1 or > MaxSequence)
        {
            throw new DomainException("DOCNO_SEQUENCE", $"Sıra numarası 1 ile {MaxSequence} arasında olmalıdır.");
        }

        return new DocumentNumber(prefix, year, sequence);
    }

    public static bool TryParse(string? value, [NotNullWhen(true)] out DocumentNumber? number)
    {
        number = null;
        if (value is not { Length: 16 } || !IsValidPrefix(value[..3]) || !value.AsSpan(3).ContainsOnlyAsciiDigits())
        {
            return false;
        }

        var year = int.Parse(value.AsSpan(3, 4), CultureInfo.InvariantCulture);
        var sequence = long.Parse(value.AsSpan(7, 9), CultureInfo.InvariantCulture);
        if (year < 2000 || sequence < 1)
        {
            return false;
        }

        number = new DocumentNumber(value[..3], year, sequence);
        return true;
    }

    public static DocumentNumber Parse(string value) =>
        TryParse(value, out var number) ? number.Value : throw new DomainException("DOCNO_FORMAT", $"Geçersiz belge numarası: '{value}'.");

    public override string ToString() => Value;

    private static bool IsValidPrefix(string prefix) =>
        prefix.Length == 3 && prefix.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c));
}

internal static class SpanExtensions
{
    public static bool ContainsOnlyAsciiDigits(this ReadOnlySpan<char> span)
    {
        foreach (var c in span)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return true;
    }
}
