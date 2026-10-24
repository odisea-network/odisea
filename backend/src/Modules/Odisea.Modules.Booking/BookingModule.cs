using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Odisea.Modules.Booking.Features.Bookings;
using Odisea.Modules.Booking.Infrastructure;

namespace Odisea.Modules.Booking;

public static class BookingModule
{
    public static IServiceCollection AddBookingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<BookingDbContext>(options => options
            .UseNpgsql(
                configuration.GetConnectionString("Default"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", BookingDbContext.Schema))
            .UseSnakeCaseNamingConvention());

        services.AddOptions<BookingOptions>()
            .Bind(configuration.GetSection(BookingOptions.SectionName))
            .Validate(
                o => o.PaymentOptionDays > 0 && o.PaymentDueDaysBeforeCheckIn >= 0 && o.MinimumPaymentWindowHours > 0,
                "Booking: PaymentOptionDays and MinimumPaymentWindowHours must be positive, PaymentDueDaysBeforeCheckIn non-negative.")
            .ValidateOnStart();

        services.AddScoped<IBookingRefGenerator, PostgresBookingRefGenerator>();
        services.AddScoped<BookingService>();
        services.AddScoped<OperatorBookingService>();
        services.AddScoped<PublicApi.IBookingLookup, BookingLookup>();

        return services;
    }
}
