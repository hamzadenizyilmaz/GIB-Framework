using Microsoft.AspNetCore.Identity;
using GIBFramework.DAL.Identity;
using GIBFramework.DAL.Tenants;
using GIBFramework.Models.Identity;
using GIBFramework.Models.Tenancy;

namespace GIBFramework.Services.Auth;

public sealed class DemoOptions
{
    public const string Section = "Demo";

    public bool Enabled { get; set; }

    public string Password { get; set; } = string.Empty;
}

public sealed partial class DemoDataSeeder(
    ITenantRepository tenants,
    IUserRepository users,
    IPasswordHasher<UserAccount> hasher,
    DemoOptions options,
    IHostEnvironment environment,
    IClock clock,
    ILogger<DemoDataSeeder> logger)
{
    public const string TenantTaxId = "1234567890";

    private static readonly (string UserCode, string DisplayName, string[] Roles)[] DemoUsers =
    [
        ("yonetici", "Genel Müdür", [Roles.TenantOwner]),
        ("firmaadmin", "Firma Yöneticisi", [Roles.CompanyAdmin]),
        ("onay", "Muhasebe Müdürü", [Roles.AccountingManager]),
        ("muhasebe", "Muhasebe Uzmanı", [Roles.Accountant, Roles.InvoiceCreator]),
        ("imza", "İmza Yetkilisi", [Roles.InvoiceSigner, Roles.IntegratorManager]),
        ("denetci", "Denetçi", [Roles.ArchiveAuditor, Roles.ReadOnlyAuditor]),
    ];

    public async Task EnsureAsync(CancellationToken ct)
    {
        if (!options.Enabled || !(environment.IsDevelopment() || environment.IsEnvironment(Base.Extensions.ServiceCollectionExtensions.DemoEnvironment)) || string.IsNullOrWhiteSpace(options.Password))
        {
            return;
        }

        var now = clock.UtcNow;
        var tenant = (await tenants.ListAsync(ct)).FirstOrDefault(t => t.Profile.TaxId == TenantTaxId);
        if (tenant is null)
        {
            tenant = new Tenant
            {
                Id = Guid.CreateVersion7(now),
                Name = "Demo Yazılım A.Ş.",
                EFaturaPrefix = "DEF",
                EArsivPrefix = "DEA",
                IsActive = true,
                CreatedAt = now,
                Profile = new InvoiceParty
                {
                    TaxId = TenantTaxId,
                    Kind = PartyKind.LegalEntity,
                    Regime = BookkeepingRegime.Bilanco,
                    Title = "Demo Yazılım Anonim Şirketi",
                    TaxOffice = "Kadıköy",
                    Street = "Bağdat Caddesi",
                    BuildingNumber = "100",
                    District = "Kadıköy",
                    City = "İstanbul",
                    PostalCode = "34710",
                    Email = "fatura@demo.local",
                    IsEFaturaRegistered = true,
                    IsEArchiveRegistered = true,
                },
            };
            await tenants.CreateAsync(tenant, ct);
            Log.TenantCreated(logger, tenant.Name, tenant.Id);
        }

        foreach (var (userCode, displayName, roles) in DemoUsers)
        {
            if (await users.FindByCodeAsync(userCode, ct) is { } existing)
            {
                if (existing.TenantId == tenant.Id && (!existing.Roles.Order().SequenceEqual(roles.Order()) || existing.DisplayName != displayName))
                {
                    existing.Roles = [.. roles];
                    existing.DisplayName = displayName;
                    existing.SecurityStamp = Guid.NewGuid();
                    existing.UpdatedAt = now;
                    await users.UpdateAsync(existing, ct);
                }

                continue;
            }

            var user = new UserAccount
            {
                Id = Guid.CreateVersion7(now),
                TenantId = tenant.Id,
                UserCode = UserRepository.Normalize(userCode),
                DisplayName = displayName,
                Roles = [.. roles],
                IsActive = true,
                MustChangePassword = false,
                SecurityStamp = Guid.NewGuid(),
                CreatedAt = now,
                UpdatedAt = now,
            };
            user.PasswordHash = hasher.HashPassword(user, options.Password);
            await users.InsertAsync(user, ct);
            Log.UserCreated(logger, user.UserCode);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Demo firma oluşturuldu: {Name} ({TenantId})")]
        public static partial void TenantCreated(ILogger logger, string name, Guid tenantId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Demo kullanıcı oluşturuldu: {UserCode}")]
        public static partial void UserCreated(ILogger logger, string userCode);
    }
}
