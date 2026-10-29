using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Odisea.Api;

/// Minimal PNG encoder for demo and test pictures: a vertical two-colour gradient, no dependencies.
public static class DemoImages
{
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static byte[] Png(int width, int height, (byte R, byte G, byte B) top, (byte R, byte G, byte B) bottom)
    {
        var stride = 1 + width * 3;
        var raw = new byte[height * stride];
        for (var y = 0; y < height; y++)
        {
            var t = height == 1 ? 0 : (double)y / (height - 1);
            var r = (byte)(top.R + (bottom.R - top.R) * t);
            var g = (byte)(top.G + (bottom.G - top.G) * t);
            var b = (byte)(top.B + (bottom.B - top.B) * t);
            var pos = y * stride;
            raw[pos++] = 0; // filter type: none
            for (var x = 0; x < width; x++)
            {
                raw[pos++] = r;
                raw[pos++] = g;
                raw[pos++] = b;
            }
        }

        using var idat = new MemoryStream();
        using (var zlib = new ZLibStream(idat, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(raw);

        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8; // bit depth
        ihdr[9] = 2; // colour type: RGB

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(png, "IHDR", ihdr);
        WriteChunk(png, "IDAT", idat.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        stream.Write(typeBytes);
        stream.Write(data);

        var crc = 0xFFFFFFFFu;
        foreach (var b in typeBytes)
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (var b in data)
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc ^ 0xFFFFFFFFu);
        stream.Write(crcBytes);
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
