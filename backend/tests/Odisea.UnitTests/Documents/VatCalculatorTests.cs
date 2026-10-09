using Odisea.Modules.Documents.Domain;
using Xunit;

namespace Odisea.UnitTests.Documents;

public class VatCalculatorTests
{
    [Fact]
    public void Margin_scheme_shows_no_separate_vat_and_carries_the_mandatory_wording()
    {
        var (taxBase, vat, rate, note) = VatCalculator.FromGross(831.10m, VatTreatment.MarginScheme);

        Assert.Equal(831.10m, taxBase);
        Assert.Equal(0m, vat);
        Assert.Equal(0m, rate);
        Assert.Contains("режим на облагане на маржа", note, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("чл. 142", note);
    }

    [Fact]
    public void Standard_rate_backs_out_20_percent_from_the_gross()
    {
        var (taxBase, vat, rate, _) = VatCalculator.FromGross(1200m, VatTreatment.Standard20);

        Assert.Equal(1000m, taxBase);
        Assert.Equal(200m, vat);
        Assert.Equal(20m, rate);
        Assert.Equal(1200m, taxBase + vat); // reconstructs exactly, no rounding leak
    }

    [Fact]
    public void Accommodation_rate_backs_out_9_percent()
    {
        var (taxBase, vat, rate, _) = VatCalculator.FromGross(109m, VatTreatment.Accommodation9);

        Assert.Equal(100m, taxBase);
        Assert.Equal(9m, vat);
        Assert.Equal(9m, rate);
    }

    [Theory]
    [InlineData(831.10, VatTreatment.Standard20)]
    [InlineData(999.99, VatTreatment.Standard20)]
    [InlineData(831.10, VatTreatment.Accommodation9)]
    public void Base_plus_vat_always_equals_the_gross(decimal gross, VatTreatment treatment)
    {
        var (taxBase, vat, _, _) = VatCalculator.FromGross(gross, treatment);
        Assert.Equal(gross, taxBase + vat);
    }

    [Fact]
    public void Exemption_requires_stated_grounds()
    {
        Assert.Throws<DocumentValidationException>(
            () => VatCalculator.FromGross(100m, VatTreatment.Exempt));

        var (_, vat, _, note) = VatCalculator.FromGross(100m, VatTreatment.Exempt, "чл. 113, ал. 9 ЗДДС");
        Assert.Equal(0m, vat);
        Assert.Contains("чл. 113, ал. 9 ЗДДС", note);
    }
}
