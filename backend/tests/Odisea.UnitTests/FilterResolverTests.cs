using System.Text.Json;
using Odisea.Application.Catalog.Filtering;
using Odisea.Domain.Entities;
using Odisea.Domain.ValueObjects;

namespace Odisea.UnitTests;

public class FilterResolverTests
{
    [Fact]
    public void Unknown_field_throws()
    {
        var spec = new FilterSpec
        {
            All = { new FilterCondition { Field = "rating", Op = "eq", Value = JsonSerializer.SerializeToElement("5") } }
        };
        var q = new List<Offer>().AsQueryable();
        Assert.Throws<FilterValidationException>(() => FilterResolver.Apply(q, spec).ToList());
    }

    [Fact]
    public void Country_eq_filters_in_memory()
    {
        var offers = new List<Offer>
        {
            new() { Country = "GR", City = "Athens", Price = 500 },
            new() { Country = "TR", City = "Istanbul", Price = 400 },
        }.AsQueryable();

        var spec = new FilterSpec
        {
            All = { new FilterCondition { Field = "country", Op = "eq", Value = JsonSerializer.SerializeToElement("GR") } }
        };

        var result = FilterResolver.Apply(offers, spec).ToList();
        Assert.Single(result);
        Assert.Equal("GR", result[0].Country);
    }
}
