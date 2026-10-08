namespace GIBFramework.Models.Tenancy;

public sealed class Tenant
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public InvoiceParty Profile { get; set; } = new();

    public string EFaturaPrefix { get; set; } = "EFT";

    public string EArsivPrefix { get; set; } = "EAR";

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}
