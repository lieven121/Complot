using Complot.Domain.Catalog;
using Complot.Domain.Plantings;
using static Complot.Domain.Tests.Plantings.PlantingTestData;

namespace Complot.Domain.Tests.Plantings;

public sealed class PlantingScheduleTests
{
    [Fact]
    public void Pre_sown_crop_is_planted_out_after_its_weeks_indoors_and_cleared_a_week_after_harvest()
    {
        var dates = PlantingSchedule.Calculate(CherryTomato, new DateOnly(2026, 2, 20));
        Assert.Equal(new DateOnly(2026, 4, 17), dates.PlantOutOn);
        Assert.Equal(new DateOnly(2026, 6, 20), dates.HarvestFrom);
        Assert.Equal(new DateOnly(2026, 9, 12), dates.HarvestTo);
        Assert.Equal(new DateOnly(2026, 9, 19), dates.ClearOn);
    }

    [Fact]
    public void Direct_sowing_skips_the_indoor_phase() =>
        Assert.Null(PlantingSchedule.Calculate(CherryTomato, new DateOnly(2026, 5, 1), direct: true).PlantOutOn);

    [Fact]
    public void Perennials_have_no_end_date() =>
        Assert.Null(PlantingSchedule.Calculate(Strawberry, new DateOnly(2026, 8, 20)).ClearOn);
}

public sealed class PlantingPhasesTests
{
    private static readonly DateOnly From = new(2025, 1, 1);
    private static readonly DateOnly To = new(2027, 12, 31);

    [Fact]
    public void Annual_runs_through_indoor_young_growing_harvest_and_clearing()
    {
        var segments = PlantingPhases.Segments(Plant(Bed("K1", 0), CherryTomato, new DateOnly(2026, 2, 20)), From, To);
        Assert.Equal(
            [PlantingPhase.Indoor, PlantingPhase.Young, PlantingPhase.Growing, PlantingPhase.Harvest, PlantingPhase.Clearing],
            segments.Select(s => s.Phase));
        Assert.Equal(new DateOnly(2026, 4, 16), segments[0].To);
        Assert.Equal(new DateOnly(2026, 4, 17), segments[1].From);
    }

    [Fact]
    public void Segments_are_clipped_to_the_requested_range()
    {
        var segments = PlantingPhases.Segments(Plant(Bed("K1", 0), CherryTomato, new DateOnly(2026, 2, 20)), new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));
        var harvest = Assert.Single(segments);
        Assert.Equal(PlantingPhase.Harvest, harvest.Phase);
        Assert.Equal(new DateOnly(2026, 7, 1), harvest.From);
    }

    [Fact]
    public void Perennial_alternates_growing_harvest_and_winter_rest()
    {
        var segments = PlantingPhases.Segments(Plant(Bed("F1", 0), Strawberry, new DateOnly(2026, 8, 20)), From, new DateOnly(2027, 8, 31));
        Assert.Equal(
            [PlantingPhase.Growing, PlantingPhase.Dormant, PlantingPhase.Growing, PlantingPhase.Harvest, PlantingPhase.Growing],
            segments.Select(s => s.Phase));
        Assert.Equal(new DateOnly(2026, 12, 1), segments[1].From);
        Assert.Equal(new DateOnly(2027, 2, 28), segments[1].To);
    }
}

public sealed class PlantingChecksTests
{
    [Fact]
    public void Same_family_too_soon_in_the_same_bed_breaks_the_rotation()
    {
        var bed = Bed("A3", 0);
        var potato = Plant(bed, Potato, new DateOnly(2026, 4, 12));
        var tomato = Plant(bed, CherryTomato, new DateOnly(2027, 3, 1)); // other category, same family

        var warning = Assert.Single(PlantingChecks.Check([potato, tomato]), w => w.Code == "rotation");
        Assert.Equal(tomato.Id, warning.PlantingId);
        Assert.Equal("Solanaceae", warning.Params["family"]);
        Assert.Equal(potato.ClearOn!.Value.AddYears(4), warning.Params["freeFrom"]);
    }

