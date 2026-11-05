using Odisea.SharedKernel;

namespace Odisea.Modules.Catalog.Domain;

/// A photo of the program, stored in the catalog schema. The bytes behind an id never change
/// (there is no replace — delete and upload instead), so clients may cache them forever.
public class ProgramImage : Entity
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxPerProgram = 20;

    public required Guid ProgramId { get; set; }
    public required string ContentType { get; set; }
    public required byte[] Data { get; set; }
    public required int SizeBytes { get; set; }
    public required int Width { get; set; }
    public required int Height { get; set; }

    /// SHA-256 of the bytes, hex. Doubles as the ETag.
    public required string Sha256 { get; set; }

    public string? Caption { get; set; }

    /// What a screen reader (or a broken image) says instead of the picture.
    public string? AltText { get; set; }

    public int SortOrder { get; set; }
    public bool IsCover { get; set; }
}
