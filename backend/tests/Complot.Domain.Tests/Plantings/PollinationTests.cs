using Complot.Domain.Catalog;
using Complot.Domain.Garden;
using Complot.Domain.Plantings;
using static Complot.Domain.Tests.Plantings.PlantingTestData;

namespace Complot.Domain.Tests.Plantings;

public sealed class PollinationTests
{
    private static readonly DateOnly May = new(2026, 5, 20);

    private static readonly PlantVariety Tomatillo = new()
    {
        Name = "Tomatillo", Category = Tomatoes, Sun = [SunExposure.Full], SowOutdoor = new MonthWindow(5, 6),
        DaysToFirstHarvest = 90, HarvestWeeks = 8, Harvest = new MonthWindow(8, 10), SpacingInRowCm = 60, SpacingBetweenRowsCm = 80,
        Pollination = new Pollination(PollinationNeed.Partner, "Tomatillo", 10),
    };

    private static Tree Tree(string name, double x, PollinationNeed need, string group = "Kiwi", string? cultivar = null) =>
        new() { Name = name, Cultivar = cultivar, X = x, Y = 0, Pollination = new Pollination(need, group, 15) };

    private static List<Pollinator>? PollinatorsOf(Tree tree, params Tree[] others) =>
        PollinationCheck.PollinatorsOf(Flowering.Of(tree)!, PollinationCheck.Collect([tree, .. others], []));

    [Fact]
    public void Female_kiwi_is_pollinated_by_a_male_within_range_but_not_by_another_female()
    {
        var female = Tree("Kiwi ♀", 0, PollinationNeed.Female);
        Assert.Empty(PollinatorsOf(female, Tree("Kiwi ♀ 2", 3, PollinationNeed.Female))!);
        Assert.Empty(PollinatorsOf(female, Tree("Kiwi ♂ far", 30, PollinationNeed.Male))!);
        Assert.Equal("Kiwi ♂", Assert.Single(PollinatorsOf(female, Tree("Kiwi ♂", 6, PollinationNeed.Male))!).Partner.Name);
    }

    [Fact]
    public void Apple_needs_another_cultivar_of_its_group()
    {
        var elstar = Tree("Appel", 0, PollinationNeed.Partner, "Apple", "Elstar");
        Assert.Empty(PollinatorsOf(elstar, Tree("Appel 2", 4, PollinationNeed.Partner, "Apple", "Elstar"))!);
        Assert.Empty(PollinatorsOf(elstar, Tree("Peer", 4, PollinationNeed.Partner, "Pear", "Conference"))!);
        Assert.Single(PollinatorsOf(elstar, Tree("Appel 3", 4, PollinationNeed.SelfFertile, "apple", "Golden Delicious"))!);
    }

    [Fact]
    public void Self_fertile_and_male_plants_need_no_pollinator() =>
        Assert.Null(PollinatorsOf(Tree("Kiwi ♂", 0, PollinationNeed.Male)));

    [Fact]
    public void A_single_tomatillo_is_warned_but_a_spot_of_two_pollinates_itself()
    {
        var single = Spot(SpotsBed("M1"), Tomatillo, May, 1);
        var warning = Assert.Single(PlantingChecks.Check([single]), w => w.Code == "noPollinator");
        Assert.Equal("Tomatillo", warning.Params["group"]);

        Assert.DoesNotContain(PlantingChecks.Check([Spot(SpotsBed("M2"), Tomatillo, May, 2)]), w => w.Code == "noPollinator");
    }

    [Fact]
    public void Two_single_tomatillos_pollinate_each_other_only_while_both_are_in_the_ground()
    {
        var first = Spot(SpotsBed("M1"), Tomatillo, May, 1);
        var neighbour = Spot(SpotsBed("M2", 3), Tomatillo, May, 1);
        Assert.DoesNotContain(PlantingChecks.Check([first, neighbour]), w => w.Code == "noPollinator");

        var nextYear = Spot(SpotsBed("M3", 3), Tomatillo, May.AddYears(1), 1);
        Assert.Equal(2, PlantingChecks.Check([first, nextYear]).Count(w => w.Code == "noPollinator"));
    }

    [Fact]
    public void A_tree_on_the_site_can_pollinate_a_planting()
    {
        var kiwiVariety = new PlantVariety
        {
            Name = "Kiwibes", Category = Strawberries, Life = Life.Perennial, Sun = [SunExposure.Full], PlantOut = new MonthWindow(4, 5),
            Harvest = new MonthWindow(9, 10), Pollination = new Pollination(PollinationNeed.Female, "Kiwi", 15),
        };
        var female = Spot(SpotsBed("K1"), kiwiVariety, May, 1);
        Assert.Single(PlantingChecks.Check([female]), w => w.Code == "noMalePollinator");
        Assert.DoesNotContain(PlantingChecks.Check([female], [Tree("Kiwi ♂", 5, PollinationNeed.Male)]), w => w.Code == "noMalePollinator");
    }
}
