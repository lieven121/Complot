using Complot.Api.Data;

namespace Complot.Api.Features.Catalog.Exchange;

public static class ExchangeEndpoints
{
    public static void MapCatalogExchange(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/catalog").WithTags("CatalogExchange");
        group.MapGet("/export", (ComplotDbContext db, CancellationToken ct) => CatalogExporter.ExportAsync(db, ct)).WithName("ExportCatalog");
        group.MapPost("/import/preview", (CatalogExchange data, CatalogImporter importer, CancellationToken ct) => importer.RunAsync(data, commit: false, ct))
            .WithName("PreviewCatalogImport");
        group.MapPost("/import", (CatalogExchange data, CatalogImporter importer, CancellationToken ct) => importer.RunAsync(data, commit: true, ct))
            .WithName("ImportCatalog");
    }
}
