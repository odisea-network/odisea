using System.Reflection;
using Odisea.Modules.Booking.Features.Bookings;
using Odisea.Modules.Catalog.Features.Search;
using Xunit;

namespace Odisea.UnitTests.Architecture;

/// The provider's net cost is a trade secret: agencies must only ever see the
/// sell price and their commission. This guard fails the build if an
/// agency-facing DTO grows a field that smells like the net or the breakdown.
/// It discovers DTOs by convention so new ones are covered automatically;
/// operator-only shapes are exempt by their "Operator" name prefix.
public class NetCostLeakGuardTests
{
    private static readonly string[] AgencyFacingNamespaces =
    [
        typeof(BookingDto).Namespace!,
        typeof(SearchResponse).Namespace!,
    ];

    private static readonly string[] ForbiddenFragments = ["Net", "Breakdown", "Margin", "Cost"];

    private static IEnumerable<Type> AgencyFacingDtos() =>
        new[] { typeof(BookingDto).Assembly, typeof(SearchResponse).Assembly }
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsPublic && !t.IsGenericTypeDefinition && !t.IsAbstract)
            .Where(t => AgencyFacingNamespaces.Contains(t.Namespace))
            .Where(t => t.Name.EndsWith("Dto", StringComparison.Ordinal)
                        || t.Name.EndsWith("Response", StringComparison.Ordinal))
            .Where(t => !t.Name.StartsWith("Operator", StringComparison.Ordinal))
            .OrderBy(t => t.FullName);

    public static TheoryData<Type> Dtos()
    {
        var data = new TheoryData<Type>();
        foreach (var type in AgencyFacingDtos())
            data.Add(type);
        return data;
    }

    [Fact]
    public void The_scan_still_finds_the_known_agency_dtos()
    {
        var found = AgencyFacingDtos().ToList();
        Assert.Contains(typeof(BookingDto), found);
        Assert.Contains(typeof(PassengerDto), found);
        Assert.Contains(typeof(StatusHistoryDto), found);
        Assert.Contains(typeof(RePriceResponse), found);
        Assert.Contains(typeof(SearchResponse), found);
        Assert.Contains(typeof(HotelOfferDto), found);
        Assert.DoesNotContain(typeof(OperatorBookingDto), found);
    }

    [Theory]
    [MemberData(nameof(Dtos))]
    public void Agency_facing_dto_exposes_no_net_cost(Type dto)
    {
        var offending = dto.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => ForbiddenFragments.Any(f =>
                p.Name.Contains(f, StringComparison.OrdinalIgnoreCase)))
            .Select(p => p.Name)
            .ToList();

        Assert.True(offending.Count == 0,
            $"{dto.Name} exposes suspicious fields to agencies: {string.Join(", ", offending)}");
    }
}
