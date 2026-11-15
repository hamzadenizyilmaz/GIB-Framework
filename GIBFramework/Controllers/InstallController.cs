using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using GIBFramework.DAL;
using GIBFramework.DAL.Identity;
using GIBFramework.DTOs;
using GIBFramework.Mapper;
using GIBFramework.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GIBFramework.Controllers;

[ApiController]
[Route("api/v1/install")]
[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class InstallController(InstallOptions options, DatabaseInitialization database, UserBootstrapper bootstrap, IClock clock) : ControllerBase
{
    public const string RateLimitPolicy = "install";
    [HttpGet("/install")]
    public async Task<IActionResult> Page(CancellationToken ct) =>
        database.Ready && await IsAvailableAsync(ct) ? Redirect("/app/#/install") : NotFound();

    [HttpGet]
    public async Task<IActionResult> Status(CancellationToken ct) =>
        database.Ready && await IsAvailableAsync(ct) ? NoContent() : NotFound();

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicy)]
    public async Task<IActionResult> Create(InstallRequest request, CancellationToken ct)
    {
        if (!options.IsConfigured)
            return NotFound();

        if (!database.Ready)
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Veritabanı hazır değil. SQL şemasını ve bağlantı ayarlarını kontrol edin.");

        if (!await IsAvailableAsync(ct))
            return NotFound();

        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(options.TokenSha256),
            SHA256.HashData(Encoding.UTF8.GetBytes(request.InstallToken))))
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Kurulum anahtarı geçersiz.");

        var userCode = UserRepository.Normalize(request.UserCode);
        var ownerCode = UserRepository.Normalize(request.OwnerCode);
        if (userCode == ownerCode || request.Password == request.OwnerPassword)
            return UnprocessableEntity(new { title = "Platform yöneticisi ve firma sahibi için farklı kullanıcı kodları ve parolalar girin." });

        var errors = PasswordPolicy.Validate(request.Password, userCode).Concat(PasswordPolicy.Validate(request.OwnerPassword, ownerCode)).Distinct().ToArray();
        if (errors.Length > 0)
            return Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: string.Join(" ", errors));

        if (!TaxIdentifier.TryParse(request.Company.Profile.TaxId, out _, out var taxError))
            return Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: taxError.Message);

        if (request.Company.EFaturaPrefix == "TSL" || request.Company.EArsivPrefix == "TSL" || request.Company.EFaturaPrefix == request.Company.EArsivPrefix)
            return Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "e-Fatura ve e-Arşiv serileri farklı olmalı ve TSL kullanılamaz.");

        if (!await bootstrap.TryCreateAsync(userCode, request.Password, mustChangePassword: false, ct,
            request.Company.ToTenant(clock.UtcNow), ownerCode, request.OwnerPassword))
            return NotFound();

        return NoContent();
    }

    private async Task<bool> IsAvailableAsync(CancellationToken ct) =>
        options.IsConfigured && await bootstrap.IsWebInstallAvailableAsync(ct);
}

public sealed record InstallRequest(
    [Required, StringLength(50, MinimumLength = 3), RegularExpression("^[A-Za-z0-9._-]+$")] string UserCode,
    [Required, StringLength(200, MinimumLength = PasswordPolicy.MinLength)] string Password,
    [Required, StringLength(256, MinimumLength = 32)] string InstallToken,
    [Required] CreateTenantRequest Company,
    [Required, StringLength(50, MinimumLength = 3), RegularExpression("^[A-Za-z0-9._-]+$")] string OwnerCode,
    [Required, StringLength(200, MinimumLength = PasswordPolicy.MinLength)] string OwnerPassword);
