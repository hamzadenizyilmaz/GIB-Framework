namespace GIBFramework.Models.Announcements;

public enum AnnouncementLevel
{
    Info,
    Success,
    Warning,
    Danger,
}

public enum AnnouncementAudience
{
    Everyone,

    Authenticated,

    Tenant,
}

public sealed record Announcement(
    Guid Id,
    string Title,
    string Message,
    AnnouncementLevel Level,
    AnnouncementAudience Audience,
    Guid? TenantId,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    bool IsActive,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