    [Fact]
    public void Bad_neighbours_in_adjacent_beds_at_the_same_time_are_flagged_on_both()
    {
        var onions = Plant(Bed("A3", 0), Onion, new DateOnly(2026, 3, 28));
        var beans = Plant(Bed("A4", 1.7), Bean, new DateOnly(2026, 5, 20));

        var warnings = PlantingChecks.Check([onions, beans]).Where(w => w.Code == "badNeighbours").ToList();
        Assert.Equal(2, warnings.Count);
        Assert.Contains(warnings, w => w.PlantingId == onions.Id);
        Assert.Contains(warnings, w => w.PlantingId == beans.Id);
    }

    [Fact]
    public void Bad_neighbours_far_apart_are_fine()
    {
        var onions = Plant(Bed("A1", 0), Onion, new DateOnly(2026, 3, 28));
        var beans = Plant(Bed("A4", 5.1), Bean, new DateOnly(2026, 5, 20));
        Assert.DoesNotContain(PlantingChecks.Check([onions, beans]), w => w.Code == "badNeighbours");
    }

    [Fact]
    public void Sun_mismatch_is_an_info()
    {
        var warning = Assert.Single(PlantingChecks.Check([Plant(Bed("B1", 0, sun: SunExposure.Half), Bean, new DateOnly(2026, 5, 20))]));
        Assert.Equal(("sun", WarningLevel.Info), (warning.Code, warning.Level));
    }

    [Fact]
    public void Perennial_without_end_must_be_renewed_after_its_max_stay()
    {
        var warning = Assert.Single(PlantingChecks.Check([Plant(Bed("F3", 0), Strawberry, new DateOnly(2026, 8, 20))]));
        Assert.Equal("renewBy", warning.Code);
        Assert.Equal(new DateOnly(2028, 8, 20), warning.Params["date"]);
    }

    [Fact]
    public void Sowing_outside_the_window_is_reported()
    {
        var planting = Plant(Bed("A1", 0), Bean, new DateOnly(2026, 10, 1));
        Assert.Equal("outsideWindow", PlantingChecks.OutsideSowingWindow(planting)?.Code);
        Assert.Null(PlantingChecks.OutsideSowingWindow(Plant(Bed("A1", 0), Bean, new DateOnly(2026, 5, 20))));
    }
}

public sealed class SpotPlantingTests
{
    [Fact]
    public void Spot_counts_its_plants_and_uses_their_spacing()
    {
        var spot = Spot(SpotsBed("M1"), CherryTomato, new DateOnly(2026, 2, 20), 2);
        Assert.Equal(2, spot.PlantCount());
        Assert.Equal(0.7, spot.UsedArea(), 3); // 2 × 0.5 m × 0.7 m
    }

    [Fact]
    public void Spot_without_spacing_uses_the_default_area_per_plant() =>
        Assert.Equal(3 * Planting.DefaultSpotAreaM2, Spot(SpotsBed("M1"), Potato, new DateOnly(2026, 4, 12), 3).UsedArea(), 3);

    [Fact]
    public void Spot_needs_a_count_and_a_position_inside_the_bed()
    {
        var spot = Spot(SpotsBed("M1"), Onion, new DateOnly(2026, 3, 28), 4);
        Assert.Null(spot.SpotProblem());
        spot.SpotX = 1.2;
        Assert.Equal(("spotX", "spotOutside"), spot.SpotProblem());
        spot.Count = null;
        Assert.Equal(("count", "notEmpty"), spot.SpotProblem());
    }

    [Fact]
    public void Strips_bed_takes_no_spot_fields()
    {
        var planting = Plant(Bed("A1", 0), Onion, new DateOnly(2026, 3, 28));
        Assert.Null(planting.SpotProblem());
        planting.Count = 3;
        Assert.Equal(("count", "notSpotBed"), planting.SpotProblem());
    }

    [Fact]
    public void Rotation_counts_the_whole_spots_bed()
    {
        var bed = SpotsBed("M1");
        var potato = Spot(bed, Potato, new DateOnly(2026, 4, 12), 3);
        var tomato = Spot(bed, CherryTomato, new DateOnly(2027, 3, 1), 1);
        tomato.SpotX = 0.1;
        Assert.Single(PlantingChecks.Check([potato, tomato]), w => w.Code == "rotation" && w.PlantingId == tomato.Id);
    }
}
