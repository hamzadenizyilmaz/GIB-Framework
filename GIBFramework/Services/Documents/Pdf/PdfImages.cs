using System.Buffers.Binary;
using System.IO.Compression;

namespace GIBFramework.Services.Documents.Pdf;

public static class PdfImages
{
    public static PdfImage? Load(byte[] data, string contentType)
    {
        ArgumentNullException.ThrowIfNull(data);
        try
        {
            return contentType switch
            {
                "image/jpeg" => Jpeg(data),
                "image/png" => Png(data),
                _ => null,
            };
        }
        catch (Exception ex) when (ex is InvalidDataException or IndexOutOfRangeException or ArgumentException or OverflowException)
        {
            return null;
        }
    }

    private static PdfImage? Jpeg(byte[] d)
    {
        var i = 2;
        while (i + 9 < d.Length)
        {
            if (d[i] != 0xFF)
            {
                return null;
            }

            var marker = d[i + 1];
            var length = (d[i + 2] << 8) | d[i + 3];
            if (marker is 0xC0 or 0xC1 or 0xC2)
            {
                var height = (d[i + 5] << 8) | d[i + 6];
                var width = (d[i + 7] << 8) | d[i + 8];
                var components = d[i + 9];
                var space = components switch { 1 => "DeviceGray", 4 => "DeviceCMYK", _ => "DeviceRGB" };
                return width > 0 && height > 0 ? new PdfImage(width, height, space, "DCTDecode", d) : null;
            }

            i += 2 + length;
        }

        return null;
    }

    private static PdfImage? Png(byte[] d)
    {
        if (d.Length < 33 || d[0] != 0x89 || d[1] != 0x50)
        {
            return null;
        }

        var pos = 8;
        int width = 0, height = 0, depth = 0, colorType = 0, interlace = 0;
        byte[]? palette = null;
        byte[]? trns = null;
        using var idat = new MemoryStream();
        while (pos + 8 <= d.Length)
        {
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(pos));
            var type = System.Text.Encoding.ASCII.GetString(d, pos + 4, 4);
            var chunk = d.AsSpan(pos + 8, length);
            switch (type)
            {
                case "IHDR":
                    width = (int)BinaryPrimitives.ReadUInt32BigEndian(chunk);
                    height = (int)BinaryPrimitives.ReadUInt32BigEndian(chunk[4..]);
                    depth = chunk[8];
                    colorType = chunk[9];
                    interlace = chunk[12];
                    break;
                case "PLTE":
                    palette = chunk.ToArray();
                    break;
                case "tRNS":
                    trns = chunk.ToArray();
                    break;
                case "IDAT":
                    idat.Write(chunk);
                    break;
            }

            if (type == "IEND")
            {
                break;
            }

            pos += 12 + length;
        }

        if (width <= 0 || height <= 0 || width > 4000 || height > 4000 || interlace != 0)
        {
            return null;
        }

        var channels = colorType switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => 0 };
        if (channels == 0 || (colorType != 3 && depth != 8) || (colorType == 3 && depth is not (1 or 2 or 4 or 8)))
        {
            return null;
        }

        var bitsPerPixel = channels * depth;
        var stride = ((width * bitsPerPixel) + 7) / 8;
        var bpp = Math.Max(1, bitsPerPixel / 8);
        var raw = Inflate(idat.ToArray());
        if (raw.Length < (stride + 1) * height)
        {
            return null;
        }

        var pixels = new byte[stride * height];
        var previous = new byte[stride];
        for (var y = 0; y < height; y++)
        {
            var filter = raw[y * (stride + 1)];
            var line = raw.AsSpan((y * (stride + 1)) + 1, stride);
            var current = pixels.AsSpan(y * stride, stride);
            for (var x = 0; x < stride; x++)
            {
                var a = x >= bpp ? current[x - bpp] : 0;
                var b = previous[x];
                var c = x >= bpp ? previous[x - bpp] : 0;
                var value = line[x];
                current[x] = filter switch
                {
                    0 => value,
                    1 => (byte)(value + a),
                    2 => (byte)(value + b),
                    3 => (byte)(value + ((a + b) / 2)),
                    4 => (byte)(value + Paeth(a, b, c)),
                    _ => throw new InvalidDataException("PNG filtresi desteklenmiyor."),
                };
            }

            current.CopyTo(previous);
        }

        var rgb = new byte[width * height * 3];
        var alpha = new byte[width * height];
        var hasAlpha = false;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                byte r, g, bl, al = 255;
                var row = y * stride;
                switch (colorType)
                {
                    case 0:
                        r = g = bl = pixels[row + x];
                        break;
                    case 2:
                        r = pixels[row + (x * 3)];
                        g = pixels[row + (x * 3) + 1];
                        bl = pixels[row + (x * 3) + 2];
                        break;
                    case 4:
                        r = g = bl = pixels[row + (x * 2)];
                        al = pixels[row + (x * 2) + 1];
                        break;
                    case 6:
                        r = pixels[row + (x * 4)];
                        g = pixels[row + (x * 4) + 1];
                        bl = pixels[row + (x * 4) + 2];
                        al = pixels[row + (x * 4) + 3];
                        break;
                    default:
                        var bit = x * depth;
                        var index = (pixels[row + (bit / 8)] >> (8 - depth - (bit % 8))) & ((1 << depth) - 1);
                        if (palette is null || (index * 3) + 2 >= palette.Length)
                        {
                            return null;
                        }

                        r = palette[index * 3];
                        g = palette[(index * 3) + 1];
                        bl = palette[(index * 3) + 2];
                        if (trns is not null && index < trns.Length)
                        {
                            al = trns[index];
                        }

                        break;
                }

                var p = (y * width) + x;
                rgb[p * 3] = r;
                rgb[(p * 3) + 1] = g;
                rgb[(p * 3) + 2] = bl;
                alpha[p] = al;
                hasAlpha |= al != 255;
            }
        }

        return new PdfImage(width, height, "DeviceRGB", "FlateDecode", PdfBuilder.Deflate(rgb), hasAlpha ? PdfBuilder.Deflate(alpha) : null);
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static byte[] Inflate(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var z = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        z.CopyTo(output);
        return output.ToArray();
    }
}
