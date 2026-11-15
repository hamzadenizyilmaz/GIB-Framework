using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GIBFramework.Models.Compliance;

namespace GIBFramework.Services.Compliance;

/// <summary>Panelden yapılan kural incelemesi / onayı. Kuralın içerik parmak izine bağlıdır.</summary>
public sealed record RuleApproval(
    string VersionTag,
    string Fingerprint,
    string Status,
    string ReviewedBy,
    DateTimeOffset ReviewedAt,
    string? ReviewNote,
    string? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    string? ApprovalNote);

/// <summary>Kuralın panelde görünen geçerli inceleme durumu.</summary>
public sealed record EffectiveRuleReview(
    RuleReviewStatus Status,
    string Source,
    string? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? ReviewNote,
    string? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    string? ApprovalNote,
    bool Stale);

/// <summary>
/// İki kişili kural onayı (dört göz ilkesi). Kural dosyaları değişmez; panelden verilen inceleme ve onaylar
/// App_Data/rule-reviews.json dosyasında saklanır. Onay, kuralın içeriğinden (tür, dönem, parametreler,
/// hukuki dayanak…) hesaplanan parmak izine bağlıdır: kural güncellenirse onay kendiliğinden geçersiz olur.
/// </summary>
public sealed class RuleReviewStore
{
    public const string Reviewed = "Reviewed";
    public const string Approved = "Approved";

    private readonly string path;
    private readonly object gate = new();
    private Dictionary<string, RuleApproval> current = new(StringComparer.Ordinal);
    private DateTime loadedAt = DateTime.MinValue;

    public RuleReviewStore(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        path = Path.Combine(environment.ContentRootPath, "App_Data", "rule-reviews.json");
        Reload();
    }

