using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace Odisea.UnitTests.Architecture;

public class ModuleBoundaryTests
{
    private static readonly string[] ModuleNames =
        ["Agencies", "Catalog", "Pricing", "Booking", "Integrations"];

    private static readonly string[] InternalSegments =
        ["Domain", "Features", "Infrastructure", "Controllers"];

    public static TheoryData<string, string> ModulePairs()
    {
        var pairs = new TheoryData<string, string>();
        foreach (var source in ModuleNames)
            foreach (var target in ModuleNames.Where(t => t != source))
                pairs.Add(source, target);
        return pairs;
    }

    [Theory]
    [MemberData(nameof(ModulePairs))]
    public void Module_may_only_reference_another_modules_public_api(string source, string target)
    {
        var sourceAssembly = Assembly.Load($"Odisea.Modules.{source}");
        var forbidden = InternalSegments
            .Select(segment => $"Odisea.Modules.{target}.{segment}")
            .ToArray();

        var offenders = Types.InAssembly(sourceAssembly)
            .That().ResideInNamespaceStartingWith($"Odisea.Modules.{source}")
            .And().HaveDependencyOnAny(forbidden)
            .GetTypes()
            .ToList();

        Assert.True(offenders.Count == 0,
            $"{source} must only reference {target}.PublicApi, but these types reach inside: " +
            string.Join(", ", offenders.Select(t => t.FullName)));
    }
}
