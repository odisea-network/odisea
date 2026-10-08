using Microsoft.Extensions.Options;
using Odisea.Modules.Integrations.Adapters.Mock;
using Odisea.Modules.Integrations.PublicApi;
using Odisea.SharedKernel;
using Xunit;

namespace Odisea.UnitTests.Integrations;

public class MockReservationProviderTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 10, 9, 10, 0, 0, TimeSpan.Zero);
    }

    private static MockReservationProvider Create(Action<MockProviderOptions>? configure = null)
    {
        var options = new MockProviderOptions();
        configure?.Invoke(options);
        return new MockReservationProvider(Options.Create(options), new FakeClock());
    }

    private static ProviderSearchRequest AntalyaSearch(params string[] hotels) => new(
        "ANT",
        new DateRange(new DateOnly(2027, 3, 30), new DateOnly(2027, 4, 6)),
        new Pax(2),
        hotels);

    [Fact]
    public async Task Search_is_deterministic()
    {
        var provider = Create();
        var first = await provider.SearchAsync(AntalyaSearch("MOCK-ANT-001"), CancellationToken.None);
        var second = await provider.SearchAsync(AntalyaSearch("MOCK-ANT-001"), CancellationToken.None);

        Assert.Equal(2, first.Offers.Count); // HB + AI
        Assert.Equal(
            first.Offers.Select(o => (o.ExternalHotelCode, o.Board, o.NetPrice)),
            second.Offers.Select(o => (o.ExternalHotelCode, o.Board, o.NetPrice)));
        Assert.All(first.Offers, o => Assert.Equal("EUR", o.NetPrice.Currency));
        Assert.All(first.Offers, o => Assert.True(o.NetPrice.Amount > 0));
    }

    [Fact]
    public async Task RePrice_returns_same_price_when_drift_disabled()
    {
        var provider = Create();
        var search = await provider.SearchAsync(AntalyaSearch("MOCK-ANT-001"), CancellationToken.None);
        var offer = search.Offers[0];

        var repriced = await provider.RePriceAsync(offer.OfferToken, CancellationToken.None);

        Assert.True(repriced.IsAvailable);
        Assert.Equal(offer.NetPrice, repriced.CurrentNetPrice);
        Assert.NotNull(repriced.NewOfferToken);
    }

    [Fact]
    public async Task RePrice_drifts_when_drift_rate_is_full()
    {
        var provider = Create(o => { o.RePriceDriftRatePercent = 100; o.DriftPercent = 10m; });
        var search = await provider.SearchAsync(AntalyaSearch("MOCK-ANT-001"), CancellationToken.None);
        var offer = search.Offers[0];

        var repriced = await provider.RePriceAsync(offer.OfferToken, CancellationToken.None);

        Assert.True(repriced.IsAvailable);
        Assert.Equal((offer.NetPrice * 1.10m).Round(), repriced.CurrentNetPrice);
    }

    [Fact]
    public async Task Full_failure_rate_makes_offers_unavailable_and_bookings_fail()
    {
        var provider = Create(o => o.FailureRatePercent = 100);
        var search = await provider.SearchAsync(AntalyaSearch("MOCK-ANT-001"), CancellationToken.None);
        var token = search.Offers[0].OfferToken;

        var repriced = await provider.RePriceAsync(token, CancellationToken.None);
        Assert.False(repriced.IsAvailable);

        var booking = await provider.BookAsync(
            new ProviderBookingRequest(token, [new ProviderPassenger("Test", "Agent", null)], "ODI-X"),
            CancellationToken.None);
        Assert.False(booking.Success);
        Assert.NotNull(booking.FailureReason);
    }

    [Fact]
    public async Task Book_then_status_roundtrip()
    {
        var provider = Create();
        var search = await provider.SearchAsync(AntalyaSearch("MOCK-ANT-001"), CancellationToken.None);

        var booking = await provider.BookAsync(
            new ProviderBookingRequest(search.Offers[0].OfferToken, [new ProviderPassenger("Test", "Agent", null)], "ODI-2027-000001"),
            CancellationToken.None);

        Assert.True(booking.Success);
        Assert.StartsWith("MOCK-", booking.ProviderBookingRef);
        Assert.Equal(
            ProviderBookingState.Confirmed,
            await provider.GetBookingStateAsync(booking.ProviderBookingRef!, CancellationToken.None));

        var cancel = await provider.CancelAsync(booking.ProviderBookingRef!, CancellationToken.None);
        Assert.True(cancel.Success);
    }

    [Fact]
    public async Task Garbage_token_throws_typed_exception()
    {
        var provider = Create();
        await Assert.ThrowsAsync<InvalidOfferTokenException>(
            () => provider.RePriceAsync("not-a-token", CancellationToken.None));
    }
}
