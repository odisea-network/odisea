using Odisea.Domain.Enums;

namespace Odisea.Infrastructure.Suppliers.Connectors;

// A connector's parsed-but-not-yet-validated view of one supplier offer, plus the
// untransformed payload it came from. Each adapter (JSON, XML, …) turns its own
// wire format into a sequence of these and hands them to SourceOfferImporter, which
// owns validation + upsert so the adapters only differ in fetch + parse.
public sealed record RawOffer(
    string ExternalId,
    string Title,
    string? Description,
    string Country,
    string? City,
    decimal Price,
    string? Currency,
    string? Board,
    string? Transport,
    int Nights,
    string? ImageUrl,
    IReadOnlyList<string>? Tags,
    string RawPayload)
{
    // board/transport are strings on the wire; an unknown value (or a missing
    // required field) marks the row invalid so the importer skips it.
    public bool IsValid(out BoardBasis board, out Transport transport)
    {
        board = default;
        transport = default;
        return !string.IsNullOrWhiteSpace(ExternalId)
            && !string.IsNullOrWhiteSpace(Title)
            && !string.IsNullOrWhiteSpace(Country)
            && Price >= 0
            && Enum.TryParse(Board, ignoreCase: true, out board)
            && Enum.TryParse(Transport, ignoreCase: true, out transport);
    }
}
