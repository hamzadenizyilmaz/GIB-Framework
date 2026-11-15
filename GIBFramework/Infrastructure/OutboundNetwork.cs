using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GIBFramework.Infrastructure;

public sealed record OutboundNetworkSettings(string[] WebhookHosts, string[] SmtpHosts);

/// <summary>
/// Panelin dışarıya bağlanabileceği hedefler (bildirim webhook'ları ve SMTP sunucuları).
/// Başlangıç değerleri OutboundNetwork:* yapılandırmasından gelir; Ayarlar › Dış bağlantı izinleri sayfasından
/// kaydedilen liste App_Data/outbound-network.json dosyasında saklanır ve yapılandırmanın yerine geçer.
/// </summary>
public sealed partial class OutboundNetwork
{
    private const int MaxHosts = 200;
    private readonly IHostEnvironment environment;
    private readonly string path;
    private readonly object gate = new();
    private volatile OutboundNetworkSettings current;

    public OutboundNetwork(OutboundNetworkOptions options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        this.environment = environment;
        path = Path.Combine(environment.ContentRootPath, "App_Data", "outbound-network.json");
        current = Load(path) ?? Normalize(new OutboundNetworkSettings(options.WebhookHosts ?? [], options.SmtpHosts ?? []));
    }

    public OutboundNetworkSettings Settings
    {
        get
        {
            // Birden fazla uygulama süreci aynı dosyayı paylaşabilir; değişiklik diğer süreçlere de yansısın.
            if (File.Exists(path) && File.GetLastWriteTimeUtc(path) > loadedAt)
            {
                lock (gate)
                {
                    if (Load(path) is { } fresh) current = fresh;
                }
            }
            return current;
        }
    }

    private DateTime loadedAt = DateTime.MinValue;

    public bool AllowsWebhook(string host) => Allows(host, Settings.WebhookHosts);

    public bool AllowsSmtp(string host) => Allows(host, Settings.SmtpHosts);

    public OutboundNetworkSettings Save(OutboundNetworkSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var next = Normalize(settings);
        lock (gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var staging = path + ".tmp";
            try
            {
                File.WriteAllText(staging, JsonSerializer.Serialize(next));
                File.Move(staging, path, overwrite: true);
                current = next;
                loadedAt = File.GetLastWriteTimeUtc(path);
            }
            finally
            {
                File.Delete(staging);
            }
        }
        return next;
    }

    public async Task ConnectWebhookAsync(Socket socket, DnsEndPoint endpoint, CancellationToken ct) =>
        await ConnectAsync(socket, endpoint.Host, endpoint.Port, AllowsWebhook(endpoint.Host), ct);

    public async Task ConnectSmtpAsync(Socket socket, string host, int port, CancellationToken ct) =>
        await ConnectAsync(socket, host, port, AllowsSmtp(host), ct);

    private bool Allows(string host, string[] allowed) =>
        environment.IsDevelopment() || allowed.Any(entry => string.Equals(entry, host.Trim().TrimEnd('.'), StringComparison.OrdinalIgnoreCase));

    private OutboundNetworkSettings? Load(string file)
    {
        if (!File.Exists(file)) return null;
        var settings = JsonSerializer.Deserialize<OutboundNetworkSettings>(File.ReadAllText(file))
            ?? throw new InvalidOperationException("Dış bağlantı izinleri okunamadı.");
        loadedAt = File.GetLastWriteTimeUtc(file);
        return Normalize(settings);
    }

    private static OutboundNetworkSettings Normalize(OutboundNetworkSettings settings)
    {
        static string[] Clean(string[]? hosts)
        {
            var list = (hosts ?? []).Select(h => (h ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant())
                .Where(h => h.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
            if (list.Length > MaxHosts) throw new InvalidOperationException($"En fazla {MaxHosts} sunucu adı girilebilir.");
            foreach (var host in list)
            {
                if (host.Length > 253 || !HostName().IsMatch(host))
                    throw new InvalidOperationException($"\"{host}\" geçerli bir sunucu adı değil. Yalnızca alan adı yazın (ör. dash.ornek.com); https:// veya yol eklemeyin.");
            }
            return list;
        }

        return new OutboundNetworkSettings(Clean(settings.WebhookHosts), Clean(settings.SmtpHosts));
    }

    [GeneratedRegex(@"^(?=.{1,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z0-9-]{2,63}$|^xn--[a-z0-9.-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex HostName();

    private async Task ConnectAsync(Socket socket, string host, int port, bool allowed, CancellationToken ct)
    {
        if (!allowed)
        {
            throw new InvalidOperationException("Hedef sunucu izin verilen adresler arasında değil. Platform yöneticisi Ayarlar › Güvenlik › Dış bağlantı izinleri sayfasından ekleyebilir.");
        }

        var addresses = await Dns.GetHostAddressesAsync(host, ct);
        if (addresses.Length == 0 || (!environment.IsDevelopment() && addresses.Any(address => !IsPublicAddress(address))))
        {
            throw new InvalidOperationException("Hedef sunucu genel ağ adresine çözümlenemedi.");
        }

        await socket.ConnectAsync(addresses, port, ct);
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var b = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return b[0] is > 0 and < 224 and not (10 or 127)
                && !(b[0] == 100 && b[1] is >= 64 and <= 127)
                && !(b[0] == 169 && b[1] == 254)
                && !(b[0] == 172 && b[1] is >= 16 and <= 31)
                && !(b[0] == 192 && (b[1] == 168 || (b[1] == 0 && b[2] is 0 or 2)))
                && !(b[0] == 198 && (b[1] is 18 or 19 || (b[1] == 51 && b[2] == 100)))
                && !(b[0] == 203 && b[1] == 0 && b[2] == 113);
        }

        return address.AddressFamily == AddressFamily.InterNetworkV6 && (b[0] & 0xe0) == 0x20
            && !(b[0] == 0x20 && (b[1] == 2 || (b[1] == 1 && ((b[2] == 0 && b[3] == 0) || (b[2] == 0x0d && b[3] == 0xb8)))));
    }
}
