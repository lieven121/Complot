using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Complot.Api.Common;
using Complot.Api.Features.Catalog.Categories;
using Complot.Api.Features.Catalog.Exchange;
using Complot.Api.Features.Catalog.Inventory;
using Complot.Api.Features.Catalog.Suppliers;
using Complot.Api.Features.Catalog.Varieties;
using Complot.Api.Tests.Infrastructure;
using Complot.Domain.Catalog;

namespace Complot.Api.Tests.Catalog;

public sealed class CatalogTests(SeededApiFactory factory) : IClassFixture<SeededApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<T?> Get<T>(string url) => _client.GetFromJsonAsync<T>(url, ApiJson.Options, Ct);
    private Task<HttpResponseMessage> Post<T>(string url, T body) => _client.PostAsJsonAsync(url, body, ApiJson.Options, Ct);
    private Task<HttpResponseMessage> Put<T>(string url, T body) => _client.PutAsJsonAsync(url, body, ApiJson.Options, Ct);
    private static async Task<T> Read<T>(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<T>(ApiJson.Options, Ct))!;

    private async Task<VarietyDto> Variety(string name, string? cultivar = null) =>
        (await Get<VarietyDto[]>("/api/varieties"))!.Single(v => v.Name == name && (cultivar is null || v.Cultivar == cultivar));

    private async Task<CategoryDto> Category(string slug) => (await Get<CategoryDto[]>("/api/categories"))!.Single(c => c.Slug == slug);

    private static VarietyInput InputFor(Guid categoryId, string name) => new(
        categoryId, null, name, "Test", null, Life.Annual, 1, 10, 30, 40, [SunExposure.Full],
        null, null, new MonthWindowDto(4, 6), null, new MonthWindowDto(7, 9), new DayRangeDto(7, 14), 70, 6, 1, null, [], []);

    private static async Task<Dictionary<string, ErrorDetail[]>> ValidationErrors(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options, Ct);
        return problem.GetProperty("errors").Deserialize<Dictionary<string, ErrorDetail[]>>(ApiJson.Options)!;
    }

    private static async Task<string> ConflictCode(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options, Ct)).GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task Demo_catalog_is_seeded()
    {
        Assert.True((await Get<object[]>("/api/families"))!.Length >= 12);
        Assert.True((await Get<CategoryDto[]>("/api/categories"))!.Length >= 12);
        Assert.True((await Get<VarietyDto[]>("/api/varieties"))!.Length >= 26);
        Assert.True((await Get<InventoryLotDto[]>("/api/inventory-lots"))!.Length >= 21);
    }

    [Fact]
    public async Task Search_matches_name_cultivar_and_latin_name_case_insensitively()
    {
        var tomatoes = await Get<VarietyDto[]>("/api/varieties?q=TOMAAT");
        Assert.Contains(tomatoes!, v => v.Cultivar == "Black Krim");
        var byLatin = await Get<VarietyDto[]>("/api/varieties?q=pastinaca");
        Assert.Equal("Pastinaak", Assert.Single(byLatin!).Name);
    }

    [Fact]
    public async Task Rotation_rest_comes_from_the_effective_botanical_family()
    {
        Assert.Equal(1, (await Variety("Pluksla")).RotationRestYears);   // Asteraceae via the category
        Assert.Equal(2, (await Variety("Spinazie")).RotationRestYears);  // Amaranthaceae override
        Assert.Equal(0, (await Variety("Goudsbloem")).RotationRestYears); // herbs & flowers are exempt
        Assert.Equal(4, (await Variety("Kerstomaat")).RotationRestYears);
    }

    [Fact]
    public async Task Invalid_variety_returns_error_codes_with_parameters()
    {
        var category = await Category("wortel");
        var input = InputFor(category.Id, "") with { Harvest = new MonthWindowDto(7, 13), SowOutdoor = null };

        var errors = await ValidationErrors(await Post("/api/varieties", input));

        Assert.Equal("notEmpty", errors["name"][0].Code);
        var month = errors["harvest.to"][0];
        Assert.Equal("inclusiveBetween", month.Code);
        Assert.Equal(12, ((JsonElement)month.Params!["to"]!).GetInt32());
        Assert.Equal("noSowingWindow", errors["sowOutdoor"][0].Code);
    }

    [Fact]
    public async Task Variety_can_be_created_updated_with_suppliers_and_deleted()
    {
        var category = await Category("sla");
        var supplier = (await Get<SupplierDto[]>("/api/suppliers"))!.First(s => s.Name == "De Bolster");

        var created = await Read<VarietyDto>(await Post("/api/varieties", InputFor(category.Id, "Veldsla")));
        Assert.Equal(1, created.RotationRestYears);

        var update = InputFor(category.Id, "Veldsla") with { Suppliers = [new VarietySupplierInput(supplier.Id, 2.10m, "zakje 5 g", null)] };
        var updated = await Read<VarietyDto>(await Put($"/api/varieties/{created.Id}", update));
        Assert.Equal("De Bolster", Assert.Single(updated.Suppliers).SupplierName);

        // Updating again with the same supplier must update in place, not re-insert.
        var again = await Read<VarietyDto>(await Put($"/api/varieties/{created.Id}", update with { Suppliers = [new VarietySupplierInput(supplier.Id, 1.95m, null, null)] }));
        Assert.Equal(1.95m, Assert.Single(again.Suppliers).Price);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/varieties/{created.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/varieties/{created.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Things_in_use_cannot_be_deleted()
    {
        Assert.Equal("category.inUse", await ConflictCode(await _client.DeleteAsync($"/api/categories/{(await Category("tomaat")).Id}", Ct)));
        Assert.Equal("variety.hasInventory", await ConflictCode(await _client.DeleteAsync($"/api/varieties/{(await Variety("Oranje wortel")).Id}", Ct)));
    }

    [Fact]
    public async Task Duplicate_category_slug_is_a_conflict()
    {
        var input = new CategoryInput("wortel", "Nog eens wortels", "🥕", "#e07b24", null, false, [], []);
        Assert.Equal("category.duplicate", await ConflictCode(await Post("/api/categories", input)));
    }

    [Fact]
    public async Task Category_neighbours_are_stored_per_relation()
    {
        var onion = await Category("ui");
        var beans = await Category("boon");
        var input = new CategoryInput("biet", "Bieten", "🫒", "#a0306b", null, false, [onion.Id], [beans.Id]);
        var created = await Read<CategoryDto>(await Post("/api/categories", input));
        Assert.Equal([onion.Id], created.GoodNeighbourIds);
        Assert.Equal([beans.Id], created.BadNeighbourIds);

        var both = input with { Slug = "biet-2", BadNeighbourIds = [onion.Id] };
        Assert.Equal("neighbourBothGoodAndBad", (await ValidationErrors(await Post("/api/categories", both)))["badNeighbourIds"][0].Code);
    }

    [Fact]
    public async Task Inventory_lot_status_depends_on_best_before()
    {
        var variety = await Variety("Pluksla");
        var supplier = (await Get<SupplierDto[]>("/api/suppliers"))!.First();
        var lot = await Read<InventoryLotDto>(await Post("/api/inventory-lots",
            new InventoryLotInput(variety.Id, supplier.Id, 2, "g", new DateOnly(2020, 1, 1), new DateOnly(2021, 1, 1), null)));
        Assert.Equal(LotStatus.Expired, lot.Status);
    }

    [Fact]
    public async Task Exported_catalog_reimports_as_updates_without_errors()
    {
        var export = await Get<CatalogExchange>("/api/catalog/export");
        var report = await Read<ImportReport>(await Post("/api/catalog/import/preview", export));

        Assert.False(report.Committed);
        Assert.All(report.Categories, r => Assert.Equal(ImportRowStatus.Update, r.Status));
        Assert.All(report.Varieties, r => Assert.Equal(ImportRowStatus.Update, r.Status));
    }

    [Fact]
    public async Task Preview_reports_problems_and_changes_nothing_until_imported()
    {
        var data = new CatalogExchange(
            [new ExchangeCategory("bladbiet", "Bladbieten", "Amaranthaceae", null, null, null, ["ui"], ["onbekend"])],
            [
                new ExchangeVariety("bladbiet", "Snijbiet", "Rainbow", "Beta vulgaris", null, "annual", 2, 30, 40, 50, ["full", "half"],
                    [3, 4], 5, [4, 7], null, [8, 16], 60, 20, [6, 11], null, null, null, [new ExchangeSupplier("Nieuwe Zadenwinkel", 2.8m, "zakje", null)]),
                new ExchangeVariety("radijs", "Radijs", "Cherry Belle", null, "Brassicaceae", "annual", null, null, null, null, null,
                    null, null, [3, 9], null, null, 28, null, [4, 10], null, null, null, null),
            ]);

        var preview = await Read<ImportReport>(await Post("/api/catalog/import/preview", data));
        var category = Assert.Single(preview.Categories);
        Assert.Equal(ImportRowStatus.New, category.Status);
        Assert.Contains(category.Warnings, w => w.Code == "unknownNeighbour");
        Assert.Equal(ImportRowStatus.New, preview.Varieties[0].Status);
        Assert.Contains(preview.Varieties[0].Warnings, w => w.Code == "newSupplier");
        Assert.Equal(ImportRowStatus.Error, preview.Varieties[1].Status);
        Assert.Equal("unknownCategory", preview.Varieties[1].Errors!["category"][0].Code);
        Assert.DoesNotContain(await Get<CategoryDto[]>("/api/categories") ?? [], c => c.Slug == "bladbiet");

        var imported = await Read<ImportReport>(await Post("/api/catalog/import", data));
        Assert.True(imported.Committed);
        var chard = await Variety("Snijbiet", "Rainbow");
        Assert.Equal(2, chard.RotationRestYears);
        Assert.Equal("Nieuwe Zadenwinkel", Assert.Single(chard.Suppliers).SupplierName);
    }
}
