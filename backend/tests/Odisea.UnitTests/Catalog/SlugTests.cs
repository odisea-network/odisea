using Odisea.Modules.Catalog.Domain;
using Xunit;

namespace Odisea.UnitTests.Catalog;

public class SlugTests
{
    [Theory]
    [InlineData("Antalya Easter 2027", "antalya-easter-2027")]
    [InlineData("  Côte d'Azur: été!  ", "cote-d-azur-ete")]
    [InlineData("Анталия – Великден 2027", "antaliya-velikden-2027")]
    [InlineData("Щастие в Ямбол", "shtastie-v-yambol")]
    [InlineData("already-a-slug", "already-a-slug")]
    [InlineData("!!! ---", "")]
    public void Transliterates_and_normalises(string input, string expected) =>
        Assert.Equal(expected, Slug.From(input));

    [Fact]
    public void Cuts_at_the_maximum_length_without_a_trailing_hyphen()
    {
        var slug = Slug.From(string.Join(" ", Enumerable.Repeat("word", 40)), maxLength: 22);
        Assert.Equal("word-word-word-word", slug);
    }

    [Theory]
    [InlineData("antalya-easter-2027", true)]
    [InlineData("a", true)]
    [InlineData("Antalya", false)]
    [InlineData("two--hyphens", false)]
    [InlineData("-leading", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Validates_the_kebab_case_shape(string? value, bool valid) =>
        Assert.Equal(valid, Slug.IsValid(value));
}
