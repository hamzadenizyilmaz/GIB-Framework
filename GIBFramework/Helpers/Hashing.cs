using System.Security.Cryptography;
using System.Text;

namespace GIBFramework.Helpers;

public static class Hashing
{
    public static string Sha256Hex(ReadOnlySpan<byte> data) => Convert.ToHexStringLower(SHA256.HashData(data));

    public static string Sha256Hex(string text) => Sha256Hex(Encoding.UTF8.GetBytes(text));

    public static async Task<string> Sha256HexAsync(Stream stream, CancellationToken cancellationToken = default) =>
        Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
}
