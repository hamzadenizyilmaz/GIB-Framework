using System.Diagnostics.CodeAnalysis;

namespace GIBFramework.Models.Taxpayers;

public enum TaxIdentifierType
{
    Vkn,

    Tckn,
}

public readonly record struct TaxIdentifier
{
    private TaxIdentifier(string value, TaxIdentifierType type)
    {
        Value = value;
        Type = type;
    }

    public const string AnonymousConsumer = "11111111111";

    public string Value { get; }

    public TaxIdentifierType Type { get; }

    public static TaxIdentifier Parse(string? input) =>
        TryParse(input, out var id, out var error) ? id : throw new DomainException(error!.Code, error.Message);

    public static bool TryParse(string? input, out TaxIdentifier identifier, [NotNullWhen(false)] out TaxIdentifierError? error)
    {
        identifier = default;
        var value = input?.Trim() ?? string.Empty;

        if (value.Length == 0)
        {
            error = new("TAXID_EMPTY", "VKN/TCKN boş olamaz.");
            return false;
        }

        if (!value.All(char.IsAsciiDigit))
        {
            error = new("TAXID_NOT_NUMERIC", "VKN/TCKN yalnızca rakamlardan oluşmalıdır.");
            return false;
        }

        switch (value.Length)
        {
            case 10 when IsValidVkn(value):
                identifier = new(value, TaxIdentifierType.Vkn);
                error = null;
                return true;
            case 10:
                error = new("VKN_CHECKSUM", "VKN kontrol hanesi geçersiz.");
                return false;
            case 11 when value == AnonymousConsumer:
                identifier = new(value, TaxIdentifierType.Tckn);
                error = null;
                return true;
            case 11 when value[0] == '0':
                error = new("TCKN_LEADING_ZERO", "TCKN 0 ile başlayamaz.");
                return false;
            case 11 when IsValidTckn(value):
                identifier = new(value, TaxIdentifierType.Tckn);
                error = null;
                return true;
            case 11:
                error = new("TCKN_CHECKSUM", "TCKN kontrol haneleri geçersiz.");
                return false;
            default:
                error = new("TAXID_LENGTH", "VKN 10, TCKN 11 haneli olmalıdır.");
                return false;
        }
    }

    public string Masked() => string.Concat(Value.AsSpan(0, 3), new string('*', Value.Length - 5), Value.AsSpan(Value.Length - 2));

    public override string ToString() => Value;

    internal static bool IsValidVkn(string vkn)
    {
        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            var tmp = (vkn[i] - '0' + 9 - i) % 10;
            if (tmp == 0)
            {
                continue;
            }

            var weighted = tmp * (1 << (9 - i)) % 9;
            sum += weighted == 0 ? 9 : weighted;
        }

        return (10 - (sum % 10)) % 10 == vkn[9] - '0';
    }

    internal static bool IsValidTckn(string tckn)
    {
        var d = tckn.Select(c => c - '0').ToArray();
        var odd = d[0] + d[2] + d[4] + d[6] + d[8];
        var even = d[1] + d[3] + d[5] + d[7];
        var tenth = (((odd * 7) - even) % 10 + 10) % 10;
        if (tenth != d[9])
        {
            return false;
        }

        return d.Take(10).Sum() % 10 == d[10];
    }
}

public sealed record TaxIdentifierError(string Code, string Message);
