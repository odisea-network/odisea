using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Booking.Domain;
using Odisea.Modules.Booking.Features.Bookings;
using Odisea.Modules.Booking.Infrastructure;
using Odisea.Modules.Catalog.PublicApi;
using Odisea.Modules.Integrations.Adapters.Mock;
using Odisea.Modules.Integrations.Features;
using Odisea.Modules.Integrations.Infrastructure;
using Odisea.Modules.Integrations.PublicApi;
using Odisea.Modules.Pricing.Domain;
using Odisea.Modules.Pricing.Features.Calculator;
using Odisea.Modules.Pricing.Infrastructure;
using Odisea.SharedKernel;
using Xunit;

namespace Odisea.UnitTests.Booking;

/// The agency side over the real mock provider, real token protector and real price
/// calculator — only persistence and the two lookups are faked.
public class BookingServiceTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeAgencyLookup(bool active = true) : IAgencyLookup
    {
        public Task<AgencySnapshot?> FindAsync(Guid agencyId, CancellationToken ct) =>
            Task.FromResult<AgencySnapshot?>(new AgencySnapshot(agencyId, "Test Agency", active, 50_000m));

        public Task<AgencyBillingInfo?> GetBillingInfoAsync(Guid agencyId, CancellationToken ct) =>
            Task.FromResult<AgencyBillingInfo?>(new AgencyBillingInfo(
                agencyId, "Тест Ейджънси ЕООД", "201234567", "BG201234567",
                "ул. Тестова 1", "София", "Тест МОЛ"));
    }

    private sealed class FakeProgramLookup(BookingTarget? target) : IProgramLookup
    {
        public Task<BookingTarget?> ResolveBookingTargetAsync(
            Guid programId, Guid departureId, Guid hotelId, CancellationToken ct) =>
            Task.FromResult(target);
    }

    private sealed class FakeRefGenerator : IBookingRefGenerator
    {
        private int _n;
        public Task<string> NextRefAsync(int year, CancellationToken ct) =>
            Task.FromResult($"ODI-{year}-{++_n:D6}");
    }

    private readonly BookingDbContext _db;
    private readonly PricingDbContext _pricing;
    private readonly MockProviderOptions _mockOptions = new();
    private readonly OfferTokenProtector _protector = new(TestDataProtection.Provider);
    private readonly FakeClock _clock = new();
    private readonly Guid _agencyId = Guid.NewGuid();
    private readonly BookingActor _agent = new(BookingActorType.Agency, Guid.NewGuid());
    private readonly BookingTarget _target;
    private readonly MockReservationProvider _mock;

    public static readonly PassengerRequest Ivan = new(
        "Ivan", "Petrov", new DateOnly(1990, 5, 1), Gender: Gender.Male, Nationality: "bg",
        DocumentType: TravelDocumentType.Passport, DocumentNumber: "ab1234567", DocumentIssuingCountry: "BG",
        DocumentExpiryDate: new DateOnly(2031, 1, 1), IsLead: true,
        Email: "ivan.petrov@example.com", Phone: "+359 888 123 456");

    public static readonly PassengerRequest Maria = new(
        "Maria", "Petrova", new DateOnly(1992, 7, 2), Gender: Gender.Female, Nationality: "BG",
        DocumentType: TravelDocumentType.IdCard, DocumentNumber: "6401234567", DocumentIssuingCountry: "BG",
        DocumentExpiryDate: new DateOnly(2030, 6, 1));

    public BookingServiceTests()
    {
        _db = new BookingDbContext(
            new DbContextOptionsBuilder<BookingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            TestDataProtection.Provider);
        _pricing = new PricingDbContext(new DbContextOptionsBuilder<PricingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _pricing.PricingRules.Add(new PricingRule
        {
            Name = "markup", Kind = RuleKind.Markup, ValueType = RuleValueType.Percent, Value = 15m,
        });
        _pricing.SaveChanges();

        _mock = new MockReservationProvider(Options.Create(_mockOptions), _clock);
        _target = new BookingTarget(
            Guid.NewGuid(), "Antalya Easter 2027", "mock",
            Guid.NewGuid(), new DateOnly(2027, 3, 30), new DateOnly(2027, 4, 6),
            Guid.NewGuid(), "Mock Palace");
    }

    private BookingService Service(bool agencyActive = true, bool resolvable = true) => new(
        _db,
        new ProviderRegistry([_mock], new IntegrationsDbContext(
            new DbContextOptionsBuilder<IntegrationsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options)),
        _protector,
        new PriceCalculator(_pricing),
        new FakeAgencyLookup(agencyActive),
        new FakeProgramLookup(resolvable ? _target : null),
        new FakeRefGenerator(),
        Options.Create(new BookingOptions()),
        _clock);

    private async Task<string> ProtectedOfferTokenAsync(Guid? agencyId = null, Guid? hotelId = null, int adults = 2)
    {
        var search = await _mock.SearchAsync(
            new ProviderSearchRequest("ANT",
                new DateRange(new DateOnly(2027, 3, 30), new DateOnly(2027, 4, 6)),
                new Pax(adults), ["MOCK-ANT-001"]),
            CancellationToken.None);
        return _protector.Protect(new OfferEnvelope(
            search.Offers[0].OfferToken, agencyId ?? _agencyId, hotelId ?? _target.HotelId, adults, 0));
    }

    private Task<BookingDto> CreateDraftAsync(BookingService service, params PassengerRequest[] passengers) =>
        CreateDraftAsync(service, null, passengers);

    private async Task<BookingDto> CreateDraftAsync(
        BookingService service, string? offerToken, params PassengerRequest[] passengers) =>
        await service.CreateDraftAsync(
            new CreateBookingRequest(_target.ProgramId, _target.DepartureId, _target.HotelId,
                offerToken ?? await ProtectedOfferTokenAsync(),
                [.. passengers.Length > 0 ? passengers : [Ivan, Maria]]),
            _agencyId, _agent, CancellationToken.None);

    private async Task<BookingDto> SubmittedAsync(BookingService service)
    {
        var draft = await CreateDraftAsync(service);
        await service.RePriceAsync(draft.Id, _agencyId, _agent, CancellationToken.None);
        return await service.SubmitAsync(draft.Id, _agencyId, _agent, CancellationToken.None);
    }

    [Fact]
    public async Task Draft_gets_ref_passengers_occupancy_and_history()
    {
        var dto = await CreateDraftAsync(Service());

        Assert.Equal("ODI-2027-000001", dto.Ref);
        Assert.Equal("Draft", dto.Status);
        Assert.Equal(2, dto.Adults);
        Assert.Equal(0, dto.Children);
        Assert.Equal(2, dto.Passengers.Count);
        Assert.True(dto.Passengers[0].IsLead);
        Assert.Equal("Adult", dto.Passengers[0].PassengerType);
        Assert.Single(dto.History);
        Assert.Equal("Agency", dto.History[0].Actor);
        Assert.Null(dto.SellPrice);
        Assert.Equal(0m, dto.PaidAmount);
    }

    [Fact]
    public async Task Country_codes_are_upper_cased_and_the_document_number_round_trips()
    {
        var dto = await CreateDraftAsync(Service());
        var lead = dto.Passengers.Single(p => p.IsLead);

        Assert.Equal("BG", lead.Nationality);
        Assert.Equal("Passport", lead.DocumentType);
        Assert.Equal("AB1234567", lead.DocumentNumber);

        var stored = await _db.BookingPassengers.AsNoTracking().SingleAsync(p => p.IsLead);
        Assert.Equal("AB1234567", stored.DocumentNumber);
    }

    [Fact]
    public async Task First_passenger_leads_when_nobody_is_flagged()
    {
        var dto = await CreateDraftAsync(Service(),
            new PassengerRequest("Anna", "Georgieva", new DateOnly(1980, 1, 1)),
            new PassengerRequest("Boris", "Georgiev", new DateOnly(1979, 1, 1)));

        Assert.Equal("Anna", dto.Passengers.Single(p => p.IsLead).FirstName);
    }

    [Fact]
    public async Task Two_leads_are_rejected()
    {
        await Assert.ThrowsAsync<BookingValidationException>(() => CreateDraftAsync(Service(),
            Ivan, Maria with { IsLead = true }));
    }

    [Fact]
    public async Task Inactive_agency_cannot_create_bookings()
    {
        await Assert.ThrowsAsync<BookingValidationException>(
            () => CreateDraftAsync(Service(agencyActive: false)));
    }

    [Fact]
    public async Task Unbookable_combination_is_rejected()
    {
        await Assert.ThrowsAsync<BookingValidationException>(
            () => CreateDraftAsync(Service(resolvable: false)));
    }

    [Fact]
    public async Task Tampered_offer_token_is_rejected()
    {
        await Assert.ThrowsAsync<InvalidOfferTokenException>(
            () => CreateDraftAsync(Service(), "tampered-token", Ivan, Maria));
    }

    [Fact]
    public async Task Offer_token_issued_to_another_agency_is_rejected()
    {
        var foreign = await ProtectedOfferTokenAsync(agencyId: Guid.NewGuid());
        await Assert.ThrowsAsync<InvalidOfferTokenException>(
            () => CreateDraftAsync(Service(), foreign, Ivan, Maria));
    }

    [Fact]
    public async Task Offer_token_for_a_different_hotel_is_rejected()
    {
        var otherHotel = await ProtectedOfferTokenAsync(hotelId: Guid.NewGuid());
        await Assert.ThrowsAsync<InvalidOfferTokenException>(
            () => CreateDraftAsync(Service(), otherHotel, Ivan, Maria));
    }

    [Fact]
    public async Task Passengers_can_be_replaced_while_the_price_is_not_final()
    {
        var service = Service();
        var draft = await CreateDraftAsync(service);

        var updated = await service.ReplacePassengersAsync(draft.Id, _agencyId,
            new UpdatePassengersRequest([
                new PassengerRequest("Georgi", "Dimitrov", new DateOnly(1985, 3, 3), IsLead: true),
                new PassengerRequest("Elena", "Dimitrova", new DateOnly(1986, 4, 4)),
            ]),
            CancellationToken.None);

        Assert.Equal(["Georgi", "Elena"], updated.Passengers.Select(p => p.FirstName).ToArray());
        Assert.Equal(2, await _db.BookingPassengers.CountAsync());
        Assert.Equal("Draft", updated.Status);
    }

    [Fact]
    public async Task Passengers_are_frozen_once_submitted()
    {
        var service = Service();
        var submitted = await SubmittedAsync(service);

        await Assert.ThrowsAsync<BookingStateException>(() => service.ReplacePassengersAsync(
            submitted.Id, _agencyId, new UpdatePassengersRequest([Ivan, Maria]), CancellationToken.None));
    }

    [Fact]
    public async Task RePrice_sets_sell_price_and_breakdown()
    {
        var service = Service();
        var draft = await CreateDraftAsync(service);

        var result = await service.RePriceAsync(draft.Id, _agencyId, _agent, CancellationToken.None);

        Assert.Equal("PriceConfirmed", result.Booking.Status);
        Assert.False(result.PriceChanged); // first pricing — nothing to compare against
        Assert.NotNull(result.Booking.SellPrice);
        Assert.Equal(result.Booking.SellPrice, result.Booking.BalanceDue);
        var stored = await _db.Bookings.SingleAsync();
        Assert.Contains("Markup", stored.BreakdownJson);
        Assert.Equal(Math.Round((decimal)stored.NetAmount! * 1.15m, 2), stored.SellAmount);
    }

    [Fact]
    public async Task RePrice_surfaces_drift_on_the_second_pass()
    {
        var service = Service();
        var draft = await CreateDraftAsync(service);
        await service.RePriceAsync(draft.Id, _agencyId, _agent, CancellationToken.None);

        _mockOptions.RePriceDriftRatePercent = 100;
        _mockOptions.DriftPercent = 10m;
        var second = await service.RePriceAsync(draft.Id, _agencyId, _agent, CancellationToken.None);

        Assert.True(second.PriceChanged);
        Assert.True(second.Booking.SellPrice > second.PreviousSellPrice);
    }

    [Fact]
    public async Task Unavailable_offer_blocks_without_corrupting_state()
    {
        var service = Service();
        var draft = await CreateDraftAsync(service);
        _mockOptions.FailureRatePercent = 100;

        await Assert.ThrowsAsync<OfferUnavailableException>(
            () => service.RePriceAsync(draft.Id, _agencyId, _agent, CancellationToken.None));
        Assert.Equal(BookingStatus.Draft, (await _db.Bookings.SingleAsync()).Status);
    }

    [Fact]
    public async Task Submit_hands_the_reservation_to_the_provider_and_starts_the_payment_clock()
    {
        var submitted = await SubmittedAsync(Service());

        Assert.Equal("AwaitingPayment", submitted.Status);
        Assert.StartsWith("MOCK-", submitted.ProviderBookingRef);
        Assert.Equal(_clock.UtcNow.AddDays(3), submitted.PaymentDueAt); // far departure → option window
        Assert.Equal(["-", "Draft", "PriceConfirmed", "PendingConfirmation"],
            submitted.History.Select(h => h.From).ToArray());
        Assert.Equal("Agency", submitted.History[2].Actor);  // PriceConfirmed → PendingConfirmation
        Assert.Equal("System", submitted.History[^1].Actor); // PendingConfirmation → AwaitingPayment
        Assert.Contains("payment due by 2026-10-12 12:00 UTC", submitted.History[^1].Note);
    }

    [Fact]
    public async Task Submit_requires_complete_passengers_and_leaves_the_price_confirmed()
    {
        var service = Service();
        var draft = await CreateDraftAsync(service, Ivan, new PassengerRequest("Nameless", "Traveller"));
        await service.RePriceAsync(draft.Id, _agencyId, _agent, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<BookingValidationException>(
            () => service.SubmitAsync(draft.Id, _agencyId, _agent, CancellationToken.None));

        Assert.Contains("Passenger 2", ex.Message);
        Assert.DoesNotContain("Nameless", ex.Message);
        var stored = await _db.Bookings.Include(b => b.History).SingleAsync();
        Assert.Equal(BookingStatus.PriceConfirmed, stored.Status);
        Assert.Equal(2, stored.History.Count);
    }

    [Fact]
    public async Task Submit_rejects_a_party_that_does_not_match_the_occupancy()
    {
        var service = Service();
        var draft = await CreateDraftAsync(service, Ivan);
        await service.RePriceAsync(draft.Id, _agencyId, _agent, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<BookingValidationException>(
            () => service.SubmitAsync(draft.Id, _agencyId, _agent, CancellationToken.None));
        Assert.Contains("occupancy", ex.Message);
    }

    [Fact]
    public async Task Submit_records_a_provider_refusal_as_failed()
    {
        var service = Service();
        var draft = await CreateDraftAsync(service);
        await service.RePriceAsync(draft.Id, _agencyId, _agent, CancellationToken.None);
        _mockOptions.FailureRatePercent = 100;

        var failed = await service.SubmitAsync(draft.Id, _agencyId, _agent, CancellationToken.None);

        Assert.Equal("Failed", failed.Status);
        Assert.Contains("No availability", failed.History[^1].Note);
        Assert.Null(failed.PaymentDueAt);
    }

    [Fact]
    public async Task Submit_straight_from_draft_is_an_invalid_transition()
    {
        var service = Service();
        var draft = await CreateDraftAsync(service);

        await Assert.ThrowsAsync<InvalidBookingTransitionException>(
            () => service.SubmitAsync(draft.Id, _agencyId, _agent, CancellationToken.None));
    }

    [Fact]
    public async Task Agency_can_withdraw_while_awaiting_payment()
    {
        var service = Service();
        var submitted = await SubmittedAsync(service);

        var cancelled = await service.CancelAsync(submitted.Id, _agencyId, _agent, CancellationToken.None);

        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal("Agency", cancelled.History[^1].Actor);
    }

    [Fact]
    public async Task Agency_cannot_cancel_once_confirmed()
    {
        var service = Service();
        var submitted = await SubmittedAsync(service);
        var stored = await _db.Bookings.SingleAsync(b => b.Id == submitted.Id);
        stored.TransitionTo(BookingStatus.Confirmed, new BookingActor(BookingActorType.Operator, Guid.NewGuid()), "paid", _clock.UtcNow);
        _db.Add(stored.History[^1]);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<BookingStateException>(
            () => service.CancelAsync(submitted.Id, _agencyId, _agent, CancellationToken.None));
    }

    [Fact]
    public async Task Other_agencies_bookings_are_invisible()
    {
        var service = Service();
        var draft = await CreateDraftAsync(service);
        var otherAgency = Guid.NewGuid();

        Assert.Null(await service.GetAsync(draft.Id, otherAgency, CancellationToken.None));
        Assert.Empty(await service.ListAsync(otherAgency, CancellationToken.None));
        await Assert.ThrowsAsync<BookingNotFoundException>(
            () => service.RePriceAsync(draft.Id, otherAgency, _agent, CancellationToken.None));
    }

    [Fact]
    public async Task Unknown_booking_is_not_found()
    {
        var service = Service();
        await Assert.ThrowsAsync<BookingNotFoundException>(() => service.ReplacePassengersAsync(
            Guid.NewGuid(), _agencyId, new UpdatePassengersRequest([Ivan]), CancellationToken.None));
    }
}
