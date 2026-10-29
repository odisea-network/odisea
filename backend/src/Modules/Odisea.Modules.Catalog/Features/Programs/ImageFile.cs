using System.Buffers.Binary;
using Odisea.Modules.Catalog.Domain;

namespace Odisea.Modules.Catalog.Features.Programs;

/// Recognises the image formats we serve from their bytes, not from what the client claims, and
/// reads the pixel size from the header. Anything else is refused: no SVG (scriptable), no GIF.
public static class ImageFile
{
    public const string Png = "image/png";
    public const string Jpeg = "image/jpeg";
    public const string WebP = "image/webp";

    public static readonly IReadOnlyList<string> ContentTypes = [Png, Jpeg, WebP];

    public readonly record struct Info(string ContentType, int Width, int Height);

    public static Info Inspect(byte[] bytes)
    {
        if (bytes.Length == 0)
            throw new ProgramValidationException("Upload an image file in the 'file' form field.");
        if (bytes.Length > ProgramImage.MaxBytes)
            throw new ProgramValidationException($"The image exceeds {ProgramImage.MaxBytes / 1024 / 1024} MB.");

        var info = ReadPng(bytes) ?? ReadJpeg(bytes) ?? ReadWebP(bytes)
            ?? throw new ProgramValidationException("Only JPEG, PNG and WebP images are accepted.");
        if (info.Width <= 0 || info.Height <= 0)
            throw new ProgramValidationException("The image header is damaged.");
        return info;
    }

    private static Info? ReadPng(ReadOnlySpan<byte> b)
    {
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (b.Length < 24 || !b[..8].SequenceEqual(signature) || !b[12..16].SequenceEqual("IHDR"u8))
            return null;
        return new Info(Png,
            (int)BinaryPrimitives.ReadUInt32BigEndian(b[16..20]),
            (int)BinaryPrimitives.ReadUInt32BigEndian(b[20..24]));
    }

    private static Info? ReadJpeg(ReadOnlySpan<byte> b)
    {
        if (b.Length < 4 || b[0] != 0xFF || b[1] != 0xD8)
            return null;

        // Walk the marker segments until a start-of-frame, which carries the dimensions.
        var pos = 2;
        while (pos + 4 <= b.Length)
        {
            if (b[pos] != 0xFF)
                return null;
            var marker = b[pos + 1];
            if (marker == 0xFF)
            {
                pos++;
                continue;
            }
            if (marker is 0xD8 or 0x01 or >= 0xD0 and <= 0xD7)
            {
                pos += 2;
                continue;
            }
            var length = BinaryPrimitives.ReadUInt16BigEndian(b[(pos + 2)..(pos + 4)]);
            var isSof = marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC;
            if (isSof)
            {
                if (pos + 9 > b.Length)
                    return null;
                return new Info(Jpeg,
                    BinaryPrimitives.ReadUInt16BigEndian(b[(pos + 7)..(pos + 9)]),
                    BinaryPrimitives.ReadUInt16BigEndian(b[(pos + 5)..(pos + 7)]));
            }
            if (marker == 0xD9 || marker == 0xDA)
                return null;
            pos += 2 + length;
        }
        return null;
    }

    private static Info? ReadWebP(ReadOnlySpan<byte> b)
    {
        if (b.Length < 30 || !b[..4].SequenceEqual("RIFF"u8) || !b[8..12].SequenceEqual("WEBP"u8))
            return null;

        var chunk = b[12..16];
        if (chunk.SequenceEqual("VP8 "u8))
        {
            // Lossy: 3-byte frame tag, 3-byte start code, then 14-bit width and height.
            if (b[23] != 0x9D || b[24] != 0x01 || b[25] != 0x2A)
                return null;
            return new Info(WebP,
                BinaryPrimitives.ReadUInt16LittleEndian(b[26..28]) & 0x3FFF,
                BinaryPrimitives.ReadUInt16LittleEndian(b[28..30]) & 0x3FFF);
        }
        if (chunk.SequenceEqual("VP8L"u8))
        {
            // Lossless: signature byte, then 14 bits (width - 1) and 14 bits (height - 1).
            if (b[20] != 0x2F)
                return null;
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(b[21..25]);
            return new Info(WebP, (int)(bits & 0x3FFF) + 1, (int)((bits >> 14) & 0x3FFF) + 1);
        }
        if (chunk.SequenceEqual("VP8X"u8))
        {
            // Extended: 24-bit canvas width - 1 and height - 1 after the flags.
            return new Info(WebP,
                (b[24] | b[25] << 8 | b[26] << 16) + 1,
                (b[27] | b[28] << 8 | b[29] << 16) + 1);
        }
        return null;
    }
}
