using Odisea.Modules.Integrations.Features.CallLogging;
using Odisea.Modules.Integrations.Infrastructure;
using Odisea.Modules.Integrations.PublicApi;

namespace Odisea.Modules.Integrations.Features;

public class ProviderRegistry(IEnumerable<IReservationProvider> adapters, IntegrationsDbContext db) : IProviderRegistry
{
    private readonly Dictionary<string, IReservationProvider> _adapters =
        adapters.ToDictionary(a => a.ProviderCode, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> KnownProviderCodes => [.. _adapters.Keys];

    public IReservationProvider Get(string providerCode) =>
        _adapters.TryGetValue(providerCode, out var adapter)
            ? new LoggingReservationProvider(adapter, db)
            : throw new UnknownProviderException(providerCode);
}
