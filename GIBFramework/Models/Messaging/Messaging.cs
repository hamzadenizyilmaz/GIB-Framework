namespace GIBFramework.Models.Messaging;

public enum MessageChannel
{
    Email,
    Sms,
}

public enum MessageStatus
{
    Queued,
    Sending,
    Sent,
    Failed,
    Cancelled,
}

public enum SmtpSecurity
{
    StartTls,
    SslOnConnect,
    None,
}

public sealed class SmtpSettings
{
    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public SmtpSecurity Security { get; set; } = SmtpSecurity.StartTls;

    public string? UserName { get; set; }

    public string? PasswordProtected { get; set; }

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = string.Empty;

    public string? ReplyTo { get; set; }
}

public sealed class SmsSettings
{
    public bool Enabled { get; set; }

    public string Provider { get; set; } = "Netgsm";

    public string? UserCode { get; set; }

    public string? PasswordProtected { get; set; }

    public string? Header { get; set; }

    public string Encoding { get; set; } = "TR";
}

public sealed class TemplateOverride
{
    public string Key { get; set; } = string.Empty;

    public MessageChannel Channel { get; set; }

    public bool Enabled { get; set; }

    public string? Subject { get; set; }

    public string Body { get; set; } = string.Empty;
}

public sealed class MessagingSettings
{
    public SmtpSettings Smtp { get; set; } = new();

    public SmsSettings Sms { get; set; } = new();

    public List<TemplateOverride> Templates { get; set; } = [];

    public string? EmailFooter { get; set; }

    public string BrandColor { get; set; } = "#0778E6";

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}

public sealed record MessageAttachmentRef(string Kind, Guid RefId);

public sealed class MessageExtras
{
    public List<MessageAttachmentRef> Attachments { get; set; } = [];

    public Dictionary<string, string> Secrets { get; set; } = [];
}

public sealed class OutboundMessage
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public MessageChannel Channel { get; set; }

    public string Recipient { get; set; } = string.Empty;

    public string? RecipientName { get; set; }

    public string? Subject { get; set; }

    public string Body { get; set; } = string.Empty;

    public string? TemplateKey { get; set; }

    public string? EntityType { get; set; }

    public string? EntityId { get; set; }

    public MessageExtras Extras { get; set; } = new();

    public MessageStatus Status { get; set; } = MessageStatus.Queued;

    public int Attempts { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public string? LastError { get; set; }

    public string? ProviderMessageId { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }
}

public sealed record MessageView(
    Guid Id,
    MessageChannel Channel,
    string Recipient,
    string? RecipientName,
    string? Subject,
    string? TemplateKey,
    string? EntityType,
    string? EntityId,
    MessageStatus Status,
    int Attempts,
    DateTimeOffset NextAttemptAt,
    string? LastError,
    string? ProviderMessageId,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt)
{
    public static MessageView From(OutboundMessage m) => new(
        m.Id, m.Channel, m.Recipient, m.RecipientName, m.Subject, m.TemplateKey, m.EntityType, m.EntityId, m.Status, m.Attempts,
        m.NextAttemptAt, m.LastError, m.ProviderMessageId, m.CreatedBy, m.CreatedAt, m.SentAt);
}

public sealed record OutboxEvent(long Id, Guid? TenantId, string EventType, string EntityType, string EntityId, string PayloadJson, DateTimeOffset CreatedAt, int Attempts);

public sealed record NotificationRecipient(string Name, string? Email, string? Phone);
