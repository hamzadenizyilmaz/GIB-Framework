namespace GIBFramework.Models.Catalog;

public sealed class Customer
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string TaxId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? FirstName { get; set; }

    public string? FamilyName { get; set; }

    public BookkeepingRegime Regime { get; set; } = BookkeepingRegime.NotATaxpayer;

    public string? TaxOffice { get; set; }

    public string? ProvinceName { get; set; }

    public string? DistrictName { get; set; }

    public string? NeighborhoodName { get; set; }

    public string? Street { get; set; }

    public string? BuildingNumber { get; set; }

    public string? PostalCode { get; set; }

    public string Country { get; set; } = "Türkiye";

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public bool IsEFaturaRegistered { get; set; }

    public string? EFaturaAlias { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class Product
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string? Code { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string UnitCode { get; set; } = "C62";

    public decimal UnitPrice { get; set; }

    public string Currency { get; set; } = "TRY";

    public decimal VatRate { get; set; } = 20;

    public string? VatExemptionCode { get; set; }

    public string? WithholdingCode { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class NoteTemplate
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public bool AutoAdd { get; set; }
}

public sealed class BankAccount
{
    public Guid Id { get; set; }

    public string BankName { get; set; } = string.Empty;

    public string? Branch { get; set; }

    public string Iban { get; set; } = string.Empty;

    public string AccountHolder { get; set; } = string.Empty;

    public string Currency { get; set; } = "TRY";

    public bool AddToNotes { get; set; }
}

public sealed class InvoiceDefaults
{
    public InvoiceProfile Profile { get; set; } = InvoiceProfile.TEMELFATURA;

    public string Currency { get; set; } = "TRY";

    public decimal VatRate { get; set; } = 20;

    public string UnitCode { get; set; } = "C62";

    public int? PaymentDueDays { get; set; }

    public bool SaveCustomer { get; set; } = true;
}

public sealed class SecurityPolicy
{
    public bool RequireTotp { get; set; }

    public int? SessionMinutes { get; set; }

    public int IdleLogoutMinutes { get; set; }

    public int? ApiKeyMaxDays { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}

public sealed class TenantSettings
{
    public InvoiceDefaults Defaults { get; set; } = new();

    public List<NoteTemplate> NoteTemplates { get; set; } = [];

    public List<BankAccount> BankAccounts { get; set; } = [];

    public SecurityPolicy Security { get; set; } = new();

    public Messaging.MessagingSettings Messaging { get; set; } = new();

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}