    public static string Fingerprint(LegalRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var canonical = JsonSerializer.Serialize(new
        {
            rule.Code,
            rule.Version,
            rule.Kind,
            rule.Title,
            rule.Description,
            From = rule.Period.From.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            To = rule.Period.To?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            Severity = rule.Severity.ToString(),
            rule.Blocking,
            rule.Priority,
            LegalBasis = rule.LegalBasis.Select(b => b.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray(),
            Parameters = rule.Parameters.Values.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                .Select(p => new[] { p.Key.ToLowerInvariant(), p.Value.GetRawText() }).ToArray(),
        });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>Kural, dosyada veya panelde geçerli (içeriği değişmemiş) iki kişili onaya sahip mi?</summary>
    public bool IsApproved(LegalRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (rule.IsApproved) return true;
        return Snapshot().TryGetValue(rule.VersionTag, out var a) && a.Status == Approved && a.Fingerprint == Fingerprint(rule);
    }

    public EffectiveRuleReview Effective(LegalRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (rule.Review.Status is RuleReviewStatus.Approved or RuleReviewStatus.Retired)
        {
            return new(rule.Review.Status, "Package", rule.Review.ReviewedBy, null, rule.Review.Notes, rule.Review.ApprovedBy, rule.Review.ApprovedAt, null, false);
        }

        if (!Snapshot().TryGetValue(rule.VersionTag, out var a))
        {
            return new(rule.Review.Status, "Package", null, null, rule.Review.Notes, null, null, null, false);
        }

        if (a.Fingerprint != Fingerprint(rule))
        {
            // Kural içeriği inceleme/onaydan sonra değişti: kayıt yalnızca bilgi amaçlı gösterilir.
            return new(rule.Review.Status, "Panel", a.ReviewedBy, a.ReviewedAt, a.ReviewNote, a.ApprovedBy, a.ApprovedAt, a.ApprovalNote, true);
        }

        var status = a.Status == Approved ? RuleReviewStatus.Approved : RuleReviewStatus.PendingExpertReview;
        return new(status, "Panel", a.ReviewedBy, a.ReviewedAt, a.ReviewNote, a.ApprovedBy, a.ApprovedAt, a.ApprovalNote, false);
    }

    /// <summary>1. adım: kuralı hukuki dayanağıyla karşılaştırıp incelendi olarak işaretler.</summary>
    public RuleApproval Review(LegalRule rule, string user, string? note, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(rule);
        Guard(rule);
        return Mutate(map =>
        {
            if (map.TryGetValue(rule.VersionTag, out var existing) && existing.Status == Approved && existing.Fingerprint == Fingerprint(rule))
                throw new ValidationFailedException("RULE_ALREADY_APPROVED", "Kural zaten onaylı. Yeniden incelemek için önce onayı geri alın.", []);
            var review = new RuleApproval(rule.VersionTag, Fingerprint(rule), Reviewed, user, now, Clean(note), null, null, null);
            map[rule.VersionTag] = review;
            return review;
        });
    }

    /// <summary>2. adım: incelemeyi yapan kişiden FARKLI bir kullanıcı onaylar.</summary>
    public RuleApproval Approve(LegalRule rule, string user, string? note, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(rule);
        Guard(rule);
        return Mutate(map =>
        {
            if (!map.TryGetValue(rule.VersionTag, out var review) || review.Fingerprint != Fingerprint(rule))
                throw new ValidationFailedException("RULE_NOT_REVIEWED", "Kural önce incelenmelidir (inceleme yoksa veya kural içeriği değiştiyse).", []);
            if (review.Status == Approved)
                throw new ValidationFailedException("RULE_ALREADY_APPROVED", "Kural zaten onaylı.", []);
            if (string.Equals(review.ReviewedBy, user, StringComparison.OrdinalIgnoreCase))
                throw new ValidationFailedException("RULE_FOUR_EYES", "Dört göz ilkesi: kuralı inceleyen kişi aynı kuralı onaylayamaz. Başka bir yetkili onaylamalıdır.", []);
            var approved = review with { Status = Approved, ApprovedBy = user, ApprovedAt = now, ApprovalNote = Clean(note) };
            map[rule.VersionTag] = approved;
            return approved;
        });
    }

    /// <summary>İnceleme veya onayı geri alır; kural yeniden "İncelemede" durumuna döner.</summary>
    public void Revoke(LegalRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        Mutate(map =>
        {
            if (!map.Remove(rule.VersionTag))
                throw new ValidationFailedException("RULE_NO_PANEL_REVIEW", "Bu kural için panelden verilmiş bir inceleme veya onay yok.", []);
            return 0;
        });
    }

    private static void Guard(LegalRule rule)
    {
        if (rule.Review.Status == RuleReviewStatus.Retired)
            throw new ValidationFailedException("RULE_RETIRED", "Geri çekilmiş kural onaylanamaz.", []);
        if (rule.Review.Status == RuleReviewStatus.Approved)
            throw new ValidationFailedException("RULE_PACKAGE_APPROVED", "Kural, onaylı kural paketinde zaten onaylı.", []);
    }

    private static string? Clean(string? note)
    {
        var text = note?.Trim();
        return string.IsNullOrEmpty(text) ? null : text.Length > 1000 ? text[..1000] : text;
    }

    private Dictionary<string, RuleApproval> Snapshot()
    {
        // Birden fazla uygulama süreci aynı dosyayı paylaşabilir; değişiklik diğer süreçlere de yansısın.
        if (File.Exists(path) && File.GetLastWriteTimeUtc(path) > loadedAt)
        {
            lock (gate) Reload();
        }
        return current;
    }

    private void Reload()
    {
        if (!File.Exists(path)) return;
        var list = JsonSerializer.Deserialize<List<RuleApproval>>(File.ReadAllText(path)) ?? [];
        current = list.ToDictionary(a => a.VersionTag, StringComparer.Ordinal);
        loadedAt = File.GetLastWriteTimeUtc(path);
    }

    private T Mutate<T>(Func<Dictionary<string, RuleApproval>, T> change)
    {
        lock (gate)
        {
            Reload();
            var map = new Dictionary<string, RuleApproval>(current, StringComparer.Ordinal);
            var result = change(map);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var staging = path + ".tmp";
            try
            {
                File.WriteAllText(staging, JsonSerializer.Serialize(map.Values.OrderBy(a => a.VersionTag, StringComparer.Ordinal).ToList(),
                    new JsonSerializerOptions { WriteIndented = true }));
                File.Move(staging, path, overwrite: true);
                current = map;
                loadedAt = File.GetLastWriteTimeUtc(path);
            }
            finally
            {
                File.Delete(staging);
            }
            return result;
        }
    }
}
