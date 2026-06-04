using System.Text.Json;
using Odisea.Application.Catalog.Filtering;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
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
        var offers = TwoCountries().AsQueryable();

        var spec = new FilterSpec
        {
            All = { Cond("country", "eq", "GR") }
        };

        var result = FilterResolver.Apply(offers, spec).ToList();
        Assert.Single(result);
        Assert.Equal("GR", result[0].Country);
    }

    [Fact]
    public void Flat_all_still_works_unchanged()
    {
        // Backwards-compat sentinel: exactly mirrors a pre-Increment-2 FilterSpec
        // with no Any/Groups fields populated.
        var offers = new List<Offer>
        {
            new() { Country = "GR", City = "Athens", Price = 500, BoardBasis = BoardBasis.HalfBoard },
            new() { Country = "GR", City = "Athens", Price = 900, BoardBasis = BoardBasis.HalfBoard },
            new() { Country = "TR", City = "Istanbul", Price = 400, BoardBasis = BoardBasis.HalfBoard },
        }.AsQueryable();

        var spec = new FilterSpec
        {
            All =
            {
                Cond("country", "eq", "GR"),
                Cond("maxPrice", "lte", 700m),
            }
        };

        var result = FilterResolver.Apply(offers, spec).ToList();
        Assert.Single(result);
        Assert.Equal("Athens", result[0].City);
        Assert.Equal(500m, result[0].Price);
    }

    [Fact]
    public void Any_at_root_matches_if_any_child_matches()
    {
        var offers = ThreeCountries().AsQueryable();

        var spec = new FilterSpec
        {
            Any =
            {
                Cond("country", "eq", "GR"),
                Cond("country", "eq", "EG"),
            }
        };

        var result = FilterResolver.Apply(offers, spec).ToList();
        Assert.Equal(2, result.Count);
        Assert.Contains(result, o => o.Country == "GR");
        Assert.Contains(result, o => o.Country == "EG");
    }

    [Fact]
    public void All_group_matches_only_when_all_children_match()
    {
        var offers = new List<Offer>
        {
            new() { Country = "GR", BoardBasis = BoardBasis.AllInclusive, Price = 800 },
            new() { Country = "GR", BoardBasis = BoardBasis.HalfBoard,    Price = 800 },
            new() { Country = "TR", BoardBasis = BoardBasis.AllInclusive, Price = 800 },
        }.AsQueryable();

        var spec = new FilterSpec
        {
            Groups =
            {
                new FilterGroup
                {
                    Op = "all",
                    Conditions =
                    {
                        Cond("country", "eq", "GR"),
                        Cond("board", "eq", "AllInclusive"),
                    }
                }
            }
        };

        var result = FilterResolver.Apply(offers, spec).ToList();
        Assert.Single(result);
        Assert.Equal("GR", result[0].Country);
        Assert.Equal(BoardBasis.AllInclusive, result[0].BoardBasis);
    }

    [Fact]
    public void Any_group_matches_when_one_child_matches()
    {
        var offers = new List<Offer>
        {
            new() { Country = "GR", City = "Athens", Price = 500 },
            new() { Country = "GR", City = "Santorini", Price = 500 },
            new() { Country = "GR", City = "Crete", Price = 500 },
        }.AsQueryable();

        var spec = new FilterSpec
        {
            Groups =
            {
                new FilterGroup
                {
                    Op = "any",
                    Conditions =
                    {
                        Cond("city", "eq", "Athens"),
                        Cond("city", "eq", "Santorini"),
                    }
                }
            }
        };

        var result = FilterResolver.Apply(offers, spec).ToList();
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, o => o.City == "Crete");
    }

    [Fact]
    public void Root_all_combined_with_any_group_is_anded()
    {
        // country=GR AND (board=AllInclusive OR maxPrice<=600)
        var offers = new List<Offer>
        {
            new() { Country = "GR", BoardBasis = BoardBasis.AllInclusive, Price = 1200 }, // match (board)
            new() { Country = "GR", BoardBasis = BoardBasis.HalfBoard,    Price = 500 },  // match (price)
            new() { Country = "GR", BoardBasis = BoardBasis.HalfBoard,    Price = 1200 }, // miss
            new() { Country = "EG", BoardBasis = BoardBasis.AllInclusive, Price = 500 },  // miss (country)
        }.AsQueryable();

        var spec = new FilterSpec
        {
            All = { Cond("country", "eq", "GR") },
            Groups =
            {
                new FilterGroup
                {
                    Op = "any",
                    Conditions =
                    {
                        Cond("board", "eq", "AllInclusive"),
                        Cond("maxPrice", "lte", 600m),
                    }
                }
            }
        };

        var result = FilterResolver.Apply(offers, spec).ToList();
        Assert.Equal(2, result.Count);
        Assert.All(result, o => Assert.Equal("GR", o.Country));
    }

    [Fact]
    public void Nested_groups_two_levels()
    {
        // all{ country=GR, any{ board=AI, all{ maxPrice<=500, transport=Bus } } }
        var offers = new List<Offer>
        {
            new() { Country = "GR", BoardBasis = BoardBasis.AllInclusive, Transport = Transport.Plane, Price = 1200 }, // match (board)
            new() { Country = "GR", BoardBasis = BoardBasis.HalfBoard,    Transport = Transport.Bus,   Price = 400 },  // match (nested all)
            new() { Country = "GR", BoardBasis = BoardBasis.HalfBoard,    Transport = Transport.Bus,   Price = 800 },  // miss (price too high)
            new() { Country = "GR", BoardBasis = BoardBasis.HalfBoard,    Transport = Transport.Plane, Price = 400 },  // miss (wrong transport)
            new() { Country = "EG", BoardBasis = BoardBasis.AllInclusive, Transport = Transport.Bus,   Price = 400 },  // miss (country)
        }.AsQueryable();

        var spec = new FilterSpec
        {
            All = { Cond("country", "eq", "GR") },
            Groups =
            {
                new FilterGroup
                {
                    Op = "any",
                    Conditions = { Cond("board", "eq", "AllInclusive") },
                    Groups =
                    {
                        new FilterGroup
                        {
                            Op = "all",
                            Conditions =
                            {
                                Cond("maxPrice", "lte", 500m),
                                Cond("transport", "eq", "Bus"),
                            }
                        }
                    }
                }
            }
        };

        var result = FilterResolver.Apply(offers, spec).ToList();
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Unknown_group_op_throws()
    {
        var spec = new FilterSpec
        {
            Groups = { new FilterGroup { Op = "xor", Conditions = { Cond("country", "eq", "GR") } } }
        };

        var ex = Assert.Throws<FilterValidationException>(() =>
            FilterResolver.Apply(new List<Offer>().AsQueryable(), spec).ToList());
        Assert.Contains("Unknown group op", ex.Message);
    }

    [Fact]
    public void Max_depth_exceeded_throws()
    {
        // Build a chain of 5 nested groups (depth 5, MaxDepth = 4).
        var leaf = new FilterGroup { Op = "all", Conditions = { Cond("country", "eq", "GR") } };
        var group = leaf;
        for (var i = 0; i < 4; i++)
            group = new FilterGroup { Op = "all", Groups = { group } };

        var spec = new FilterSpec { Groups = { group } };

        Assert.Throws<FilterValidationException>(() =>
            FilterResolver.Apply(new List<Offer>().AsQueryable(), spec).ToList());
    }

    [Fact]
    public void Max_node_count_exceeded_throws()
    {
        // 65 leaf conditions on the root All bucket → exceeds MaxNodeCount = 64.
        var spec = new FilterSpec();
        for (var i = 0; i < 65; i++)
            spec.All.Add(Cond("country", "eq", "GR"));

        Assert.Throws<FilterValidationException>(() =>
            FilterResolver.Apply(new List<Offer>().AsQueryable(), spec).ToList());
    }

    [Fact]
    public void Empty_group_is_noop()
    {
        var offers = ThreeCountries().AsQueryable();

        var spec = new FilterSpec
        {
            Groups = { new FilterGroup { Op = "all" } }
        };

        var result = FilterResolver.Apply(offers, spec).ToList();
        Assert.Equal(offers.Count(), result.Count);
    }

    [Fact]
    public void Group_with_unknown_field_throws()
    {
        var spec = new FilterSpec
        {
            Groups =
            {
                new FilterGroup
                {
                    Op = "all",
                    Conditions = { Cond("rating", "eq", "5") }
                }
            }
        };

        Assert.Throws<FilterValidationException>(() =>
            FilterResolver.Apply(new List<Offer>().AsQueryable(), spec).ToList());
    }

    [Fact]
    public void Case_insensitive_op_accepted()
    {
        var offers = ThreeCountries().AsQueryable();

        var spec = new FilterSpec
        {
            Groups =
            {
                new FilterGroup
                {
                    Op = "ANY",
                    Conditions =
                    {
                        Cond("country", "eq", "GR"),
                        Cond("country", "eq", "TR"),
                    }
                }
            }
        };

        var result = FilterResolver.Apply(offers, spec).ToList();
        Assert.Equal(2, result.Count);
    }

    // ---- helpers ----

    private static FilterCondition Cond(string field, string op, object value) =>
        new() { Field = field, Op = op, Value = JsonSerializer.SerializeToElement(value) };

    private static List<Offer> TwoCountries() => new()
    {
        new() { Country = "GR", City = "Athens", Price = 500 },
        new() { Country = "TR", City = "Istanbul", Price = 400 },
    };

    private static List<Offer> ThreeCountries() => new()
    {
        new() { Country = "GR", City = "Athens", Price = 500 },
        new() { Country = "TR", City = "Istanbul", Price = 400 },
        new() { Country = "EG", City = "Cairo", Price = 600 },
    };
}
