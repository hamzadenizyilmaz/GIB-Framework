using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.DataProtection;
using GIBFramework.DAL.Catalog;
using GIBFramework.Models.Audit;
using GIBFramework.Models.Messaging;

namespace GIBFramework.Services.Messaging;

public sealed record SmtpSettingsView(bool Enabled, string Host, int Port, SmtpSecurity Security, string? UserName, bool HasPassword, string FromAddress, string FromName, string? ReplyTo);

public sealed record SmsSettingsView(bool Enabled, string Provider, string? UserCode, bool HasPassword, string? Header, string Encoding);

public sealed record MessagingSettingsView(
    SmtpSettingsView Smtp,
    SmsSettingsView Sms,
    string? EmailFooter,
    string BrandColor,
    bool PlatformSmtpAvailable,
    bool PlatformSmsAvailable,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);

public sealed record SaveSmtpRequest(
    bool Enabled,
    [StringLength(200)] string? Host,
    [Range(1, 65535)] int Port,
    SmtpSecurity Security,
    [StringLength(200)] string? UserName,
    [StringLength(500)] string? Password,
    bool ClearPassword,
    [StringLength(200)] string? FromAddress,
    [StringLength(200)] string? FromName,
    [StringLength(200)] string? ReplyTo,
    [StringLength(1000)] string? EmailFooter,
    [StringLength(7)] string? BrandColor);

public sealed record SaveSmsRequest(
    bool Enabled,
    [StringLength(50)] string? UserCode,
    [StringLength(200)] string? Password,
    bool ClearPassword,
    [StringLength(11)] string? Header,
    [StringLength(10)] string? Encoding);

public sealed record TemplateView(
    string Key,
    string Label,
    string Audience,
    string Description,
    IReadOnlyList<TemplateVariable> Variables,
    MessageChannel Channel,
    bool Enabled,
    string? Subject,
    string Body,
    bool IsCustom,
    bool DefaultEnabled,
    string? DefaultSubject,
    string DefaultBody);

public sealed record SaveTemplateRequest(
    [Required, StringLength(60)] string Key,
    MessageChannel Channel,
    bool Enabled,
    [StringLength(300)] string? Subject,
    [Required, StringLength(20000)] string Body);

public sealed record EffectiveTemplate(bool Enabled, string? Subject, string Body);

