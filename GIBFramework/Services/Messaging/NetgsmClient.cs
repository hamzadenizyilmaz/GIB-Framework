using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GIBFramework.Services.Messaging;

public sealed record NetgsmCredentials(string UserCode, string Password, string? Header, string Encoding);

public sealed record NetgsmBalanceItem(string Name, string Amount);

public sealed class NetgsmException(string code, string message, bool transient = false) : Exception(message)
{
    public string Code { get; } = code;

    public bool Transient { get; } = transient;
}

public sealed class NetgsmClient(IHttpClientFactory httpFactory, MessagingOptions options)
{
    public const string HttpClientName = "Netgsm";

    private static readonly Dictionary<string, string> Errors = new(StringComparer.Ordinal)
    {
        ["20"] = "Mesaj metni hatalı veya azami karakter sayısını aşıyor.",
        ["30"] = "Geçersiz kullanıcı adı / şifre, API erişim izni yok ya da IP kısıtlamasına takıldı (Netgsm: Abonelik İşlemleri / API işlemleri).",
        ["40"] = "Mesaj başlığı (gönderici adı) Netgsm hesabında tanımlı değil.",
        ["50"] = "Abone hesabıyla İYS kontrollü gönderim yapılamıyor.",
        ["51"] = "Aboneliğe tanımlı İYS marka bilgisi bulunamadı.",
        ["60"] = "Hesapta tanımlı paket veya kampanya yok.",
        ["70"] = "Hatalı sorgulama: parametrelerden biri hatalı veya eksik.",
        ["80"] = "Gönderim sınırı aşıldı.",
        ["85"] = "Mükerrer gönderim sınırı aşıldı (aynı numaraya 1 dakikada 20'den fazla görev).",
        ["100"] = "Netgsm sistem hatası.",
        ["101"] = "Netgsm sistem hatası.",
    };

    public static string? NormalizePhone(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var trimmed = input.Trim();
        var digits = new string([.. trimmed.Where(char.IsAsciiDigit)]);
        if (trimmed.StartsWith('+') && !digits.StartsWith("90", StringComparison.Ordinal))
        {
            return digits.Length >= 8 ? "00" + digits : null;
        }

        if (digits.StartsWith("00", StringComparison.Ordinal) && !digits.StartsWith("0090", StringComparison.Ordinal))
        {
            return digits.Length >= 10 ? digits : null;
        }

        if (digits.StartsWith("0090", StringComparison.Ordinal))
        {
            digits = digits[4..];
        }
        else if (digits.Length == 12 && digits.StartsWith("90", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }
        else if (digits.Length == 11 && digits.StartsWith('0'))
        {
            digits = digits[1..];
        }

        return digits.Length == 10 && digits[0] is '5' or '8' or '2' or '3' or '4' ? digits : null;
    }

    public async Task<string> SendAsync(NetgsmCredentials credentials, string phone, string text, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        if (string.IsNullOrWhiteSpace(credentials.Header))
        {
            throw new NetgsmException("HEADER", "SMS başlığı (gönderici adı) seçilmemiş.");
        }

        var body = new SendRequest(credentials.Header, [new SendMessage(text, phone)], credentials.Encoding, "0", "GIBFramework");
        using var request = new HttpRequestMessage(HttpMethod.Post, Url("/sms/rest/v2/send"))
        {
            Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json"),
        };
        Authorize(request, credentials);
        using var doc = await SendCoreAsync<JsonDocument>(request, ct);
        var code = Prop(doc.RootElement, "code");
        var job = Prop(doc.RootElement, "jobid");
        if (code == "00" && !string.IsNullOrWhiteSpace(job))
        {
            return job;
        }

        throw Fail(code, Prop(doc.RootElement, "description"));
    }

    public async Task<IReadOnlyList<string>> HeadersAsync(NetgsmCredentials credentials, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        using var request = new HttpRequestMessage(HttpMethod.Get, Url("/sms/rest/v2/msgheader"));
        Authorize(request, credentials);
        using var doc = await SendCoreAsync<JsonDocument>(request, ct);
        var code = Prop(doc.RootElement, "code");
        if (code == "00")
        {
            return doc.RootElement.TryGetProperty("msgheaders", out var list) && list.ValueKind == JsonValueKind.Array
                ? [.. list.EnumerateArray().Select(h => h.ToString())]
                : [];
        }

        throw Fail(code, Prop(doc.RootElement, "description"));
    }

    public async Task<IReadOnlyList<NetgsmBalanceItem>> BalanceAsync(NetgsmCredentials credentials, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        using var request = new HttpRequestMessage(HttpMethod.Post, Url("/balance"))
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { usercode = credentials.UserCode, password = credentials.Password, stip = 3 }),
                Encoding.UTF8,
                "application/json"),
        };
        using var doc = await SendCoreAsync<JsonDocument>(request, ct);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("balance", out var balance) && balance.ValueKind == JsonValueKind.Array)
        {
            return [.. balance.EnumerateArray().Select(b => new NetgsmBalanceItem(
                b.TryGetProperty("balance_name", out var n) ? n.ToString() : "Bakiye",
                b.TryGetProperty("amount", out var a) ? a.ToString() : "0"))];
        }

        throw Fail(Prop(root, "code") ?? "?", null);
    }

    private static NetgsmException Fail(string? code, string? description)
    {
        code ??= "?";
        var known = Errors.TryGetValue(code, out var text) ? text : $"Netgsm hata kodu {code}{(string.IsNullOrWhiteSpace(description) ? string.Empty : ": " + description)}";
        return new NetgsmException(code, known, transient: code is "80" or "85" or "100" or "101");
    }

    private async Task<T> SendCoreAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        var client = httpFactory.CreateClient(HttpClientName);
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new NetgsmException("NETWORK", "Netgsm'e bağlanılamadı: " + ex.Message, transient: true);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new NetgsmException("TIMEOUT", "Netgsm zamanında yanıt vermedi.", transient: true);
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            if ((int)response.StatusCode >= 500)
            {
                throw new NetgsmException("HTTP" + (int)response.StatusCode, $"Netgsm HTTP {(int)response.StatusCode} döndürdü.", transient: true);
            }

            try
            {
                return JsonSerializer.Deserialize<T>(text, Json) ?? throw new JsonException("Boş yanıt");
            }
            catch (JsonException)
            {
                var code = text.Trim().Split(' ', 2)[0];
                throw Fail(code.Length is > 0 and <= 3 ? code : "?", code.Length > 3 ? text.Trim()[..Math.Min(200, text.Trim().Length)] : null);
            }
        }
    }

    private static string? Prop(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : null;

    private Uri Url(string path) => new(options.NetgsmBaseUrl.TrimEnd('/') + path);

    private static void Authorize(HttpRequestMessage request, NetgsmCredentials credentials) =>
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.UserCode}:{credentials.Password}")));

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private sealed record SendMessage([property: JsonPropertyName("msg")] string Msg, [property: JsonPropertyName("no")] string No);

    private sealed record SendRequest(
        [property: JsonPropertyName("msgheader")] string MsgHeader,
        [property: JsonPropertyName("messages")] IReadOnlyList<SendMessage> Messages,
        [property: JsonPropertyName("encoding")] string Encoding,
        [property: JsonPropertyName("iysfilter")] string IysFilter,
        [property: JsonPropertyName("appname")] string AppName);
}
