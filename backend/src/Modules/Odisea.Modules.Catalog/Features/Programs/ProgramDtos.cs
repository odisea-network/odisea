using System.ComponentModel.DataAnnotations;
using Odisea.Modules.Catalog.Domain;

namespace Odisea.Modules.Catalog.Features.Programs;

public record ProgramDto(
    Guid Id, string Name, Guid DestinationId, string ProviderCode,
    string? Season, string? Description, string Status, DateTimeOffset CreatedAt);

public record ProgramDetailDto(
    Guid Id, string Name, Guid DestinationId, string ProviderCode,
    string? Season, string? Description, string Status,
    IReadOnlyList<DepartureDto> Departures,
    IReadOnlyList<Guid> HotelIds);

public record DepartureDto(Guid Id, DateOnly StartDate, DateOnly EndDate, int Nights, string? TransportNote);

public record CreateProgramRequest(
    [Required, MaxLength(200)] string Name,
    [Required] Guid DestinationId,
    [Required, MaxLength(30)] string ProviderCode,
    [MaxLength(50)] string? Season,
    [MaxLength(2000)] string? Description);

public record UpdateProgramRequest(
    [Required, MaxLength(200)] string Name,
    [MaxLength(50)] string? Season,
    [MaxLength(2000)] string? Description);

public record CreateDepartureRequest(
    [Required] DateOnly StartDate,
    [Required] DateOnly EndDate,
    [MaxLength(500)] string? TransportNote);

public static class ProgramDtoMapping
{
    public static ProgramDto ToDto(this Domain.Program p) => new(
        p.Id, p.Name, p.DestinationId, p.ProviderCode, p.Season, p.Description, p.Status.ToString(), p.CreatedAt);

    public static DepartureDto ToDto(this ProgramDeparture d) =>
        new(d.Id, d.StartDate, d.EndDate, d.Period.Nights, d.TransportNote);

    public static ProgramDetailDto ToDetailDto(this Domain.Program p) => new(
        p.Id, p.Name, p.DestinationId, p.ProviderCode, p.Season, p.Description, p.Status.ToString(),
        [.. p.Departures.OrderBy(d => d.StartDate).Select(d => d.ToDto())],
        [.. p.Hotels.Select(h => h.HotelId)]);
}
