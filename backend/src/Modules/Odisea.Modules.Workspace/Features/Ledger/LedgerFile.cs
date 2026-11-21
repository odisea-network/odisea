using Odisea.Modules.Workspace.Domain.Ledger;

namespace Odisea.Modules.Workspace.Features.Ledger;

/// Recognises the accepted document formats from their bytes, not from the name or the content type
/// the client sends. Anything else is refused: no office files (macros), no HTML or SVG (scriptable).
public static class LedgerFile
{
    public const string Pdf = "application/pdf";
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";

    private const int MaxNameLength = 200;

    public static string Inspect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
            throw new LedgerValidationException("file_required", "Upload a file in the 'file' form field.");
        if (bytes.Length > LedgerDocument.MaxBytes)
            throw new LedgerValidationException("file_too_large",
                $"The file exceeds {LedgerDocument.MaxBytes / 1024 / 1024} MB.");

        ReadOnlySpan<byte> png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (bytes.StartsWith("%PDF-"u8))
            return Pdf;
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return Jpeg;
        if (bytes.StartsWith(png))
            return Png;
        throw new LedgerValidationException("file_type_unsupported", "Only PDF, JPEG and PNG files are accepted.");
    }

    /// The name is shown back and sent in a download header, so it is reduced to a bare file name
    /// without path parts or control characters. The extension follows the detected type.
    public static string SafeName(string? original, string contentType)
    {
        var name = (original ?? "").Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        name = new string([.. name.Where(c => !char.IsControl(c) && c != '"')]).Trim().TrimStart('.');

        var extension = contentType switch
        {
            Pdf => ".pdf",
            Jpeg => ".jpg",
            _ => ".png",
        };
        var dot = name.LastIndexOf('.');
        var stem = (dot > 0 ? name[..dot] : name).Trim();
        if (stem.Length == 0)
            stem = "document";
        if (stem.Length > MaxNameLength - extension.Length)
            stem = stem[..(MaxNameLength - extension.Length)];
        return stem + extension;
    }
}
