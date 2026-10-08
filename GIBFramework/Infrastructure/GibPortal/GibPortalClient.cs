using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GIBFramework.Infrastructure.GibPortal;

public sealed class GibPortalException(string message, bool sessionExpired = false) : Exception(message)
{
    public bool SessionExpired { get; } = sessionExpired;
}

public sealed record GibPortalUserInfo(string TaxId, string Title, string? FirstName, string? LastName, string? TaxOffice, string? City);

public sealed record GibPortalRecipient(string? Title, string? FirstName, string? LastName, string? TaxOffice)
{
    public bool Found => !string.IsNullOrWhiteSpace(Title) || !string.IsNullOrWhiteSpace(FirstName) || !string.IsNullOrWhiteSpace(LastName);
}

public sealed record GibPortalDocumentRow(
    string Ettn,
    string? DocumentNumber,
    string? RecipientTaxId,
    string? RecipientTitle,
    string? DocumentDate,
    string? ApprovalStatus,
    JsonElement Raw)
{
    public bool IsApproved => ApprovalStatus == GibPortalProtocol.Approved;
}

public sealed class GibPortalClient(IHttpClientFactory httpFactory, GibPortalOptions options)
{
    public const string HttpClientName = "GibPortal";

    private static readonly JsonSerializerOptions PortalJson = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public Uri BaseUri(GibPortalEnvironment environment) =>
        new(environment == GibPortalEnvironment.Production ? options.ProductionBaseUrl : options.TestBaseUrl);

    public async Task<string> LoginAsync(GibPortalEnvironment environment, string userCode, string password, CancellationToken ct)
    {
        var root = await PostAsync(environment, GibPortalProtocol.LoginPath, new Dictionary<string, string>
        {
            ["assoscmd"] = environment == GibPortalEnvironment.Production ? GibPortalProtocol.LoginCommandProduction : GibPortalProtocol.LoginCommandTest,
            ["rtype"] = "json",
            ["userid"] = userCode,
            ["sifre"] = password,
            ["sifre2"] = password,
            ["parola"] = "1",
        }, ct);

        return root.TryGetProperty("token", out var token) && token.ValueKind == JsonValueKind.String && token.GetString() is { Length: > 0 } value
            ? value
            : throw new GibPortalException("GİB e-Arşiv Portal girişi başarısız: kullanıcı kodu veya şifre hatalı olabilir.");
    }

    public async Task<string> SuggestTestUserAsync(CancellationToken ct)
    {
        var root = await PostAsync(GibPortalEnvironment.Test, GibPortalProtocol.EsignPath, new Dictionary<string, string>
        {
            ["assoscmd"] = GibPortalProtocol.SuggestTestUserCommand,
            ["rtype"] = "json",
        }, ct);

        return Str(root, "userid") is { Length: > 0 } userId
            ? userId
            : throw new GibPortalException("GİB test portalında şu an boş test hesabı yok; biraz sonra tekrar deneyin.");
    }

    public async Task LogoutAsync(GibPortalEnvironment environment, string token, CancellationToken ct)
    {
        try
        {
            await PostAsync(environment, GibPortalProtocol.LoginPath, new Dictionary<string, string>
            {
                ["assoscmd"] = GibPortalProtocol.LogoutCommand,
                ["rtype"] = "json",
                ["token"] = token,
            }, ct);
        }
        catch (GibPortalException)
        {
        }
    }

    public async Task<JsonElement> DispatchAsync(GibPortalEnvironment environment, string token, string command, string pageName, object payload, CancellationToken ct)
    {
        var root = await PostAsync(environment, GibPortalProtocol.DispatchPath, new Dictionary<string, string>
        {
            ["cmd"] = command,
            ["callid"] = Guid.NewGuid().ToString(),
            ["pageName"] = pageName,
            ["token"] = token,
            ["jp"] = JsonSerializer.Serialize(payload, PortalJson),
        }, ct);

        if (!root.TryGetProperty("data", out var data))
        {
            throw new GibPortalException("GİB portalından beklenmeyen yanıt (data yok).");
        }

        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("hata", out var hata) && hata.ValueKind == JsonValueKind.String && hata.GetString() is { Length: > 0 } error)
        {
            throw new GibPortalException("GİB portalı: " + error, IsExpired(error));
        }

