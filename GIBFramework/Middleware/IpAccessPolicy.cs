using System.Net;
using System.Collections.Concurrent;
using System.Text.Json;

namespace GIBFramework.Middleware;

public sealed record IpAccessSettings(bool Enabled, string[] Allow, string[] Deny);

public sealed class IpAccessPolicy
{
    private readonly string path;
    private readonly string tenantDirectory;
    private readonly object gate = new();
    private volatile Snapshot current;
    private readonly ConcurrentDictionary<Guid, Snapshot> tenants = new();
    private static readonly Snapshot Disabled = Compile(new IpAccessSettings(false, [], []));

    public IpAccessPolicy(IConfiguration config, IHostEnvironment host)
    {
        path = Path.Combine(host.ContentRootPath, "App_Data", "ip-access.json");
        tenantDirectory = Path.Combine(host.ContentRootPath, "App_Data", "tenant-ip-access");
        var allow = config.GetSection("IpAccess:Allow").Get<string[]>() ?? [];
        var deny = config.GetSection("IpAccess:Deny").Get<string[]>() ?? [];
        var defaults = new IpAccessSettings(allow.Length + deny.Length > 0, allow, deny);
        current = Load(path) ?? Compile(defaults);
    }

    public IpAccessSettings GetSettings(Guid? tenantId) => GetSnapshot(tenantId).Settings;
    public bool Allows(IPAddress? address, Guid? tenantId = null) => GetSnapshot(tenantId).Allows(address);

    public IpAccessSettings Save(Guid? tenantId, IpAccessSettings settings, IPAddress? caller)
    {
        var next = Compile(settings);
        if (!next.Allows(caller)) throw new InvalidOperationException("Bu ayar mevcut IP adresinizin erişimini kapatır.");
        lock (gate)
        {
            var destination = tenantId is { } id ? TenantPath(id) : path;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            var staging = destination + ".tmp";
            try
            {
                File.WriteAllText(staging, JsonSerializer.Serialize(next.Settings));
                File.Move(staging, destination, overwrite: true);
                if (tenantId is { } tenant) tenants[tenant] = next;
                else current = next;
            }
            finally
            {
                File.Delete(staging);
            }
        }
        return next.Settings;
    }

    private Snapshot GetSnapshot(Guid? tenantId) =>
        tenantId is { } id ? tenants.GetOrAdd(id, key => Load(TenantPath(key)) ?? Disabled) : current;

    private string TenantPath(Guid tenantId) => Path.Combine(tenantDirectory, $"{tenantId:N}.json");

    private static Snapshot? Load(string file) => File.Exists(file)
        ? Compile(JsonSerializer.Deserialize<IpAccessSettings>(File.ReadAllText(file)) ?? throw new InvalidOperationException("IP ayarları okunamadı."))
        : null;

    private static Snapshot Compile(IpAccessSettings settings)
    {
        if (settings.Allow is null || settings.Deny is null || settings.Allow.Length + settings.Deny.Length > 256)
            throw new InvalidOperationException("En fazla 256 IP adresi veya ağ girilebilir.");
        var normalized = settings with
        {
            Allow = settings.Allow.Select(value => value?.Trim() ?? "").Distinct().ToArray(),
            Deny = settings.Deny.Select(value => value?.Trim() ?? "").Distinct().ToArray(),
        };
        return new Snapshot(normalized, normalized.Allow.Select(Parse).ToArray(), normalized.Deny.Select(Parse).ToArray());
    }

    private static System.Net.IPNetwork Parse(string value)
    {
        if (System.Net.IPNetwork.TryParse(value, out var network)) return network;
        if (!IPAddress.TryParse(value, out var address))
            throw new InvalidOperationException("IP adresi veya CIDR aralığı geçersiz.");
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        return new System.Net.IPNetwork(address, address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128);
    }

    private sealed record Snapshot(IpAccessSettings Settings, System.Net.IPNetwork[] Allow, System.Net.IPNetwork[] Deny)
    {
        public bool Allows(IPAddress? address)
        {
            if (!Settings.Enabled) return true;
            if (address?.IsIPv4MappedToIPv6 == true) address = address.MapToIPv4();
            if (address is null) return Allow.Length + Deny.Length == 0;
            return !Deny.Any(network => network.Contains(address)) && (Allow.Length == 0 || Allow.Any(network => network.Contains(address)));
        }
    }
}
