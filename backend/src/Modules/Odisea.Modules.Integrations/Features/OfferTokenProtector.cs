using Microsoft.AspNetCore.DataProtection;
using Odisea.Modules.Integrations.PublicApi;

namespace Odisea.Modules.Integrations.Features;

public class OfferTokenProtector : IOfferTokenProtector
{
    private readonly IDataProtector _protector;

    public OfferTokenProtector(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector("Odisea.Integrations.OfferToken");

    public string Protect(string providerToken) => _protector.Protect(providerToken);

    public string Unprotect(string publicToken)
    {
        try
        {
            return _protector.Unprotect(publicToken);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            throw new InvalidOfferTokenException("protected");
        }
    }
}
