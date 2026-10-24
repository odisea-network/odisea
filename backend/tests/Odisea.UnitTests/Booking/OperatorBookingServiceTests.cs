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

/// The operator side: payments, the admin confirmation gate, reject/expire/cancel, oversight.
/// Reservations are brought to AwaitingPayment through the real agency service first.
public class OperatorBookingServiceTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeAgencyLookup : IAgencyLookup
    {
        public Task<AgencySnapshot?> FindAsync(Guid agencyId, CancellationToken ct) =>
            Task.FromResult<AgencySnapshot?>(new AgencySnapshot(agencyId, "Test Agency", true, 50_000m));

        public Task<AgencyBillingInfo?> GetBillingInfoAsync(Guid agencyId, CancellationToken ct) =>
            Task.FromResult<AgencyBillingInfo?>(null);

        public Task<AgencyContractInfo?> GetContractInfoAsync(Guid agencyId, CancellationToken ct) =>
            Task.FromResult<AgencyContractInfo?>(null);
    }

    private sealed class FakeProgramLookup(BookingTarget target) : IProgramLookup
    {
        public Task<BookingTarget?> ResolveBookingTargetAsync(
            Guid programId, Guid departureId, Guid hotelId, CancellationToken ct) =>
            Task.FromResult<BookingTarget?>(target);
    }

    private sealed class FakeRefGenerator : IBookingRefGenerator
    {
        private int _n;
        public Task<string> NextRefAsync(int year, CancellationToken ct) =>
            Task.FromResult($"ODI-{year}-{++_n:D6}");
    }

    private readonly BookingDbContext _db;
    private readonly FakeClock _clock = new();
    private readonly MockProviderOptions _mockOptions = new();
    private readonly OfferTokenProtector _protector = new(TestDataProtection.Provider);
    private readonly MockReservationProvider _mock;
    private readonly ProviderRegistry _providers;
    private readonly BookingTarget _target;
    private readonly Guid _agencyId = Guid.NewGuid();
    private readonly BookingActor _agent = new(BookingActorType.Agency, Guid.NewGuid());
    private readonly BookingActor _admin = new(BookingActorType.Operator, Guid.NewGuid());
    private readonly BookingService _agencyService;
    private readonly OperatorBookingService _service;

    public OperatorBookingServiceTests()
    {
        _db = new BookingDbContext(
            new DbContextOptionsBuilder<BookingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            TestDataProtection.Provider);
        var pricing = new PricingDbContext(new DbContextOptionsBuilder<PricingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        pricing.PricingRules.Add(new PricingRule
        {
            Name = "markup", Kind = RuleKind.Markup, ValueType = RuleValueType.Percent, Value = 15m,
        });
        pricing.SaveChanges();

        _mock = new MockReservationProvider(Options.Create(_mockOptions), _clock);
        _providers = new ProviderRegistry([_mock], new IntegrationsDbContext(
            new DbContextOptionsBuilder<IntegrationsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options));
        _target = new BookingTarget(
            Guid.NewGuid(), "Antalya Easter 2027", "mock",
            Guid.NewGuid(), new DateOnly(2027, 3, 30), new DateOnly(2027, 4, 6),
            Guid.NewGuid(), "Mock Palace");

        _agencyService = new BookingService(
            _db, _providers, _protector, new PriceCalculator(pricing),
            new FakeAgencyLookup(), new FakeProgramLookup(_target), new FakeRefGenerator(),
            Options.Create(new BookingOptions()), _clock);
        _service = new OperatorBookingService(_db, _providers, _clock);
    }

    private async Task<BookingDto> DraftAsync()
    {
        var search = await _mock.SearchAsync(
            new ProviderSearchRequest("ANT",
                new DateRange(new DateOnly(2027, 3, 30), new DateOnly(2027, 4, 6)), new Pax(2), ["MOCK-ANT-001"]),
            CancellationToken.None);
        var token = _protector.Protect(new OfferEnvelope(search.Offers[0].OfferToken, _agencyId, _target.HotelId, 2, 0));
        return await _agencyService.CreateDraftAsync(
            new CreateBookingRequest(_target.ProgramId, _target.DepartureId, _target.HotelId, token,
                [BookingServiceTests.Ivan, BookingServiceTests.Maria]),
            _agencyId, _agent, CancellationToken.None);
    }

    private async Task<BookingDto> AwaitingPaymentAsync()
    {
        var draft = await DraftAsync();
        await _agencyService.RePriceAsync(draft.Id, _agencyId, _agent, CancellationToken.None);
        var submitted = await _agencyService.SubmitAsync(draft.Id, _agencyId, _agent, CancellationToken.None);
        Assert.Equal("AwaitingPayment", submitted.Status);
        return submitted;
    }

    private static RecordPaymentRequest Payment(decimal amount, string currency = "EUR") =>
        new(amount, currency, PaymentMethod.BankTransfer, new DateOnly(2026, 10, 9), "TRX-1");

    [Fact]
    public async Task Confirm_is_refused_until_fully_paid()
    {
        var booking = await AwaitingPaymentAsync();

        var ex = await Assert.ThrowsAsync<PaymentIncompleteException>(
            () => _service.ConfirmAsync(booking.Id, _admin, CancellationToken.None));

        Assert.Contains($"Balance due: {booking.SellPrice:0.00} EUR", ex.Message);
        Assert.Equal(BookingStatus.AwaitingPayment, (await _db.Bookings.SingleAsync()).Status);
    }

    [Fact]
    public async Task Payments_accumulate_and_the_admin_confirms_once_covered()
    {
        var booking = await AwaitingPaymentAsync();
        var sell = booking.SellPrice!.Value;

        var partial = await _service.RecordPaymentAsync(booking.Id, Payment(100m), _admin, CancellationToken.None);
        Assert.Equal(100m, partial.PaidAmount);
        Assert.Equal(sell - 100m, partial.BalanceDue);

        await _service.RecordPaymentAsync(booking.Id, Payment(sell - 100m), _admin, CancellationToken.None);
        var confirmed = await _service.ConfirmAsync(booking.Id, _admin, CancellationToken.None);

        Assert.Equal("Confirmed", confirmed.Status);
        Assert.Equal(0m, confirmed.BalanceDue);
        Assert.Equal(2, confirmed.Payments.Count);
        Assert.Equal("Operator", confirmed.History[^1].ActorType);
        Assert.Equal(_admin.UserId, confirmed.History[^1].ActorUserId);
        Assert.Contains($"paid {sell:0.00} EUR", confirmed.History[^1].Note);
    }

    [Fact]
    public async Task Overpayment_wrong_currency_and_future_dates_are_rejected()
    {
        var booking = await AwaitingPaymentAsync();
        var sell = booking.SellPrice!.Value;

        var over = await Assert.ThrowsAsync<BookingValidationException>(
            () => _service.RecordPaymentAsync(booking.Id, Payment(sell + 0.01m), _admin, CancellationToken.None));
        Assert.Contains("exceeds the balance due", over.Message);

        var currency = await Assert.ThrowsAsync<BookingValidationException>(
            () => _service.RecordPaymentAsync(booking.Id, Payment(10m, "bgn"), _admin, CancellationToken.None));
        Assert.Contains("BGN does not match", currency.Message);

        await Assert.ThrowsAsync<BookingValidationException>(() => _service.RecordPaymentAsync(booking.Id,
            Payment(10m) with { ReceivedOn = new DateOnly(2026, 10, 10) }, _admin, CancellationToken.None));
    }

    [Fact]
    public async Task Agency_cannot_withdraw_once_money_has_been_recorded()
    {
        var booking = await AwaitingPaymentAsync();
        await _service.RecordPaymentAsync(booking.Id, Payment(10m), _admin, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<BookingStateException>(
            () => _agencyService.CancelAsync(booking.Id, _agencyId, _agent, CancellationToken.None));

        Assert.Contains("Payments have already been recorded", ex.Message);
        Assert.Equal(BookingStatus.AwaitingPayment, (await _db.Bookings.SingleAsync()).Status);
    }

    [Fact]
    public async Task Payments_are_only_recorded_while_awaiting_payment()
    {
        var draft = await DraftAsync();
        await Assert.ThrowsAsync<BookingStateException>(
            () => _service.RecordPaymentAsync(draft.Id, Payment(10m), _admin, CancellationToken.None));
    }

    [Fact]
    public async Task Voided_payments_stop_counting_and_block_confirmation()
    {
        var booking = await AwaitingPaymentAsync();
        var paid = await _service.RecordPaymentAsync(booking.Id, Payment(booking.SellPrice!.Value), _admin, CancellationToken.None);
        var payment = paid.Payments.Single();

        var voided = await _service.VoidPaymentAsync(booking.Id, payment.Id, "wrong booking", _admin, CancellationToken.None);

        Assert.Equal(0m, voided.PaidAmount);
        Assert.Equal("wrong booking", voided.Payments.Single().VoidReason);
        Assert.Equal(_admin.UserId, voided.Payments.Single().VoidedByUserId);
        await Assert.ThrowsAsync<PaymentIncompleteException>(
            () => _service.ConfirmAsync(booking.Id, _admin, CancellationToken.None));
        await Assert.ThrowsAsync<BookingValidationException>(
            () => _service.VoidPaymentAsync(booking.Id, payment.Id, "again", _admin, CancellationToken.None));
        await Assert.ThrowsAsync<PaymentNotFoundException>(
            () => _service.VoidPaymentAsync(booking.Id, Guid.NewGuid(), "ghost", _admin, CancellationToken.None));
    }

    [Fact]
    public async Task Reject_releases_the_provider_hold_and_keeps_the_note()
    {
        var booking = await AwaitingPaymentAsync();

        var rejected = await _service.RejectAsync(booking.Id, "Agency asked to withdraw", _admin, CancellationToken.None);

        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal("Agency asked to withdraw", rejected.History[^1].Note);
        await Assert.ThrowsAsync<InvalidBookingTransitionException>(
            () => _service.ConfirmAsync(booking.Id, _admin, CancellationToken.None));
    }

    [Fact]
    public async Task Expire_waits_for_the_payment_deadline()
    {
        var booking = await AwaitingPaymentAsync();

        var early = await Assert.ThrowsAsync<BookingStateException>(
            () => _service.ExpireAsync(booking.Id, _admin, CancellationToken.None));
        Assert.Contains("deadline has not passed", early.Message);

        _clock.UtcNow = booking.PaymentDueAt!.Value.AddMinutes(1);
        var expired = await _service.ExpireAsync(booking.Id, _admin, CancellationToken.None);

        Assert.Equal("Expired", expired.Status);
        Assert.Contains("deadline", expired.History[^1].Note);
    }

    [Fact]
    public async Task Admin_can_cancel_a_confirmed_booking()
    {
        var booking = await AwaitingPaymentAsync();
        await _service.RecordPaymentAsync(booking.Id, Payment(booking.SellPrice!.Value), _admin, CancellationToken.None);
        await _service.ConfirmAsync(booking.Id, _admin, CancellationToken.None);

        var cancelled = await _service.CancelAsync(booking.Id, "Refund agreed", _admin, CancellationToken.None);

        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal("Refund agreed", cancelled.History[^1].Note);
    }

    [Fact]
    public async Task Detail_exposes_net_cost_breakdown_and_passengers()
    {
        var booking = await AwaitingPaymentAsync();

        var detail = await _service.GetAsync(booking.Id, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.NotNull(detail.NetAmount);
        Assert.NotNull(detail.Breakdown);
        var breakdown = detail.Breakdown.Value;
        Assert.Equal(detail.NetAmount, breakdown.GetProperty("NetCost").GetProperty("Amount").GetDecimal());
        Assert.Equal(detail.SellAmount, breakdown.GetProperty("SellPrice").GetProperty("Amount").GetDecimal());
        Assert.True(breakdown.GetProperty("Steps").GetArrayLength() > 0);
        Assert.Equal(2, detail.Passengers.Count);
        Assert.Equal("AB1234567", detail.Passengers[0].DocumentNumber);
        Assert.StartsWith("MOCK-", detail.ProviderBookingRef);
    }

    [Fact]
    public async Task List_filters_by_status_and_pages()
    {
        await AwaitingPaymentAsync();
        await AwaitingPaymentAsync();
        await DraftAsync();

        var all = await _service.ListAsync(new BookingListQuery(), CancellationToken.None);
        var awaiting = await _service.ListAsync(
            new BookingListQuery { Status = BookingStatus.AwaitingPayment, PageSize = 1 }, CancellationToken.None);
        var drafts = await _service.ListAsync(new BookingListQuery { Status = BookingStatus.Draft }, CancellationToken.None);
        var elsewhere = await _service.ListAsync(new BookingListQuery { AgencyId = Guid.NewGuid() }, CancellationToken.None);

        Assert.Equal(3, all.TotalCount);
        Assert.Equal(2, awaiting.TotalCount);
        Assert.Single(awaiting.Items);
        Assert.Equal("AwaitingPayment", awaiting.Items[0].Status);
        Assert.Single(drafts.Items);
        Assert.Empty(elsewhere.Items);
    }

    [Fact]
    public async Task Unknown_booking_is_not_found()
    {
        Assert.Null(await _service.GetAsync(Guid.NewGuid(), CancellationToken.None));
        await Assert.ThrowsAsync<BookingNotFoundException>(
            () => _service.ConfirmAsync(Guid.NewGuid(), _admin, CancellationToken.None));
    }
}