        return data;
    }

    public async Task<GibPortalUserInfo> GetUserInfoAsync(GibPortalEnvironment env, string token, CancellationToken ct)
    {
        var d = await DispatchAsync(env, token, GibPortalProtocol.UserInfo, GibPortalProtocol.PageUser, new { }, ct);
        return new GibPortalUserInfo(Str(d, "vknTckn") ?? string.Empty, Str(d, "unvan") ?? string.Empty, Str(d, "ad"), Str(d, "soyad"), Str(d, "vergiDairesi"), Str(d, "il"));
    }

    public async Task<GibPortalRecipient> GetRecipientAsync(GibPortalEnvironment env, string token, string taxId, CancellationToken ct)
    {
        var d = await DispatchAsync(env, token, GibPortalProtocol.RecipientInfo, GibPortalProtocol.PageInvoice, new Dictionary<string, string> { ["vknTcknn"] = taxId }, ct);
        return new GibPortalRecipient(
            NonEmpty(Str(d, "unvan"), Str(d, "aliciUnvan")),
            NonEmpty(Str(d, "adi"), Str(d, "ad"), Str(d, "aliciAdi")),
            NonEmpty(Str(d, "soyadi"), Str(d, "soyad"), Str(d, "aliciSoyadi")),
            NonEmpty(Str(d, "vergiDairesi"), Str(d, "vergiDairesiAdi")));
    }

    public async Task CreateDraftAsync(GibPortalEnvironment env, string token, IReadOnlyDictionary<string, object?> draft, CancellationToken ct)
    {
        var d = await DispatchAsync(env, token, GibPortalProtocol.CreateDraft, GibPortalProtocol.PageInvoice, draft, ct);
        var text = d.ValueKind == JsonValueKind.String ? d.GetString() ?? string.Empty : d.ToString();
        if (!text.Contains(GibPortalProtocol.CreateSuccessFragment, StringComparison.OrdinalIgnoreCase))
        {
            throw new GibPortalException("GİB portalı taslağı oluşturmadı: " + text, IsExpired(text));
        }
    }

    public async Task<IReadOnlyList<GibPortalDocumentRow>> ListDraftsAsync(GibPortalEnvironment env, string token, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var d = await DispatchAsync(env, token, GibPortalProtocol.ListDrafts, GibPortalProtocol.PageDrafts, new Dictionary<string, object>
        {
            ["baslangic"] = PortalDate(from),
            ["bitis"] = PortalDate(to),
            ["hangiTip"] = GibPortalProtocol.InvoiceKind,
            ["table"] = Array.Empty<object>(),
        }, ct);

        return d.ValueKind != JsonValueKind.Array
            ? []
            : [.. d.EnumerateArray()
                .Where(r => Str(r, "ettn") is not null)
                .Select(r => new GibPortalDocumentRow(Str(r, "ettn")!, Str(r, "belgeNumarasi"), Str(r, "aliciVknTckn"), Str(r, "aliciUnvanAdSoyad"),
                    Str(r, "belgeTarihi"), Str(r, "onayDurumu"), r.Clone()))];
    }

    public async Task<IReadOnlyList<JsonElement>> ListIssuedToMeAsync(GibPortalEnvironment env, string token, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var d = await DispatchAsync(env, token, GibPortalProtocol.ListIssuedToMe, GibPortalProtocol.PageIssuedToMe, new Dictionary<string, object>
        {
            ["baslangic"] = PortalDate(from),
            ["bitis"] = PortalDate(to),
        }, ct);

        return d.ValueKind == JsonValueKind.Array ? [.. d.EnumerateArray().Select(r => r.Clone())] : [];
    }

    public async Task<string> GetPhoneAsync(GibPortalEnvironment env, string token, CancellationToken ct)
    {
        var d = await DispatchAsync(env, token, GibPortalProtocol.PhoneNumber, GibPortalProtocol.PageDrafts, new { }, ct);
        return Str(d, "telefon") ?? throw new GibPortalException("GİB portalında kayıtlı cep telefonu bulunamadı.");
    }

    public async Task<string> SendSmsAsync(GibPortalEnvironment env, string token, string phone, CancellationToken ct)
    {
        var d = await DispatchAsync(env, token, GibPortalProtocol.SendSms, GibPortalProtocol.PageSms,
            new Dictionary<string, object> { ["CEPTEL"] = phone, ["KCEPTEL"] = false, ["TIP"] = string.Empty }, ct);
        return Str(d, "oid") ?? throw new GibPortalException("GİB portalı SMS işlem kimliği (oid) döndürmedi.");
    }

    public async Task<bool> VerifySmsAndSignAsync(GibPortalEnvironment env, string token, string code, string oid, IReadOnlyList<GibPortalDocumentRow> rows, CancellationToken ct)
    {
        var d = await DispatchAsync(env, token, GibPortalProtocol.VerifySmsAndSign, GibPortalProtocol.PageSms, new Dictionary<string, object>
        {
            ["SIFRE"] = code,
            ["OID"] = oid,
            ["OPR"] = 1,
            ["DATA"] = rows.Select(r => r.Raw).ToArray(),
        }, ct);

        return d.ValueKind == JsonValueKind.Object && d.TryGetProperty("sonuc", out var s)
            && (s.ValueKind == JsonValueKind.Number ? s.GetInt32() == 1 : s.GetString() == "1");
    }

    public Task SignWithHsmAsync(GibPortalEnvironment env, string token, IReadOnlyList<GibPortalDocumentRow> rows, CancellationToken ct) =>
        DispatchAsync(env, token, GibPortalProtocol.SignWithHsm, GibPortalProtocol.PageDrafts,
            new Dictionary<string, object> { ["imzalanacaklar"] = rows.Select(r => r.Raw).ToArray() }, ct);

    public async Task<string> GetHtmlAsync(GibPortalEnvironment env, string token, string ettn, bool approved, CancellationToken ct)
    {
        var d = await DispatchAsync(env, token, GibPortalProtocol.ShowHtml, GibPortalProtocol.PageDrafts,
            new Dictionary<string, string> { ["ettn"] = ettn, ["onayDurumu"] = approved ? GibPortalProtocol.Approved : GibPortalProtocol.NotApproved }, ct);
        return d.ValueKind == JsonValueKind.String ? d.GetString()! : d.ToString();
    }

    public Task DeleteDraftsAsync(GibPortalEnvironment env, string token, IReadOnlyList<GibPortalDocumentRow> rows, string reason, CancellationToken ct) =>
        DispatchAsync(env, token, GibPortalProtocol.DeleteDrafts, GibPortalProtocol.PageDrafts,
            new Dictionary<string, object> { ["silinecekler"] = rows.Select(r => r.Raw).ToArray(), ["aciklama"] = reason }, ct);

    public async Task<string> CreateCancellationRequestAsync(GibPortalEnvironment env, string token, string ettn, string reason, CancellationToken ct)
    {
        var d = await DispatchAsync(env, token, GibPortalProtocol.CancellationRequest, GibPortalProtocol.PageDocuments, new Dictionary<string, string>
        {
            ["ettn"] = ettn,
            ["onayDurumu"] = GibPortalProtocol.Approved,
            ["belgeTuru"] = "FATURA",
            ["talepAciklama"] = reason,
        }, ct);
        return d.ValueKind == JsonValueKind.String ? d.GetString()! : d.ToString();
    }

    public async Task<string> CreateObjectionAsync(GibPortalEnvironment env, string token, string ettn, string method, string referenceId, DateOnly referenceDate, string reason, CancellationToken ct)
    {
        var d = await DispatchAsync(env, token, GibPortalProtocol.ObjectionRequest, GibPortalProtocol.PageDocuments, new Dictionary<string, string>
        {
            ["ettn"] = ettn,
            ["onayDurumu"] = GibPortalProtocol.Approved,
            ["belgeTuru"] = "FATURA",
            ["itirazYontemi"] = method,
            ["referansBelgeId"] = referenceId,
            ["referansBelgeTarihi"] = PortalDate(referenceDate),
            ["talepAciklama"] = reason,
        }, ct);
        return d.ValueKind == JsonValueKind.String ? d.GetString()! : d.ToString();
    }

    public async Task<byte[]> DownloadZipAsync(GibPortalEnvironment env, string token, string ettn, CancellationToken ct)
    {
        var client = Create(env);
        var url = $"{GibPortalProtocol.DownloadPath}?token={Uri.EscapeDataString(token)}&ettn={Uri.EscapeDataString(ettn)}&belgeTip=FATURA"
            + $"&onayDurumu={Uri.EscapeDataString(GibPortalProtocol.Approved)}&cmd={GibPortalProtocol.DownloadCommand}";
        using var response = await client.GetAsync(url, ct);
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        if (!response.IsSuccessStatusCode || bytes.Length < 4 || bytes[0] != 'P' || bytes[1] != 'K')
        {
            var text = Encoding.UTF8.GetString(bytes);
            throw new GibPortalException("GİB portalından belge indirilemedi: " + (text.Length > 300 ? text[..300] : text), IsExpired(text));
        }

        return bytes;
    }

    private static string? NonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    public static string PortalDate(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private HttpClient Create(GibPortalEnvironment environment)
    {
        var client = httpFactory.CreateClient(HttpClientName);
        client.BaseAddress = BaseUri(environment);
        client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
        client.DefaultRequestHeaders.Referrer = new Uri(client.BaseAddress, GibPortalProtocol.RefererPath);
        return client;
    }

    private async Task<JsonElement> PostAsync(GibPortalEnvironment environment, string path, Dictionary<string, string> form, CancellationToken ct)
    {
        var client = Create(environment);
        using var content = new FormUrlEncodedContent(form);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded") { CharSet = "UTF-8" };
        HttpResponseMessage response;
        string body;
        try
        {
            response = await client.PostAsync(path, content, ct);
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            throw new GibPortalException($"GİB portalına ulaşılamadı ({client.BaseAddress?.Host}): {ex.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new GibPortalException($"GİB portalı {options.RequestTimeoutSeconds} saniyede yanıt vermedi ({client.BaseAddress?.Host}).");
        }

        using var _ = response;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            throw new GibPortalException($"GİB portalından JSON olmayan yanıt (HTTP {(int)response.StatusCode}).");
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var err) && err.ToString() == "1")
            {
                var (message, expired) = Messages(root);
                throw new GibPortalException("GİB portalı: " + message, expired);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new GibPortalException($"GİB portalı HTTP {(int)response.StatusCode} döndü.");
            }

            return root.Clone();
        }
    }

    private static (string Message, bool Expired) Messages(JsonElement root)
    {
        if (!root.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array)
        {
            return ("bilinmeyen hata", false);
        }

        var texts = new List<string>();
        var expired = false;
        foreach (var m in messages.EnumerateArray())
        {
            if (m.ValueKind == JsonValueKind.String)
            {
                texts.Add(m.GetString()!);
            }
            else if (m.ValueKind == JsonValueKind.Object)
            {
                texts.Add(Str(m, "text") ?? m.ToString());
                expired |= Str(m, "type") == "4";
            }
        }

        var joined = string.Join(" ", texts);
        return (joined, expired || IsExpired(joined));
    }

    private static bool IsExpired(string text) =>
        text.Contains("zamanaşım", StringComparison.OrdinalIgnoreCase)
        || text.Contains("zaman aşım", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Oturum geçersiz", StringComparison.OrdinalIgnoreCase);

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v)
            ? v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.GetRawText(),
                _ => null,
            }
            : null;
}
