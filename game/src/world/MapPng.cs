using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

/// Reads the map layers' PNGs without Godot, so the world query also runs in a headless replay (D-010).
/// Supports what the map format allows: 8-bit greyscale or RGBA, non-interlaced.
public static class MapPng
{
    /// Returns the pixels row-major (row 0 first) with `channels` bytes per pixel.
    public static byte[] Read(string path, int channels, out int width, out int height)
    {
        byte[] file = File.ReadAllBytes(path);
        if (file.Length < 8 || BinaryPrimitives.ReadUInt64BigEndian(file) != 0x89504E470D0A1A0AUL)
            throw new InvalidDataException($"{path}: not a PNG");
        width = height = 0;
        var idat = new MemoryStream();
        for (int at = 8; at + 8 <= file.Length;)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(file.AsSpan(at));
            string type = System.Text.Encoding.ASCII.GetString(file, at + 4, 4);
            var body = file.AsSpan(at + 8, length);
            if (type == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(body);
                height = BinaryPrimitives.ReadInt32BigEndian(body[4..]);
                int colorType = channels == 1 ? 0 : channels == 4 ? 6 : -1;
                if (body[8] != 8 || body[9] != colorType || body[12] != 0)
                    throw new InvalidDataException($"{path}: must be 8-bit {(channels == 1 ? "greyscale" : "RGBA")}, non-interlaced");
            }
            else if (type == "IDAT")
            {
                idat.Write(body);
            }
            else if (type == "IEND")
            {
                break;
            }
            at += 12 + length;
        }
        int stride = width * channels;
        var raw = new byte[height * (stride + 1)];
        idat.Position = 0;
        using (var inflate = new ZLibStream(idat, CompressionMode.Decompress))
            inflate.ReadExactly(raw);

        var pixels = new byte[height * stride];
        for (int r = 0; r < height; r++)
        {
            byte filter = raw[r * (stride + 1)];
            var line = raw.AsSpan(r * (stride + 1) + 1, stride);
            var output = pixels.AsSpan(r * stride, stride);
            var above = r > 0 ? pixels.AsSpan((r - 1) * stride, stride) : new byte[stride];
            for (int i = 0; i < stride; i++)
            {
                int left = i >= channels ? output[i - channels] : 0;
                int up = above[i];
                int upLeft = i >= channels ? above[i - channels] : 0;
                output[i] = (byte)(line[i] + filter switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) >> 1,
                    4 => Paeth(left, up, upLeft),
                    _ => throw new InvalidDataException($"{path}: unknown PNG filter {filter} on row {r}"),
                });
            }
        }
        return pixels;
    }

    static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
