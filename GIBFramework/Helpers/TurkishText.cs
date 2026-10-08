using System.Globalization;
using System.Text;

namespace GIBFramework.Helpers;

public static class TurkishText
{
    public static CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("tr-TR");

    public static string NormalizeUpper(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var collapsed = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.ToUpper(Culture);
    }

    public static string ToSearchKey(string value)
    {
        var upper = NormalizeUpper(value);
        var sb = new StringBuilder(upper.Length);
        foreach (var c in upper)
        {
            sb.Append(c switch
            {
                'Ç' => 'C',
                'Ğ' => 'G',
                'İ' => 'I',
                'Ö' => 'O',
                'Ş' => 'S',
                'Ü' => 'U',
                'Â' => 'A',
                'Î' => 'I',
                'Û' => 'U',
                _ => c,
            });
        }

        return sb.ToString();
    }
}
