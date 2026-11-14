using System.Net.Http;

namespace GIBFramework.Infrastructure;

internal static class BoundedHttpContent
{
    public static async Task<string> ReadAsync(HttpContent content, int maxBytes, CancellationToken ct)
    {
        if (content.Headers.ContentLength > maxBytes)
            throw new InvalidDataException("HTTP yanıtı boyut sınırını aştı.");

        await using var stream = await content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[32 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > maxBytes)
                throw new InvalidDataException("HTTP yanıtı boyut sınırını aştı.");
            buffer.Write(chunk, 0, read);
        }

        buffer.Position = 0;
        using var reader = new StreamReader(buffer);
        return await reader.ReadToEndAsync(ct);
    }
}
