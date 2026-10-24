using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using Odisea.Modules.Notifications.Features.Outbox;
using Odisea.Modules.Notifications.Infrastructure;
using Odisea.Modules.Notifications.PublicApi;

namespace Odisea.Modules.Notifications;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<NotificationsDbContext>(options => options
            .UseNpgsql(
                configuration.GetConnectionString("Default"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", NotificationsDbContext.Schema))
            .UseSnakeCaseNamingConvention());

        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => !o.SmtpConfigured || !string.IsNullOrWhiteSpace(o.From),
                "Email:From is required when Email:Smtp:Host is set.")
            .Validate(o => string.IsNullOrWhiteSpace(o.From) || (MailboxAddress.TryParse(o.From, out var from) && from.Address == o.From),
                "Email:From must be a plain mailbox address (name@host).")
            .ValidateOnStart();

        services.AddScoped<IEmailOutbox, EmailOutbox>();
        services.AddScoped<EmailDispatchRun>();

        // Decided once at startup: real SMTP when a host is configured, otherwise the log.
        if (string.IsNullOrWhiteSpace(configuration[$"{EmailOptions.SectionName}:Smtp:Host"]))
            services.AddSingleton<IEmailTransport, LoggingEmailTransport>();
        else
            services.AddSingleton<IEmailTransport, SmtpEmailTransport>();

        services.AddHostedService<EmailDispatcher>();
        return services;
    }
}
