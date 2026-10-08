using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using GIBFramework.Models.Identity;

namespace GIBFramework.Services.Auth;

public sealed record IssuedToken(string AccessToken, DateTimeOffset ExpiresAt);

public sealed class TokenService(JwtOptions options, IClock clock)
{
    public const string SecurityStampClaim = "sstamp";
    public const string PasswordChangeClaim = "pwd_change";
    public const string AuthMethodClaim = "amr";

    public IssuedToken IssueForUser(UserAccount user, bool mfa, TimeSpan? lifetime = null)
    {
        ArgumentNullException.ThrowIfNull(user);
        var claims = new List<Claim>
        {
            new(SecurityStampClaim, user.SecurityStamp.ToString("N")),
            new(AuthMethodClaim, mfa ? "mfa" : "pwd"),
        };
        if (user.MustChangePassword)
        {
            claims.Add(new Claim(PasswordChangeClaim, "true"));
        }

        return Create(user.UserCode, user.DisplayName, user.TenantId, user.Roles, claims, lifetime ?? TimeSpan.FromMinutes(options.AccessTokenMinutes));
    }

    public string Issue(string userId, Guid? tenantId, IReadOnlyCollection<string> roles, TimeSpan? lifetime = null) =>
        Create(userId, userId, tenantId, roles, [], lifetime ?? TimeSpan.FromHours(8)).AccessToken;

    private IssuedToken Create(string subject, string name, Guid? tenantId, IReadOnlyCollection<string> roles, IEnumerable<Claim> extra, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(roles);
        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            throw new InvalidOperationException("Jwt:SigningKey tanımlı değil.");
        }

        var unknown = roles.Except(Roles.All).ToList();
        if (unknown.Count > 0)
        {
            throw new ValidationFailedException("TOKEN_ROLE", "Tanımsız rol.", unknown);
        }

        var claims = new List<Claim> { new(GibFrameworkClaims.Subject, subject), new(GibFrameworkClaims.Name, name) };
        if (tenantId is { } t)
        {
            claims.Add(new Claim(GibFrameworkClaims.TenantId, t.ToString()));
        }

        claims.AddRange(roles.Select(r => new Claim(GibFrameworkClaims.Role, r)));
        claims.AddRange(extra);

        var now = clock.UtcNow;
        var expires = now.Add(lifetime);
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime.AddMinutes(-1),
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)), SecurityAlgorithms.HmacSha256));
        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
