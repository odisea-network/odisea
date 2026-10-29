using System.Buffers.Binary;
using System.Text;
using Odisea.Modules.Catalog.Domain;
using Odisea.Modules.Catalog.Features.Programs;
using Xunit;

namespace Odisea.UnitTests.Catalog;

/// Headers only: the inspector reads dimensions from the first bytes and never decodes pixels.
public class ImageFileTests
{
    public static byte[] PngHeader(int width, int height)
    {
        var bytes = new byte[33];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8), 13);
        "IHDR"u8.CopyTo(bytes.AsSpan(12));
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), height);
        return bytes;
    }

    private static byte[] JpegHeader(int width, int height)
    {
        var app0 = new byte[] { 0xFF, 0xE0, 0x00, 0x10 }.Concat(new byte[14]).ToArray();
        var sof0 = new byte[] { 0xFF, 0xC0, 0x00, 0x11, 0x08, 0, 0, 0, 0, 0x03 }.Concat(new byte[9]).ToArray();
        BinaryPrimitives.WriteUInt16BigEndian(sof0.AsSpan(5), (ushort)height);
        BinaryPrimitives.WriteUInt16BigEndian(sof0.AsSpan(7), (ushort)width);
        byte[] scanStart = [0xFF, 0xDA];
        return new byte[] { 0xFF, 0xD8 }.Concat(app0).Concat(sof0).Concat(scanStart).ToArray();
    }

    private static byte[] WebPLossy(int width, int height)
    {
        var bytes = new byte[40];
        "RIFF"u8.CopyTo(bytes);
        "WEBP"u8.CopyTo(bytes.AsSpan(8));
        "VP8 "u8.CopyTo(bytes.AsSpan(12));
        bytes[23] = 0x9D;
        bytes[24] = 0x01;
        bytes[25] = 0x2A;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(26), (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(28), (ushort)height);
        return bytes;
    }

    private static byte[] WebPLossless(int width, int height)
    {
        var bytes = new byte[40];
        "RIFF"u8.CopyTo(bytes);
        "WEBP"u8.CopyTo(bytes.AsSpan(8));
        "VP8L"u8.CopyTo(bytes.AsSpan(12));
        bytes[20] = 0x2F;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(21), (uint)((width - 1) | (height - 1) << 14));
        return bytes;
    }

    private static byte[] WebPExtended(int width, int height)
    {
        var bytes = new byte[40];
        "RIFF"u8.CopyTo(bytes);
        "WEBP"u8.CopyTo(bytes.AsSpan(8));
        "VP8X"u8.CopyTo(bytes.AsSpan(12));
        var w = width - 1;
        var h = height - 1;
        bytes[24] = (byte)w;
        bytes[25] = (byte)(w >> 8);
        bytes[26] = (byte)(w >> 16);
        bytes[27] = (byte)h;
        bytes[28] = (byte)(h >> 8);
        bytes[29] = (byte)(h >> 16);
        return bytes;
    }

    [Fact]
    public void Reads_png_dimensions()
    {
        var info = ImageFile.Inspect(PngHeader(1600, 900));
        Assert.Equal((ImageFile.Png, 1600, 900), (info.ContentType, info.Width, info.Height));
    }

    [Fact]
    public void Reads_jpeg_dimensions_from_the_start_of_frame()
    {
        var info = ImageFile.Inspect(JpegHeader(1280, 720));
        Assert.Equal((ImageFile.Jpeg, 1280, 720), (info.ContentType, info.Width, info.Height));
    }

    [Theory]
    [InlineData("lossy")]
    [InlineData("lossless")]
    [InlineData("extended")]
    public void Reads_every_webp_flavour(string flavour)
    {
        var bytes = flavour switch
        {
            "lossy" => WebPLossy(2048, 1365),
            "lossless" => WebPLossless(2048, 1365),
            _ => WebPExtended(2048, 1365),
        };
        var info = ImageFile.Inspect(bytes);
        Assert.Equal((ImageFile.WebP, 2048, 1365), (info.ContentType, info.Width, info.Height));
    }

    [Theory]
    [InlineData("GIF89a......")]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>")]
    [InlineData("%PDF-1.7 not an image at all, but long enough to be looked at")]
    public void Refuses_other_formats(string content)
    {
        var ex = Assert.Throws<ProgramValidationException>(() => ImageFile.Inspect(Encoding.ASCII.GetBytes(content)));
        Assert.Contains("JPEG, PNG and WebP", ex.Message);
    }

    [Fact]
    public void Refuses_empty_oversized_and_damaged_files()
    {
        Assert.Throws<ProgramValidationException>(() => ImageFile.Inspect([]));
        Assert.Contains("exceeds", Assert.Throws<ProgramValidationException>(
            () => ImageFile.Inspect(new byte[ProgramImage.MaxBytes + 1])).Message);
        Assert.Contains("damaged", Assert.Throws<ProgramValidationException>(
            () => ImageFile.Inspect(PngHeader(0, 900))).Message);
    }
}
