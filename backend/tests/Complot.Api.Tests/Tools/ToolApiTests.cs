using System.Net;
using System.Net.Http.Json;
using Complot.Api.Features.Catalog.Varieties;
using Complot.Api.Features.Tools;
using Complot.Api.Features.Work;
using Complot.Api.Tests.Infrastructure;
using Complot.Domain.Tools;
using Complot.Domain.Work;

namespace Complot.Api.Tests.Tools;

public sealed class ToolApiTests(SeededApiFactory factory) : IClassFixture<SeededApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<T>(ApiJson.Options, Ct))!;
    }

    private async Task<T> Get<T>(string url) => await Read<T>(await _client.GetAsync(url, Ct));

    private static ToolInput Input(string name, int? interval = null, DateOnly? bought = null) =>
        new(name, "Test", "🧰", "stuks", 2, 0, 0, null, false, bought, 5m, null, 10, interval, 25, [], null);

    private async Task<ToolDto> Create(ToolInput input) => await Read<ToolDto>(await _client.PostAsJsonAsync("/api/tools", input, ApiJson.Options, Ct));

    [Fact]
    public async Task Demo_tools_are_seeded_with_beds_log_and_dates()
    {
        var drip = (await Get<ToolDto[]>("/api/tools")).Single(t => t.Name == "Druppelslang 16 mm");
        Assert.Equal((7, 1, 0, 3), (drip.QtyGood, drip.QtyRepair, drip.QtyWorn, drip.BedIds.Count));
        Assert.Equal(new DateOnly(2026, 4, 10), drip.LastMaintenance);
        Assert.Equal(new DateOnly(2026, 10, 10), drip.NextMaintenance);
        Assert.Equal(new DateOnly(2033, 3, 5), drip.ReplaceBy);
    }

    [Fact]
    public async Task Demand_comes_from_the_plan()
    {
        var tools = await Get<ToolDto[]>("/api/tools");
        var demand = await Get<ToolDemandDto[]>("/api/tools/demand?year=2026");
        var spiral = tools.Single(t => t.Name == "Tomatenspiraal 180 cm");
        Assert.Contains(demand, d => d.ToolId == spiral.Id && d.Quantity > 0 && d.Month is >= 4 and <= 10);
    }

    [Fact]
    public async Task Maintenance_task_check_off_logs_the_maintenance()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var tool = await Create(Input("Testsnoeischaar", interval: 6, bought: today.AddMonths(-7)));
        var range = $"from={today.AddMonths(-2):yyyy-MM-dd}&to={today.AddMonths(2):yyyy-MM-dd}";

        var task = (await Get<TaskDto[]>($"/api/tasks?{range}")).Single(t => t.Kind == TaskKind.Maintenance && t.Subject == "Testsnoeischaar");
        Assert.Equal(25, task.BaseMinutes);
        Assert.Null(task.SiteId);

        var key = Uri.EscapeDataString(task.Key);
        await Read<TaskDto>(await _client.PostAsJsonAsync($"/api/tasks/{key}/complete", new CompleteTaskInput(30, today), ApiJson.Options, Ct));
        var done = (await Get<ToolDto[]>("/api/tools")).Single(t => t.Id == tool.Id);
        Assert.Equal(today, done.LastMaintenance);
        Assert.Equal(today.AddMonths(6), done.NextMaintenance);
        Assert.Contains(done.Log, e => e is { IsMaintenance: true, FromTask: true });
        Assert.Contains(await Get<TaskDto[]>($"/api/tasks?{range}"), t => t.Key == task.Key && t.Done);

        await Read<TaskDto>(await _client.PostAsync($"/api/tasks/{key}/reopen", null, Ct));
        Assert.Empty((await Get<ToolDto[]>("/api/tools")).Single(t => t.Id == tool.Id).Log);
    }

    [Fact]
    public async Task Log_entries_can_be_added_and_removed()
    {
        var tool = await Create(Input("Testgieter"));
        var logged = await Read<ToolDto>(await _client.PostAsJsonAsync($"/api/tools/{tool.Id}/log",
            new ToolLogInput(new DateOnly(2026, 5, 1), "Tuit vervangen", 4.5m, false), ApiJson.Options, Ct));
        var entry = Assert.Single(logged.Log);
        Assert.Equal(("Tuit vervangen", 4.5m, false), (entry.Text, entry.Cost, entry.IsMaintenance));
        Assert.Empty((await Read<ToolDto>(await _client.DeleteAsync($"/api/tools/{tool.Id}/log/{entry.Id}", Ct))).Log);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync($"/api/tools/{tool.Id}/log",
            new ToolLogInput(new DateOnly(2026, 5, 1), "", null, false), ApiJson.Options, Ct)).StatusCode);
    }

    [Fact]
    public async Task Varieties_need_tools_and_lose_the_need_when_the_tool_goes()
    {
        var tool = await Create(Input("Testnet"));
        var variety = (await Get<VarietyDto[]>("/api/varieties")).First(v => v.Name == "Pluksla");
        var input = new VarietyInput(variety.CategoryId, variety.FamilyId, variety.Name, variety.Cultivar, variety.LatinName, variety.Life,
            variety.SowDepthCm, variety.SpacingInRowCm, variety.SpacingBetweenRowsCm, variety.HeightCm, [.. variety.Sun], variety.SowIndoor,
            variety.WeeksIndoor, variety.SowOutdoor, variety.PlantOut, variety.Harvest, variety.Germination, variety.DaysToFirstHarvest,
            variety.HarvestWeeks, variety.MaxYearsInBed, variety.Notes, [.. variety.Actions],
            [.. variety.Suppliers.Select(s => new VarietySupplierInput(s.SupplierId, s.Price, s.Unit, s.Url))],
            [new ToolNeedDto(tool.Id, NeedPer.Bed, 1)]);
        var updated = await Read<VarietyDto>(await _client.PutAsJsonAsync($"/api/varieties/{variety.Id}", input, ApiJson.Options, Ct));
        Assert.Equal([new ToolNeedDto(tool.Id, NeedPer.Bed, 1)], updated.ToolNeeds);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/tools/{tool.Id}", Ct)).StatusCode);
        Assert.Empty((await Get<VarietyDto>($"/api/varieties/{variety.Id}")).ToolNeeds);
    }

    [Fact]
    public async Task Tool_needs_a_name_and_sensible_numbers() =>
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/tools", Input("") with { QtyGood = -1 }, ApiJson.Options, Ct)).StatusCode);
}
