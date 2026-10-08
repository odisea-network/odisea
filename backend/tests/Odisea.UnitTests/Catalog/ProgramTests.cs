using Odisea.Modules.Catalog.Domain;
using Xunit;

namespace Odisea.UnitTests.Catalog;

public class ProgramTests
{
    private static Program Draft() => new()
    {
        Name = "Antalya Easter 2027",
        DestinationId = Guid.NewGuid(),
        ProviderCode = "mock",
    };

    [Fact]
    public void Publish_requires_a_departure_and_a_hotel()
    {
        var program = Draft();
        Assert.Throws<InvalidProgramStateException>(program.Publish);

        program.Departures.Add(new ProgramDeparture
        {
            ProgramId = program.Id,
            StartDate = new DateOnly(2027, 3, 30),
            EndDate = new DateOnly(2027, 4, 6),
        });
        Assert.Throws<InvalidProgramStateException>(program.Publish);

        program.Hotels.Add(new ProgramHotel { ProgramId = program.Id, HotelId = Guid.NewGuid() });
        program.Publish();
        Assert.Equal(ProgramStatus.Published, program.Status);
    }

    [Fact]
    public void Archived_program_cannot_be_republished()
    {
        var program = Draft();
        program.Archive();
        Assert.Throws<InvalidProgramStateException>(program.Publish);
    }
}
