using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;

namespace GIBFramework.Models.Compliance;

public sealed class RuleParameters
{
    private readonly ReadOnlyDictionary<string, JsonElement> _values;

    public RuleParameters(IDictionary<string, JsonElement> values) =>
        _values = new(new Dictionary<string, JsonElement>(values, StringComparer.OrdinalIgnoreCase));

    public static RuleParameters Empty { get; } = new(new Dictionary<string, JsonElement>());

    public IReadOnlyDictionary<string, JsonElement> Values => _values;

    public bool Has(string name) =>
        _values.TryGetValue(name, out var v) && v.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;

    public decimal GetDecimal(string name) => TryGetDecimal(name) ?? throw Missing(name);

    public decimal? TryGetDecimal(string name)
    {
        if (!Has(name))
        {
            return null;
        }

        var v = _values[name];
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDecimal(),
            JsonValueKind.String => decimal.Parse(v.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture),
            _ => throw new FormatException($"Kural parametresi '{name}' sayı olmalıdır."),
        };
    }

    public int GetInt(string name) =>
        Has(name) && _values[name].ValueKind == JsonValueKind.Number ? _values[name].GetInt32() : throw Missing(name);

    public bool GetBool(string name, bool defaultValue) => Has(name) ? _values[name].GetBoolean() : defaultValue;

    public IReadOnlyList<string> GetStringList(string name) =>
        Has(name) ? [.. _values[name].EnumerateArray().Select(e => e.GetString()!)] : [];

    public TEnum GetEnum<TEnum>(string name, TEnum defaultValue)
        where TEnum : struct, Enum =>
        Has(name) ? Enum.Parse<TEnum>(_values[name].GetString()!, ignoreCase: true) : defaultValue;

    public IReadOnlyList<TEnum> GetEnumList<TEnum>(string name)
        where TEnum : struct, Enum =>
        [.. GetStringList(name).Select(s => Enum.Parse<TEnum>(s, ignoreCase: true))];

    private static KeyNotFoundException Missing(string name) => new($"Kural parametresi eksik: '{name}'.");
}
