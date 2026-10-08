using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Odisea.Modules.Integrations.PublicApi;
using Odisea.SharedKernel;

namespace Odisea.Modules.Integrations.Adapters.Mock;

/// Deterministic in-process provider. Same request → same offers, so tests and
/// manual Swagger sessions are reproducible. Failure and drift rates are
/// configuration-driven to exercise the unhappy booking paths on demand.
public class MockReservationProvider(IOptions<MockProviderOptions> options, IClock clock) : IReservationProvider
{
    public const string Code = "mock";
    private static readonly string[] Boards = ["HB", "AI"];

    private readonly MockProviderOptions _options = options.Value;

    public string ProviderCode => Code;

    public async Task<ProviderSearchResult> SearchAsync(ProviderSearchRequest request, CancellationToken ct)
    {
        await SimulateLatencyAsync(ct);

        var hotelCodes = request.ExternalHotelCodes.Count > 0
            ? request.ExternalHotelCodes
            : [$"{request.ExternalLocationCode}-H1", $"{request.ExternalLocationCode}-H2", $"{request.ExternalLocationCode}-H3"];

        List<ProviderOffer> offers = [];
        foreach (var hotelCode in hotelCodes)
        {
            foreach (var board in Boards)
            {
                var net = CalculateNet(hotelCode, board, request.Stay, request.Pax);
                var payload = new TokenPayload(
                    hotelCode, "DBL", board, net.Amount,
                    request.Stay.From, request.Stay.To,
                    request.Pax.Adults, request.Pax.Children);
                offers.Add(new ProviderOffer(
                    Encode(payload), hotelCode, "DBL", board, net,
                    clock.UtcNow.AddMinutes(30)));
            }
        }

        return new ProviderSearchResult(offers);
    }

    public async Task<ProviderRePriceResult> RePriceAsync(string offerToken, CancellationToken ct)
    {
        await SimulateLatencyAsync(ct);
        var payload = Decode(offerToken);

        if (Roll(offerToken, "availability") < _options.FailureRatePercent)
            return new ProviderRePriceResult(false, null, null);

        var price = Money.Eur(payload.Net);
        if (Roll(offerToken, "drift") < _options.RePriceDriftRatePercent)
            price = (price * (1 + _options.DriftPercent / 100m)).Round();

        var refreshed = payload with { Net = price.Amount };
        return new ProviderRePriceResult(true, price, Encode(refreshed));
    }

    public async Task<ProviderBookingResult> BookAsync(ProviderBookingRequest request, CancellationToken ct)
    {
        await SimulateLatencyAsync(ct);
        Decode(request.OfferToken); // validates the token

        if (Roll(request.OfferToken, "book") < _options.FailureRatePercent)
            return new ProviderBookingResult(false, null, "No availability at confirmation.");

        var reference = $"MOCK-{StableHash(request.OfferToken + request.ClientReference):X8}";
        return new ProviderBookingResult(true, reference, null);
    }

    public async Task<ProviderCancelResult> CancelAsync(string providerBookingRef, CancellationToken ct)
    {
        await SimulateLatencyAsync(ct);
        return new ProviderCancelResult(true, null);
    }

    public async Task<ProviderBookingState> GetBookingStateAsync(string providerBookingRef, CancellationToken ct)
    {
        await SimulateLatencyAsync(ct);
        return providerBookingRef.StartsWith("MOCK-", StringComparison.Ordinal)
            ? ProviderBookingState.Confirmed
            : ProviderBookingState.Unknown;
    }

    private static Money CalculateNet(string hotelCode, string board, DateRange stay, Pax pax)
    {
        var perNight = 50 + StableHash(hotelCode) % 100;
        var boardFactor = board == "AI" ? 1.4m : 1.0m;
        var paxFactor = pax.Adults + 0.5m * pax.Children;
        return (Money.Eur(perNight) * stay.Nights * paxFactor * boardFactor).Round();
    }

    private int Roll(string token, string purpose) => (int)(StableHash($"{purpose}:{token}") % 100);

    // string.GetHashCode is randomized per process — a stable hash keeps the mock deterministic.
    private static uint StableHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return BitConverter.ToUInt32(bytes, 0);
    }

    private Task SimulateLatencyAsync(CancellationToken ct) =>
        _options.LatencyMs > 0 ? Task.Delay(_options.LatencyMs, ct) : Task.CompletedTask;

    private sealed record TokenPayload(
        string Hotel, string Room, string Board, decimal Net,
        DateOnly From, DateOnly To, int Adults, int Children);

    private static string Encode(TokenPayload payload) =>
        Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(payload));

    private static TokenPayload Decode(string token)
    {
        try
        {
            return JsonSerializer.Deserialize<TokenPayload>(Convert.FromBase64String(token))
                ?? throw new InvalidOfferTokenException(Code);
        }
        catch (Exception e) when (e is FormatException or JsonException)
        {
            throw new InvalidOfferTokenException(Code);
        }
    }
}
