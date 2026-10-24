using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Odisea.Modules.Agencies.Infrastructure;

namespace Odisea.IntegrationTests;

/// Offer tokens (and, later, encrypted passenger documents) are only readable while the
/// DataProtection key ring exists, so it must live in the database, not on local disk.
[Collection(ApiCollection.Name)]
public class DataProtectionKeyPersistenceTests(OdiseaApiFactory factory)
{
    [Fact]
    public async Task Protecting_an_offer_token_persists_the_key_ring_in_the_agencies_schema()
    {
        // Search protects every offer token, which forces the key ring to load and, on a
        // fresh database, to generate its first key.
        await AgencyFlow.SearchAsync(factory.ClientAs(TestIdentity.Agent));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgenciesDbContext>();
        var keys = await db.DataProtectionKeys.AsNoTracking().ToListAsync();

        Assert.NotEmpty(keys);
        Assert.All(keys, key =>
        {
            Assert.False(string.IsNullOrWhiteSpace(key.FriendlyName));
            Assert.Contains("<key", key.Xml);
        });
    }
}
