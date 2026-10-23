using Complot.Domain.Catalog;

namespace Complot.Domain.Tests.Catalog;

public sealed class PlantVarietyTests
{
    private static readonly PlantFamily Asteraceae = new() { LatinName = "Asteraceae", NameNl = "composieten", NameEn = "daisy family", RotationRestYears = 1 };
    private static readonly PlantFamily Amaranthaceae = new() { LatinName = "Amaranthaceae", NameNl = "amarantenfamilie", NameEn = "amaranth family", RotationRestYears = 2 };

    private static PlantVariety Variety(PlantCategory category, PlantFamily? family = null) =>
        new() { Name = "Test", Category = category, Family = family, Harvest = new MonthWindow(5, 9) };

    private static PlantCategory LeafyGreens(bool exempt = false) =>
        new() { Slug = "sla", Name = "Bladgroenten", DefaultFamily = Asteraceae, RotationExempt = exempt };

    [Fact]
    public void Uses_the_category_default_family_when_not_overridden()
    {
        var lettuce = Variety(LeafyGreens());
        Assert.Same(Asteraceae, lettuce.EffectiveFamily);
        Assert.Equal(1, lettuce.RotationRestYears);
    }

    [Fact]
    public void Variety_family_overrides_the_category_default()
    {
        var spinach = Variety(LeafyGreens(), Amaranthaceae);
        Assert.Same(Amaranthaceae, spinach.EffectiveFamily);
        Assert.Equal(2, spinach.RotationRestYears);
    }

    [Fact]
    public void Exempt_categories_need_no_rotation() => Assert.Equal(0, Variety(LeafyGreens(exempt: true)).RotationRestYears);

    [Fact]
    public void Without_any_family_there_is_no_rotation() =>
        Assert.Equal(0, Variety(new PlantCategory { Slug = "x", Name = "X" }).RotationRestYears);
}
