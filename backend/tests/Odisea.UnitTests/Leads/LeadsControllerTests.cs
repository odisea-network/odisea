using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Leads.Dtos;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Infrastructure.Data;
using Odisea.UnitTests.Helpers;
using Odisea.WebAPI.Controllers;

namespace Odisea.UnitTests.Leads;

public class LeadsControllerTests
{
    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private static async Task<(AppDbContext db, Guid agencyId, string pubKey)> SeedPublication()
    {
        var db = NewDb();
        var agencyId = Guid.NewGuid();
        var collection = new Collection { AgencyId = agencyId, Name = "C", Slug = "c", Status = CollectionStatus.Published };
        db.Collections.Add(collection);
        db.Publications.Add(new Publication
        {
            Key = "pub-key-001", AgencyId = agencyId, CollectionId = collection.Id,
            Status = PublicationStatus.Published, Version = 1,
        });
        await db.SaveChangesAsync();
        return (db, agencyId, "pub-key-001");
    }

    [Fact]
    public async Task Submit_ResolvesAgencyFromPublication_AndDefaultsToInquiry()
    {
        var (db, agencyId, pubKey) = await SeedPublication();
        await using var _ = db;
        var controller = new LeadsController(db, new FakeAgencyContext());

        var req = new CreateLeadRequest(pubKey, "Ivan Petrov", "ivan@example.bg", null, "Interested!", null, null, null, null, null);
        var result = await controller.Submit(req, default);

        Assert.IsType<AcceptedResult>(result);
        var lead = await db.Leads.FirstAsync();
        Assert.Equal(agencyId, lead.AgencyId);          // resolved server-side, not trusted
        Assert.Equal(LeadKind.Inquiry, lead.Kind);
        Assert.Equal(LeadStatus.New, lead.Status);
        Assert.Equal("Ivan Petrov", lead.ContactName);
    }

    [Fact]
    public async Task Submit_WithOfferAndParty_IsBookingRequest()
    {
        var (db, _, pubKey) = await SeedPublication();
        await using var _ = db;
        var controller = new LeadsController(db, new FakeAgencyContext());

        var req = new CreateLeadRequest(pubKey, "Maria", "maria@example.bg", "+359...", null,
            OfferId: Guid.NewGuid(), PartySize: 2, PreferredDepartureDate: new DateOnly(2026, 8, 1), Nights: 7, Channel: "WebComponent");
        await controller.Submit(req, default);

        var lead = await db.Leads.FirstAsync();
        Assert.Equal(LeadKind.BookingRequest, lead.Kind);
        Assert.Equal(2, lead.PartySize);
    }

    [Fact]
    public async Task Submit_UnknownPublication_Returns404()
    {
        var (db, _, _) = await SeedPublication();
        await using var _ = db;
        var controller = new LeadsController(db, new FakeAgencyContext());

        var req = new CreateLeadRequest("does-not-exist", "X", "x@example.bg", null, null, null, null, null, null, null);
        var result = await controller.Submit(req, default);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, problem.StatusCode);
    }

    [Fact]
    public async Task Submit_MissingContact_Returns400()
    {
        var (db, _, pubKey) = await SeedPublication();
        await using var _ = db;
        var controller = new LeadsController(db, new FakeAgencyContext());

        var req = new CreateLeadRequest(pubKey, "", "", null, null, null, null, null, null, null);
        var result = await controller.Submit(req, default);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, problem.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsOnlyCallingAgencysLeads()
    {
        var (db, agencyId, pubKey) = await SeedPublication();
        await using var _ = db;
        db.Leads.AddRange(
            new Lead { AgencyId = agencyId, PublicationKey = pubKey, ContactName = "Mine", ContactEmail = "a@b.bg" },
            new Lead { AgencyId = Guid.NewGuid(), PublicationKey = "other", ContactName = "Theirs", ContactEmail = "c@d.bg" });
        await db.SaveChangesAsync();

        var controller = new LeadsController(db, new FakeAgencyContext(agencyId));
        var result = await controller.List(status: null, default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var leads = Assert.IsAssignableFrom<IEnumerable<LeadDto>>(ok.Value);
        Assert.Single(leads);
        Assert.Equal("Mine", leads.First().ContactName);
    }

    [Fact]
    public async Task SetStatus_AdvancesPipeline_AndScopesToAgency()
    {
        var (db, agencyId, pubKey) = await SeedPublication();
        await using var _ = db;
        var lead = new Lead { AgencyId = agencyId, PublicationKey = pubKey, ContactName = "X", ContactEmail = "x@y.bg", Status = LeadStatus.New };
        db.Leads.Add(lead);
        await db.SaveChangesAsync();

        var controller = new LeadsController(db, new FakeAgencyContext(agencyId));
        var result = await controller.SetStatus(lead.Id, new UpdateLeadStatusRequest("Contacted"), default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<LeadDto>(ok.Value);
        Assert.Equal("Contacted", dto.Status);
    }

    [Fact]
    public async Task SetStatus_AnotherAgencysLead_Returns404()
    {
        var (db, _, pubKey) = await SeedPublication();
        await using var _ = db;
        var lead = new Lead { AgencyId = Guid.NewGuid(), PublicationKey = pubKey, ContactName = "X", ContactEmail = "x@y.bg" };
        db.Leads.Add(lead);
        await db.SaveChangesAsync();

        var controller = new LeadsController(db, new FakeAgencyContext(Guid.NewGuid()));
        var result = await controller.SetStatus(lead.Id, new UpdateLeadStatusRequest("Contacted"), default);

        Assert.IsType<NotFoundResult>(result);
    }
}
