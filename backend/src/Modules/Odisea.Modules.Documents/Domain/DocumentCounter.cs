namespace Odisea.Modules.Documents.Domain;

/// Gapless counter per numbering series. Incremented atomically INSIDE the
/// issuing transaction, so a failed issue rolls the number back — a plain
/// Postgres sequence would leave holes, which чл.78 ППЗДДС does not allow.
public class DocumentCounter
{
    public required string Series { get; set; }
    public long LastNumber { get; set; }
}