public sealed class MessagingSettingsService(
    ITenantSettingsRepository settings,
    IDataProtectionProvider dataProtection,
    MessagingOptions options,
    IAuditTrail audit,
    ITenantContext context,
    IClock clock)
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("GIBFramework.Messaging.v1");

    public bool PlatformSmtpAvailable => !string.IsNullOrWhiteSpace(options.Smtp.Host) && !string.IsNullOrWhiteSpace(options.Smtp.FromAddress);

    public bool PlatformSmsAvailable => !string.IsNullOrWhiteSpace(options.Sms.UserCode) && !string.IsNullOrWhiteSpace(options.Sms.Password) && !string.IsNullOrWhiteSpace(options.Sms.Header);

    public async Task<MessagingSettings> GetAsync(Guid? tenantId, CancellationToken ct) =>
        tenantId is { } t ? (await settings.GetAsync(t, ct)).Messaging ?? new MessagingSettings() : new MessagingSettings();

    public async Task<MessagingSettingsView> ViewAsync(CancellationToken ct)
    {
        var m = await GetAsync(context.RequireTenant(), ct);
        return new MessagingSettingsView(
            new SmtpSettingsView(m.Smtp.Enabled, m.Smtp.Host, m.Smtp.Port, m.Smtp.Security, m.Smtp.UserName, m.Smtp.PasswordProtected is not null, m.Smtp.FromAddress, m.Smtp.FromName, m.Smtp.ReplyTo),
            new SmsSettingsView(m.Sms.Enabled, m.Sms.Provider, m.Sms.UserCode, m.Sms.PasswordProtected is not null, m.Sms.Header, m.Sms.Encoding),
            m.EmailFooter,
            TemplateRenderer.SafeColor(m.BrandColor),
            PlatformSmtpAvailable,
            PlatformSmsAvailable,
            m.UpdatedAt,
            m.UpdatedBy);
    }

    public async Task<MessagingSettingsView> SaveSmtpAsync(SaveSmtpRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new List<string>();
        var host = request.Host?.Trim() ?? string.Empty;
        var from = request.FromAddress?.Trim() ?? string.Empty;
        if (request.Enabled && host.Length == 0)
        {
            errors.Add("SMTP sunucusu zorunludur.");
        }

        if (request.Enabled && !IsEmail(from))
        {
            errors.Add("Gönderen e-posta adresi geçerli olmalıdır.");
        }

        if (!string.IsNullOrWhiteSpace(request.ReplyTo) && !IsEmail(request.ReplyTo.Trim()))
        {
            errors.Add("Yanıt adresi geçerli bir e-posta olmalıdır.");
        }

        if (!string.IsNullOrWhiteSpace(request.BrandColor) && TemplateRenderer.SafeColor(request.BrandColor) != request.BrandColor)
        {
            errors.Add("Marka rengi #RRGGBB biçiminde olmalıdır.");
        }

        Fail(errors);
        return await MutateAsync(m =>
        {
            m.Smtp.Enabled = request.Enabled;
            m.Smtp.Host = host;
            m.Smtp.Port = request.Port;
            m.Smtp.Security = request.Security;
            m.Smtp.UserName = Clean(request.UserName);
            if (request.ClearPassword)
            {
                m.Smtp.PasswordProtected = null;
            }
            else if (!string.IsNullOrEmpty(request.Password))
            {
                m.Smtp.PasswordProtected = _protector.Protect(request.Password);
            }

            m.Smtp.FromAddress = from;
            m.Smtp.FromName = request.FromName?.Trim() ?? string.Empty;
            m.Smtp.ReplyTo = Clean(request.ReplyTo);
            m.EmailFooter = Clean(request.EmailFooter);
            m.BrandColor = string.IsNullOrWhiteSpace(request.BrandColor) ? "#0778E6" : request.BrandColor;
        }, "MESSAGING_SMTP_UPDATED", new { request.Enabled, host, request.Port, request.Security, from }, ct);
    }

    public async Task<MessagingSettingsView> SaveSmsAsync(SaveSmsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new List<string>();
        var encoding = (request.Encoding ?? "TR").Trim().ToUpperInvariant();
        if (encoding is not ("TR" or "UTF-8" or "UNICODE"))
        {
            errors.Add("Karakter kodlaması TR, UTF-8 veya UNICODE olmalıdır.");
        }

        if (request.Enabled && string.IsNullOrWhiteSpace(request.UserCode))
        {
            errors.Add("Netgsm abone numarası (kullanıcı kodu) zorunludur.");
        }

        if (request.Enabled && string.IsNullOrWhiteSpace(request.Header))
        {
            errors.Add("SMS başlığı (gönderici adı) zorunludur.");
        }

        if (!string.IsNullOrWhiteSpace(request.Header) && request.Header.Trim().Length is < 3 or > 11)
        {
            errors.Add("SMS başlığı 3-11 karakter olmalıdır.");
        }

        Fail(errors);
        return await MutateAsync(m =>
        {
            m.Sms.Enabled = request.Enabled;
            m.Sms.Provider = "Netgsm";
            m.Sms.UserCode = Clean(request.UserCode);
            if (request.ClearPassword)
            {
                m.Sms.PasswordProtected = null;
            }
            else if (!string.IsNullOrEmpty(request.Password))
            {
                m.Sms.PasswordProtected = _protector.Protect(request.Password);
            }

            m.Sms.Header = Clean(request.Header);
            m.Sms.Encoding = encoding;
        }, "MESSAGING_SMS_UPDATED", new { request.Enabled, request.UserCode, request.Header, encoding }, ct);
    }

    public async Task<IReadOnlyList<TemplateView>> TemplatesAsync(CancellationToken ct)
    {
        var m = await GetAsync(context.RequireTenant(), ct);
        var list = new List<TemplateView>();
        foreach (var def in TemplateCatalog.All)
        {
            foreach (var channel in new[] { MessageChannel.Email, MessageChannel.Sms }.Where(def.Supports))
            {
                var custom = m.Templates.FirstOrDefault(t => t.Key == def.Key && t.Channel == channel);
                var effective = Effective(m, def, channel);
                var (defSubject, defBody, defEnabled) = channel == MessageChannel.Email
                    ? (def.EmailSubject, def.EmailBody!, def.EmailEnabled)
                    : (null, def.SmsBody!, def.SmsEnabled);
                list.Add(new TemplateView(def.Key, def.Label, def.Audience, def.Description, def.Variables, channel, effective.Enabled,
                    effective.Subject, effective.Body, custom is not null, defEnabled, defSubject, defBody));
            }
        }

        return list;
    }

    public async Task SaveTemplateAsync(SaveTemplateRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var def = TemplateCatalog.Find(request.Key) ?? throw new NotFoundException("Şablon bulunamadı.");
        if (!def.Supports(request.Channel))
        {
            throw new ValidationFailedException("TEMPLATE_CHANNEL", "Bu şablon bu kanalı desteklemiyor.", []);
        }

        var known = def.Variables.Select(v => v.Name);
        var unknown = TemplateRenderer.UnknownVariables((request.Subject ?? string.Empty) + " " + request.Body, known);
        var errors = unknown.Select(u => $"Tanımsız değişken: {{{{{u}}}}}").ToList();
        if (request.Channel == MessageChannel.Email && string.IsNullOrWhiteSpace(request.Subject))
        {
            errors.Add("E-posta konusu zorunludur.");
        }

        if (request.Channel == MessageChannel.Sms && request.Body.Length > 883)
        {
            errors.Add("SMS metni en fazla 883 karakter olabilir.");
        }

        Fail(errors);
        await MutateAsync(m =>
        {
            m.Templates.RemoveAll(t => t.Key == request.Key && t.Channel == request.Channel);
            m.Templates.Add(new TemplateOverride
            {
                Key = request.Key,
                Channel = request.Channel,
                Enabled = request.Enabled,
                Subject = request.Channel == MessageChannel.Email ? request.Subject?.Trim() : null,
                Body = request.Body,
            });
        }, "MESSAGING_TEMPLATE_UPDATED", new { request.Key, request.Channel, request.Enabled }, ct);
    }

    public async Task ResetTemplateAsync(string key, MessageChannel channel, CancellationToken ct) =>
        await MutateAsync(m => m.Templates.RemoveAll(t => t.Key == key && t.Channel == channel), "MESSAGING_TEMPLATE_RESET", new { key, channel }, ct);

    public static EffectiveTemplate Effective(MessagingSettings settings, TemplateDefinition def, MessageChannel channel)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(def);
        var custom = settings.Templates.FirstOrDefault(t => t.Key == def.Key && t.Channel == channel);
        if (custom is not null)
        {
            return new EffectiveTemplate(custom.Enabled, custom.Subject, custom.Body);
        }

        return channel == MessageChannel.Email
            ? new EffectiveTemplate(def.EmailEnabled, def.EmailSubject, def.EmailBody ?? string.Empty)
            : new EffectiveTemplate(def.SmsEnabled, null, def.SmsBody ?? string.Empty);
    }

    public bool SmtpConfigured(MessagingSettings m) => (m.Smtp.Enabled && !string.IsNullOrWhiteSpace(m.Smtp.Host)) || PlatformSmtpAvailable;

    public bool SmsConfigured(MessagingSettings m) => (m.Sms.Enabled && !string.IsNullOrWhiteSpace(m.Sms.UserCode) && m.Sms.PasswordProtected is not null) || PlatformSmsAvailable;

    public SmtpEndpoint? ResolveSmtp(MessagingSettings m)
    {
        ArgumentNullException.ThrowIfNull(m);
        if (m.Smtp.Enabled && !string.IsNullOrWhiteSpace(m.Smtp.Host) && IsEmail(m.Smtp.FromAddress))
        {
            return new SmtpEndpoint(m.Smtp.Host, m.Smtp.Port, m.Smtp.Security, m.Smtp.UserName, Unprotect(m.Smtp.PasswordProtected),
                m.Smtp.FromAddress, m.Smtp.FromName, m.Smtp.ReplyTo);
        }

        if (!PlatformSmtpAvailable)
        {
            return null;
        }

        var p = options.Smtp;
        var security = Enum.TryParse<SmtpSecurity>(p.Security, true, out var s) ? s : SmtpSecurity.StartTls;
        return new SmtpEndpoint(p.Host, p.Port, security, string.IsNullOrWhiteSpace(p.UserName) ? null : p.UserName, p.Password, p.FromAddress, p.FromName,
            string.IsNullOrWhiteSpace(p.ReplyTo) ? null : p.ReplyTo);
    }

    public NetgsmCredentials? ResolveSms(MessagingSettings m)
    {
        ArgumentNullException.ThrowIfNull(m);
        if (m.Sms.Enabled && !string.IsNullOrWhiteSpace(m.Sms.UserCode) && m.Sms.PasswordProtected is not null)
        {
            return new NetgsmCredentials(m.Sms.UserCode, Unprotect(m.Sms.PasswordProtected) ?? string.Empty, m.Sms.Header, m.Sms.Encoding);
        }

        return PlatformSmsAvailable ? new NetgsmCredentials(options.Sms.UserCode, options.Sms.Password, options.Sms.Header, "TR") : null;
    }

    public NetgsmCredentials? StoredSms(MessagingSettings m)
    {
        ArgumentNullException.ThrowIfNull(m);
        return !string.IsNullOrWhiteSpace(m.Sms.UserCode) && m.Sms.PasswordProtected is not null
            ? new NetgsmCredentials(m.Sms.UserCode, Unprotect(m.Sms.PasswordProtected) ?? string.Empty, m.Sms.Header, m.Sms.Encoding)
            : null;
    }

    public string ProtectSecret(string value) => _protector.Protect(value);

    public string? Unprotect(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(value);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    public static bool IsEmail(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 254 && new EmailAddressAttribute().IsValid(value) && value.Contains('.', StringComparison.Ordinal);

    private async Task<MessagingSettingsView> MutateAsync(Action<MessagingSettings> change, string auditAction, object auditData, CancellationToken ct)
    {
        var tenantId = context.RequireTenant();
        var current = await settings.GetAsync(tenantId, ct);
        current.Messaging ??= new MessagingSettings();
        change(current.Messaging);
        var now = clock.UtcNow;
        var user = context.RequireUser();
        current.Messaging.UpdatedAt = now;
        current.Messaging.UpdatedBy = user;
        await settings.SaveAsync(tenantId, current, user, now, ct);
        await audit.AppendSystemAsync(tenantId, new AuditEntry(auditAction, "Tenant", tenantId.ToString(), "Success", auditData), null, ct);
        return await ViewAsync(ct);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Fail(List<string> errors)
    {
        if (errors.Count > 0)
        {
            throw new ValidationFailedException("MESSAGING_INVALID", "Ayarlar kaydedilemedi.", errors);
        }
    }
}
