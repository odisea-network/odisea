using System.Net.Http.Json;
using Odisea.Modules.Booking.Domain;
using Odisea.Modules.Booking.Features.Bookings;

namespace Odisea.IntegrationTests;

/// Operator-side calls over HTTP. Callers assert the status codes they expect.
public static class OperatorFlow
{
    public static async Task<OperatorBookingDto> GetAsync(HttpClient op, Guid bookingId)
    {
        var detail = await op.GetFromJsonAsync<OperatorBookingDto>($"/api/v1/admin/bookings/{bookingId}", Json.Options);
        Assert.NotNull(detail);
        return detail;
    }

    public static Task<HttpResponseMessage> RecordPaymentAsync(
        HttpClient op, Guid bookingId, decimal amount, string currency = "EUR") =>
        op.PostAsJsonAsync($"/api/v1/admin/bookings/{bookingId}/payments",
            new RecordPaymentRequest(amount, currency, PaymentMethod.BankTransfer, new DateOnly(2026, 10, 1), "BANK-REF-1"),
            Json.Options);

    public static Task<HttpResponseMessage> ConfirmAsync(HttpClient op, Guid bookingId) =>
        op.PostAsync($"/api/v1/admin/bookings/{bookingId}/confirm", null);

    public static Task<HttpResponseMessage> VoidPaymentAsync(HttpClient op, Guid bookingId, Guid paymentId) =>
        op.PostAsJsonAsync($"/api/v1/admin/bookings/{bookingId}/payments/{paymentId}/void",
            new VoidPaymentRequest("Recorded against the wrong reservation"), Json.Options);

    public static async Task<OperatorBookingDto> ReadAsync(HttpResponseMessage response)
    {
        var detail = await response.Content.ReadFromJsonAsync<OperatorBookingDto>(Json.Options);
        Assert.NotNull(detail);
        return detail;
    }
}
