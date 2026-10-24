namespace Odisea.IntegrationTests;

/// One host + one database per test run; test classes in this collection run sequentially.
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<OdiseaApiFactory>
{
    public const string Name = "api";
}
