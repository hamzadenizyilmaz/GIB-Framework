using System.ComponentModel.DataAnnotations;
using GIBFramework.Models.Announcements;

namespace GIBFramework.DTOs;

public sealed record CreateTenantRequest
{
    [Required, StringLength(250)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public PartyDto Profile { get; init; } = new();

    public bool IsEFaturaRegistered { get; init; }

    public bool IsEArchiveRegistered { get; init; }

    [Required, RegularExpression("^[A-Z0-9]{3}$")]
    public string EFaturaPrefix { get; init; } = "EFT";

    [Required, RegularExpression("^[A-Z0-9]{3}$")]
    public string EArsivPrefix { get; init; } = "EAR";
}

public sealed record DevTokenRequest(
    [Required, StringLength(100)] string UserId,
    Guid? TenantId,
    [Required] IReadOnlyList<string> Roles);

public sealed record DevTokenResponse(string AccessToken, string TokenType, IReadOnlyList<string> Roles);

public sealed record TaxIdRequest(string? Value);

public sealed record ApproveSnapshotRequest(DateOnly EffectiveFrom);

public sealed record IncomingDecisionRequest(bool Accept);

public sealed record AnnouncementRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Title { get; init; } = string.Empty;

    [Required, StringLength(4000, MinimumLength = 1)]
    public string Message { get; init; } = string.Empty;

    public AnnouncementLevel Level { get; init; } = AnnouncementLevel.Info;

    public AnnouncementAudience Audience { get; init; } = AnnouncementAudience.Everyone;

    public Guid? TenantId { get; init; }

    public DateTimeOffset? StartsAt { get; init; }

    public DateTimeOffset? EndsAt { get; init; }

    public bool IsActive { get; init; } = true;
}
