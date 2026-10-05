using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Odisea.Modules.Booking;

public static class BookingModule
{
    public static IServiceCollection AddBookingModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Module registrations (DbContext, feature services) land here as features arrive.
        return services;
    }
}
