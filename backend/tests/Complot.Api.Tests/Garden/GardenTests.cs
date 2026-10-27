using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Complot.Api.Common;
using Complot.Api.Features.Catalog.Varieties;
using Complot.Api.Features.Garden;
using Complot.Api.Features.Plantings;
using Complot.Api.Tests.Infrastructure;
using Complot.Domain.Catalog;
using Complot.Domain.Garden;
using Complot.Domain.Plantings;

namespace Complot.Api.Tests.Garden;

public sealed class GardenTests(SeededApiFactory factory) : IClassFixture<SeededApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<T?> Get<T>(string url) => _client.GetFromJsonAsync<T>(url, ApiJson.Options, Ct);
    private Task<HttpResponseMessage> Post<T>(string url, T body) => _client.PostAsJsonAsync(url, body, ApiJson.Options, Ct);
    private Task<HttpResponseMessage> Put<T>(string url, T body) => _client.PutAsJsonAsync(url, body, ApiJson.Options, Ct);
    private static async Task<T> Read<T>(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<T>(ApiJson.Options, Ct))!;

    private async Task<SiteDto> Home() => (await Get<SiteDto[]>("/api/sites"))!.First(s => s.Name == "Thuis");
    private async Task<SiteLayoutDto> Layout(Guid siteId) => (await Get<SiteLayoutDto>($"/api/sites/{siteId}/layout"))!;
    private async Task<VarietyDto> Variety(string name) => (await Get<VarietyDto[]>("/api/varieties"))!.First(v => v.Name == name);

    private static async Task<string> Code(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options, Ct);
        return body.TryGetProperty("code", out var code) ? code.GetString()! : body.GetProperty("errors").EnumerateObject().First().Value[0].GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task Demo_sites_and_layout_are_seeded()
    {
        var sites = await Get<SiteDto[]>("/api/sites");
        Assert.Equal(["Thuis", "Volkstuin"], sites!.Select(s => s.Name));
        var layout = await Layout(sites![0].Id);
        Assert.Contains(layout.Beds, b => b.Code == "S1" && b.Shape.Kind == ShapeKind.Ellipse);
        Assert.Equal(3, layout.Trees.Count);
        Assert.Empty(layout.Trees.Single(t => t.Name == "Appelboom").Pollinators!); // the only apple: no partner
        Assert.Contains(layout.Markers, m => m.Kind == MarkerKind.Line && m.Shape.Height == 0);
        Assert.Equal(4.8, layout.Beds.Single(b => b.Code == "A1").Area);
    }

    [Fact]
    public async Task Layout_save_adds_updates_and_removes_items()
    {
        var site = await Read<SiteDto>(await Post("/api/sites", new SiteInput("Testtuin", null)));
        var plot = new PlotDto(Guid.NewGuid(), "Perceel", PlotType.Open, new ShapeDto(ShapeKind.Rect, 0, 0, 5, 5));
        var bed = new BedDto(Guid.NewGuid(), plot.Id, "T1", BedKind.Strips, new ShapeDto(ShapeKind.Keyhole, 1, 1, 3, 3), Domain.Catalog.SunExposure.Full, null, null, 0);
        var path = new PathDto(Guid.NewGuid(), "Pad", PathMaterial.Grass, new ShapeDto(ShapeKind.Rect, 0, 5, 5, 0.6));
        var marker = new MarkerDto(Guid.NewGuid(), MarkerKind.Line, "Muur", "#7a6a58", new ShapeDto(ShapeKind.Rect, 0, 6, 5, 0), 0.25);
        var layout = new SiteLayoutDto([plot], [bed], [path], [], [], [marker]);

        var saved = await Read<SiteLayoutDto>(await Put($"/api/sites/{site.Id}/layout", layout));
        Assert.Equal(bed.Id, Assert.Single(saved.Beds).Id);
        Assert.Equal(marker, Assert.Single(saved.Markers));

        var moved = layout with { Beds = [bed with { Shape = bed.Shape with { X = 1.5 } }], Paths = [], Markers = [] };
        saved = await Read<SiteLayoutDto>(await Put($"/api/sites/{site.Id}/layout", moved));
        Assert.Equal(1.5, Assert.Single(saved.Beds).Shape.X);
        Assert.Empty(saved.Paths);
        Assert.Empty(saved.Markers);
    }

    [Fact]
    public async Task Trees_list_their_pollinators_within_range()
    {
        var site = await Read<SiteDto>(await Post("/api/sites", new SiteInput("Kiwituin", null)));
        TreeDto Kiwi(string name, double x, PollinationNeed need) =>
            new(Guid.NewGuid(), name, null, TreeKind.Shrub, x, 0, 2, null, [], new PollinationDto(need, "Kiwi", 15));
        var female = Kiwi("Kiwi ♀", 0, PollinationNeed.Female);
        var layout = new SiteLayoutDto([], [], [], [], [female, Kiwi("Kiwi ♂ ver", 40, PollinationNeed.Male)], []);

        var saved = await Read<SiteLayoutDto>(await Put($"/api/sites/{site.Id}/layout", layout));
        var lonely = saved.Trees.Single(t => t.Id == female.Id);
        Assert.Equal(PollinationNeed.Female, lonely.Pollination!.Need);
        Assert.Empty(lonely.Pollinators!);
        Assert.Null(saved.Trees.Single(t => t.Id != female.Id).Pollinators);

        saved = await Read<SiteLayoutDto>(await Put($"/api/sites/{site.Id}/layout",
            layout with { Trees = [.. saved.Trees, Kiwi("Kiwi ♂", 6, PollinationNeed.Male)] }));
        var pollinator = Assert.Single(saved.Trees.Single(t => t.Id == female.Id).Pollinators!);
        Assert.Equal(("Kiwi ♂", 6.0), (pollinator.Name, pollinator.Distance));

        var noRange = layout with { Trees = [female with { Pollination = female.Pollination! with { RangeMeters = 0 } }] };
        Assert.Equal("inclusiveBetween", await Code(await Put($"/api/sites/{site.Id}/layout", noRange), HttpStatusCode.BadRequest));
    }

    [Fact]
    public async Task Markers_are_checked_per_kind()
    {
        var home = await Home();
        var layout = await Layout(home.Id);
        var line = new MarkerDto(Guid.NewGuid(), MarkerKind.Line, null, "#7a6a58", new ShapeDto(ShapeKind.Rect, 0, 0, 0.05, 0), 0.25);
        Assert.Equal("lineTooShort", await Code(await Put($"/api/sites/{home.Id}/layout", layout with { Markers = [line] }), HttpStatusCode.BadRequest));

        var area = new MarkerDto(Guid.NewGuid(), MarkerKind.Area, "Terras", "#d9d4c8", new ShapeDto(ShapeKind.Keyhole, 0, 0, 3, 2), 0.2);
        Assert.Equal("isInEnum", await Code(await Put($"/api/sites/{home.Id}/layout", layout with { Markers = [area] }), HttpStatusCode.BadRequest));
        Assert.Equal("color", await Code(await Put($"/api/sites/{home.Id}/layout",
            layout with { Markers = [area with { Color = "rood", Shape = area.Shape with { Kind = ShapeKind.Rect } }] }), HttpStatusCode.BadRequest));
    }

    [Fact]
    public async Task Layout_rejects_duplicate_bed_codes_and_removing_beds_in_use()
    {
        var home = await Home();
        var layout = await Layout(home.Id);

        var duplicate = layout with { Beds = [.. layout.Beds, layout.Beds[0] with { Id = Guid.NewGuid() }] };
        Assert.Equal("duplicateBedCode", await Code(await Put($"/api/sites/{home.Id}/layout", duplicate), HttpStatusCode.BadRequest));

        var withoutA1 = layout with { Beds = layout.Beds.Where(b => b.Code != "A1").ToList() };
        Assert.Equal("bed.inUse", await Code(await Put($"/api/sites/{home.Id}/layout", withoutA1), HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Plantings_come_with_phase_segments()
    {
        var home = await Home();
        var plantings = await Get<PlantingDto[]>($"/api/sites/{home.Id}/plantings?from=2026-01-01&to=2026-12-31");
        Assert.NotEmpty(plantings!);
        Assert.All(plantings!, p => Assert.All(p.Phases, s => Assert.True(s.From >= new DateOnly(2026, 1, 1) && s.To <= new DateOnly(2026, 12, 31))));
        Assert.Contains(plantings!, p => p.Phases.Any(s => s.Phase == PlantingPhase.Indoor));
    }

    [Fact]
    public async Task Site_checks_find_the_demo_rotation_and_neighbour_issues()
    {
        var warnings = await Get<PlantingWarningDto[]>($"/api/sites/{(await Home()).Id}/checks");
        Assert.Contains(warnings!, w => w.Code == "rotation" && w.Params["family"]?.ToString() == "Solanaceae");
        Assert.Contains(warnings!, w => w.Code == "badNeighbours");
        Assert.Contains(warnings!, w => w.Code == "renewBy");
    }

    [Fact]
    public async Task Planting_preview_calculates_dates_and_warnings_without_saving()
    {
        var layout = await Layout((await Home()).Id);
        var bed = layout.Beds.Single(b => b.Code == "K1");
        var tomato = await Variety("Kerstomaat");
        var input = new PlanPlantingInput(bed.Id, tomato.Id, 1, new DateOnly(2027, 2, 20), false, null);

        var preview = await Read<PlantingPreviewDto>(await Post("/api/plantings/preview", input));
        Assert.Equal(new DateOnly(2027, 4, 17), preview.Planting.PlantOutOn);
        Assert.Contains(preview.Warnings, w => w.Code == "rotation");
        Assert.Equal(18, preview.Planting.PlantCount); // 6.6 m² / (0.5 m × 0.7 m)

        var late = await Read<PlantingPreviewDto>(await Post("/api/plantings/preview", input with { SowOn = new DateOnly(2027, 9, 1) }));
        Assert.Equal("outsideWindow", late.Warnings[0].Code);

        var plantings = await Get<PlantingDto[]>($"/api/sites/{(await Home()).Id}/plantings?from=2027-01-01&to=2027-12-31");
        Assert.DoesNotContain(plantings!, p => p.BedId == bed.Id && p.SowOn == input.SowOn);
    }

    [Fact]
    public async Task Planting_can_be_created_adjusted_and_deleted()
    {
        var site = (await Get<SiteDto[]>("/api/sites"))!.Single(s => s.Name == "Volkstuin");
        var bed = (await Layout(site.Id)).Beds.Single(b => b.Code == "V3");
        var lettuce = await Variety("Pluksla");

        var created = await Read<PlantingDto>(await Post("/api/plantings", new PlanPlantingInput(bed.Id, lettuce.Id, 0.5, new DateOnly(2027, 4, 1), false, "test")));
        Assert.Equal(new DateOnly(2027, 5, 21), created.HarvestFrom);

        var bad = new UpdatePlantingInput(0.5, created.SowOn, null, new DateOnly(2027, 3, 1), new DateOnly(2027, 6, 1), null, null);
        Assert.Equal("greaterThanOrEqual", await Code(await Put($"/api/plantings/{created.Id}", bad), HttpStatusCode.BadRequest));

        var adjusted = await Read<PlantingDto>(await Put($"/api/plantings/{created.Id}",
            new UpdatePlantingInput(0.5, created.SowOn, null, new DateOnly(2027, 5, 25), new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 8), null)));
        Assert.Equal(new DateOnly(2027, 7, 8), adjusted.ClearOn);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/plantings/{created.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Demo_spots_bed_holds_counted_plants()
    {
        var home = await Home();
        var m1 = (await Layout(home.Id)).Beds.Single(b => b.Code == "M1");
        Assert.Equal(BedKind.Spots, m1.Kind);
        var plantings = await Get<PlantingDto[]>($"/api/sites/{home.Id}/plantings?from=2026-01-01&to=2026-12-31");
        var basil = plantings!.Single(p => p.BedId == m1.Id && p.Count == 3);
        Assert.Equal(3, basil.PlantCount);
        Assert.True(basil.UsedArea < 0.5);
    }

    [Fact]
    public async Task Spot_plantings_need_a_count_and_a_position_inside_the_bed()
    {
        var layout = await Layout((await Home()).Id);
        var m2 = layout.Beds.Single(b => b.Code == "M2");
        var strips = layout.Beds.Single(b => b.Code == "B3");
        var lettuce = await Variety("Pluksla");
        var input = new PlanPlantingInput(m2.Id, lettuce.Id, 1, new DateOnly(2027, 4, 1), false, null, 4, 0.5, 0.5);

        Assert.Equal("notEmpty", await Code(await Post("/api/plantings/preview", input with { Count = null }), HttpStatusCode.BadRequest));
        Assert.Equal("spotOutside", await Code(await Post("/api/plantings/preview", input with { SpotX = 1.5 }), HttpStatusCode.BadRequest));
        Assert.Equal("notSpotBed", await Code(await Post("/api/plantings/preview", input with { BedId = strips.Id }), HttpStatusCode.BadRequest));

        var created = await Read<PlantingDto>(await Post("/api/plantings", input));
        Assert.Equal(4, created.PlantCount);
        static UpdatePlantingInput ToUpdate(PlantingDto p) =>
            new(p.Share, p.SowOn, p.PlantOutOn, p.HarvestFrom, p.HarvestTo, p.ClearOn, p.Notes, p.Count, p.SpotX, p.SpotY);
        var moved = await Read<PlantingDto>(await Put($"/api/plantings/{created.Id}",
            new UpdatePlantingInput(1, created.SowOn, null, created.HarvestFrom, created.HarvestTo, created.ClearOn, null, 5, 0.9, 0.2)));
        Assert.Equal((5, 0.9, 0.2), (moved.Count!.Value, moved.SpotX!.Value, moved.SpotY!.Value));

        Assert.Equal("columnsTooMany", await Code(await Post("/api/plantings/preview", input with { SpotColumns = 5 }), HttpStatusCode.BadRequest));
        var block = await Read<PlantingDto>(await Put($"/api/plantings/{created.Id}",
            ToUpdate(moved) with { SpotColumns = 2, SpotVertical = true }));
        Assert.Equal((2, true), (block.SpotColumns!.Value, block.SpotVertical));
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/plantings/{created.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Demand_counts_the_plants_sown_in_the_period()
    {
        var home = await Home();
        var m2 = (await Layout(home.Id)).Beds.Single(b => b.Code == "M2");
        var lettuce = await Variety("Pluksla");
        var created = await Read<PlantingDto>(await Post("/api/plantings",
            new PlanPlantingInput(m2.Id, lettuce.Id, 1, new DateOnly(2029, 3, 10), false, null, 7, 0.5, 0.5)));

        var demand = await Get<PlantDemandDto[]>($"/api/sites/{home.Id}/demand?from=2029-03-01&to=2029-03-31");
        var row = Assert.Single(demand!);
        Assert.Equal((lettuce.Id, 7, 1, m2.Id), (row.VarietyId, row.Plants, row.Plantings, Assert.Single(row.BedIds)));
        Assert.Empty((await Get<PlantDemandDto[]>($"/api/sites/{home.Id}/demand?from=2029-04-01&to=2029-04-30"))!);
        await _client.DeleteAsync($"/api/plantings/{created.Id}", Ct);
    }

    [Fact]
    public async Task Kind_of_a_bed_in_use_cannot_change()
    {
        var home = await Home();
        var layout = await Layout(home.Id);
        var changed = layout with { Beds = layout.Beds.Select(b => b.Code == "A1" ? b with { Kind = BedKind.Spots } : b).ToList() };
        Assert.Equal("bed.kindInUse", await Code(await Put($"/api/sites/{home.Id}/layout", changed), HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Varieties_used_in_plantings_cannot_be_deleted() =>
        Assert.Equal("variety.inUse", await Code(await _client.DeleteAsync($"/api/varieties/{(await Variety("Aardappel")).Id}", Ct), HttpStatusCode.Conflict));
}
