namespace GIBFramework.Models.TaxOffices;

public enum TaxOfficeType
{
    Defterdarlik,
    VergiDairesiBaskanligi,
    VergiDairesiMudurlugu,
    Malmudurlugu,
    Other,
}

public sealed record TaxOfficeRecord(
    string GibCode,
    string Name,
    TaxOfficeType OfficeType,
    string ProvinceCode,
    string ProvinceName,
    string? DistrictName,
    string? ParentGibCode,
    bool IsBranch);
