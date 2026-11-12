using System.Net;
using System.Net.Sockets;

namespace GIBFramework.Infrastructure;

public sealed class OutboundNetwork(OutboundNetworkOptions options, IHostEnvironment environment)
{
    public bool AllowsWebhook(string host) => Allows(host, options.WebhookHosts);

    public bool AllowsSmtp(string host) => Allows(host, options.SmtpHosts);

    public async Task ConnectWebhookAsync(Socket socket, DnsEndPoint endpoint, CancellationToken ct) =>
        await ConnectAsync(socket, endpoint.Host, endpoint.Port, AllowsWebhook(endpoint.Host), ct);

    public async Task ConnectSmtpAsync(Socket socket, string host, int port, CancellationToken ct) =>
        await ConnectAsync(socket, host, port, AllowsSmtp(host), ct);

    private bool Allows(string host, string[] allowed) =>
        environment.IsDevelopment() || allowed.Any(entry => string.Equals(entry.Trim().TrimEnd('.'), host.TrimEnd('.'), StringComparison.OrdinalIgnoreCase));

    private async Task ConnectAsync(Socket socket, string host, int port, bool allowed, CancellationToken ct)
    {
        if (!allowed)
        {
            throw new InvalidOperationException("Hedef sunucu izin verilen adresler arasında değil.");
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
