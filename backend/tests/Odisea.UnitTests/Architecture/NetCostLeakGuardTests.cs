using System.Reflection;
using Odisea.Modules.Booking.Features.Bookings;
using Odisea.Modules.Catalog.Features.Search;
using Xunit;

namespace Odisea.UnitTests.Architecture;

/// The provider's net cost is a trade secret: agencies must only ever see the
/// sell price and their commission. This guard fails the build if an
/// agency-facing DTO grows a field that smells like the net or the breakdown.
public class NetCostLeakGuardTests
{
    private static readonly Type[] AgencyFacingDtos =
    [
        typeof(SearchResponse),
        typeof(HotelOfferDto),
        typeof(BookingDto),
        typeof(RePriceResponse),
        typeof(PassengerDto),
        typeof(StatusHistoryDto),
    ];

    private static readonly string[] ForbiddenFragments = ["Net", "Breakdown", "Margin", "Cost"];

    public static TheoryData<Type> Dtos()
    {
        var data = new TheoryData<Type>();
        foreach (var type in AgencyFacingDtos)
            data.Add(type);
        return data;
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
