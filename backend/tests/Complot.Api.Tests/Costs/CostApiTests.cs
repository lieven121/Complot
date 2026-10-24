using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Complot.Api.Features.Costs;
using Complot.Api.Tests.Infrastructure;
using Complot.Domain.Costs;

namespace Complot.Api.Tests.Costs;

public sealed class CostApiTests(SeededApiFactory factory) : IClassFixture<SeededApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<T>(ApiJson.Options, Ct))!;
    }

    private async Task<T> Get<T>(string url) => await Read<T>(await _client.GetAsync(url, Ct));
    private async Task<CostCategoryDto> Category(string name) => (await Get<CostCategoryDto[]>("/api/cost-categories")).Single(c => c.Name == name);

    [Fact]
    public async Task Demo_invoices_add_up_in_the_report()
    {
        var seeds = await Category("Zaden & plantgoed");
        var report = await Get<CostReportDto>("/api/costs/report?year=2025&grouping=quarter");
        Assert.Equal(9, report.InvoiceCount);
        Assert.Equal(4, report.Periods.Count);
        Assert.Equal(report.Total, report.Periods.Sum(p => p.Total));
        Assert.Equal(71.10m, report.ByCategory[seeds.Id]); // 32.40 + 19.80 + 9.90 + 2.50 + 6.50
        Assert.Contains(2026, report.Years);
        Assert.True(report.BedArea > 0);
    }

    [Fact]
    public async Task Invoice_lines_must_add_up_to_the_total()
    {
        var soil = await Category("Bodem & compost");
        var input = new InvoiceInput(new DateOnly(2026, 7, 1), "Hoeve Test", "T-1", 50m, null, null,
            [new InvoiceLineDto("Compost", soil.Id, 30m), new InvoiceLineDto("Mest", soil.Id, 15m)]);
        var response = await _client.PostAsJsonAsync("/api/invoices", input, ApiJson.Options, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options, Ct)).GetProperty("errors").GetProperty("lines")[0];
        Assert.Equal("linesTotal", error.GetProperty("code").GetString());
        Assert.Equal(45m, error.GetProperty("params").GetProperty("lines").GetDecimal());

        var created = await Read<InvoiceDto>(await _client.PostAsJsonAsync("/api/invoices",
            input with { Lines = [.. input.Lines, new InvoiceLineDto("Levering", soil.Id, 5m)] }, ApiJson.Options, Ct));
        Assert.Equal((50m, 3), (created.Total, created.Lines.Count));

        var updated = await Read<InvoiceDto>(await _client.PutAsJsonAsync($"/api/invoices/{created.Id}",
            input with { Total = 30m, Lines = [new InvoiceLineDto("Compost", soil.Id, 30m)] }, ApiJson.Options, Ct));
        Assert.Single(updated.Lines);
        Assert.Contains("Hoeve Test", await Get<string[]>("/api/invoices/suppliers"));
        Assert.Contains("De Bolster", await Get<string[]>("/api/invoices/suppliers"));
    }

    [Fact]
    public async Task Attachment_is_stored_served_and_removed()
    {
        var other = await Category("Overig / verzending");
        var invoice = await Read<InvoiceDto>(await _client.PostAsJsonAsync("/api/invoices",
            new InvoiceInput(new DateOnly(2026, 8, 1), "Post", null, 7m, null, null, [new InvoiceLineDto("Verzending", other.Id, 7m)]), ApiJson.Options, Ct));

        using (var pdf = new ByteArrayContent("%PDF-1.4"u8.ToArray()))
        using (var form = new MultipartFormDataContent { { pdf, "file", "factuur.pdf" } })
        {
            pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            Assert.Equal(HttpStatusCode.NoContent, (await _client.PutAsync($"/api/invoices/{invoice.Id}/attachment", form, Ct)).StatusCode);
        }
        var listed = (await Get<InvoiceDto[]>("/api/invoices?year=2026")).Single(i => i.Id == invoice.Id);
        Assert.Equal((true, "factuur.pdf"), (listed.HasAttachment, listed.AttachmentName));
        var file = await _client.GetAsync($"/api/invoices/{invoice.Id}/attachment", Ct);
        Assert.Equal("application/pdf", file.Content.Headers.ContentType?.MediaType);

        using (var text = new ByteArrayContent("hello"u8.ToArray()))
        using (var form = new MultipartFormDataContent { { text, "file", "x.txt" } })
        {
            text.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutAsync($"/api/invoices/{invoice.Id}/attachment", form, Ct)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/invoices/{invoice.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/invoices/{invoice.Id}/attachment", Ct)).StatusCode);
    }

    [Fact]
    public async Task Categories_in_use_cannot_be_deleted()
    {
        var seeds = await Category("Zaden & plantgoed");
        var response = await _client.DeleteAsync($"/api/cost-categories/{seeds.Id}", Ct);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var created = await Read<CostCategoryDto>(await _client.PostAsJsonAsync("/api/cost-categories", new CostCategoryInput("Testkosten", "#123456"), ApiJson.Options, Ct));
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsJsonAsync("/api/cost-categories", new CostCategoryInput("Testkosten", "#654321"), ApiJson.Options, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/cost-categories/{created.Id}", Ct)).StatusCode);
    }
}

public sealed class CostAccessTests(SignedInApiFactory factory) : IClassFixture<SignedInApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Costs_are_only_for_editors()
    {
        var admin = factory.Admin();
        var site = (await admin.GetFromJsonAsync<Features.Garden.SiteDto[]>("/api/sites", ApiJson.Options, Ct))!.First();
        await admin.PostAsJsonAsync("/api/admin/users",
            new Features.Admin.UserInput("kosten-kijker@example.com", null, false, null, [new Features.Admin.SiteRoleDto(site.Id, Domain.Access.SiteRole.View)]), ApiJson.Options, Ct);
        var viewer = factory.ClientFor("sub-kosten", "kosten-kijker@example.com");
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/invoices", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/costs/report", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/costs/report", Ct)).StatusCode);
    }
}
