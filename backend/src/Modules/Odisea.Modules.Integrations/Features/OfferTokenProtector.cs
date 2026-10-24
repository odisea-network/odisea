using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Odisea.Modules.Integrations.PublicApi;

namespace Odisea.Modules.Integrations.Features;

public class OfferTokenProtector : IOfferTokenProtector
{
    private readonly IDataProtector _protector;

    public OfferTokenProtector(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector("Odisea.Integrations.OfferToken");

    public string Protect(OfferEnvelope envelope) =>
        _protector.Protect(JsonSerializer.Serialize(envelope));

    public OfferEnvelope Unprotect(string publicToken)
    {
        try
        {
            return JsonSerializer.Deserialize<OfferEnvelope>(_protector.Unprotect(publicToken))
                ?? throw new InvalidOfferTokenException("protected");
        }
        catch (Exception e) when (e is CryptographicException or JsonException)
        {
            throw new InvalidOfferTokenException("protected");
        }
    }
}
