using System.Globalization;

namespace GIBFramework.Helpers;

internal static class Fmt
{
    public static string Date(DateOnly date) => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    public static string Try(decimal amount) => amount.ToString("N2", TurkishText.Culture) + " TL";

    public static string Basis(AmountBasis basis) => basis == AmountBasis.VatIncluded ? "KDV dahil" : "KDV hariç";
}

public static class Invariant
{
    public static string Amount(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    public static string Number(decimal value) => value.ToString("0.########", CultureInfo.InvariantCulture);

    public static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Time(TimeOnly value) => value.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
}
