using System.ComponentModel.DataAnnotations;
using GIBFramework.Models.Catalog;

namespace GIBFramework.DTOs;

public sealed record CustomerRequest
{
    [Required, RegularExpression("^[0-9]{10,11}$", ErrorMessage = "VKN 10, TCKN 11 haneli olmalıdır.")]
    public string TaxId { get; init; } = string.Empty;

    [Required, StringLength(250)]
    public string Title { get; init; } = string.Empty;

    [StringLength(100)]
    public string? FirstName { get; init; }

    [StringLength(100)]
    public string? FamilyName { get; init; }

    public BookkeepingRegime Regime { get; init; } = BookkeepingRegime.NotATaxpayer;

    [StringLength(100)]
    public string? TaxOffice { get; init; }

    [StringLength(100)]
    public string? ProvinceName { get; init; }

    [StringLength(100)]
    public string? DistrictName { get; init; }

    [StringLength(150)]
    public string? NeighborhoodName { get; init; }

    [StringLength(250)]
    public string? Street { get; init; }

    [StringLength(20)]
    public string? BuildingNumber { get; init; }

    [StringLength(10)]
    public string? PostalCode { get; init; }

    [StringLength(100)]
    public string Country { get; init; } = "Türkiye";

    [EmailAddress, StringLength(200)]
    public string? Email { get; init; }

    [StringLength(30)]
    public string? Phone { get; init; }

    public bool IsEFaturaRegistered { get; init; }

    [StringLength(200)]
    public string? EFaturaAlias { get; init; }

    [StringLength(1000)]
    public string? Notes { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed record ProductRequest
{
    [StringLength(50)]
    public string? Code { get; init; }

    [Required, StringLength(250)]
    public string Name { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; init; }

    [Required, StringLength(10)]
    public string UnitCode { get; init; } = "C62";

    [Range(typeof(decimal), "0", "999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal UnitPrice { get; init; }

    [Required, StringLength(3, MinimumLength = 3)]
    public string Currency { get; init; } = "TRY";

    [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal VatRate { get; init; } = 20;

    [StringLength(10)]
    public string? VatExemptionCode { get; init; }

    [StringLength(10)]
    public string? WithholdingCode { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed record TenantSettingsRequest
{
    [Required]
    public InvoiceDefaults Defaults { get; init; } = new();

    public List<NoteTemplate> NoteTemplates { get; init; } = [];

    public List<BankAccount> BankAccounts { get; init; } = [];
}

public sealed record UpdateCompanyRequest
{
    [Required, StringLength(250)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(250)]
    public string Title { get; init; } = string.Empty;

    public BookkeepingRegime Regime { get; init; } = BookkeepingRegime.Bilanco;

    [Required, StringLength(100)]
    public string TaxOffice { get; init; } = string.Empty;

    [StringLength(150)]
    public string? Neighborhood { get; init; }

    [StringLength(250)]
    public string? Street { get; init; }

    [StringLength(20)]
    public string? BuildingNumber { get; init; }

    [StringLength(100)]
    public string? District { get; init; }

    [Required, StringLength(100)]
    public string City { get; init; } = string.Empty;

    [StringLength(10)]
    public string? PostalCode { get; init; }

    [EmailAddress, StringLength(200)]
    public string? Email { get; init; }

    [StringLength(30)]
    public string? Phone { get; init; }

    public bool IsEFaturaRegistered { get; init; }

    public bool IsEArchiveRegistered { get; init; }

    [Required, RegularExpression("^[A-Z0-9]{3}$")]
    public string EFaturaPrefix { get; init; } = "EFT";

    [Required, RegularExpression("^[A-Z0-9]{3}$")]
    public string EArsivPrefix { get; init; } = "EAR";
}
