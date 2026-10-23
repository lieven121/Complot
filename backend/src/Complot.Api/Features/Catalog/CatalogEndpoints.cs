using Complot.Api.Features.Catalog.Categories;
using Complot.Api.Features.Catalog.Exchange;
using Complot.Api.Features.Catalog.Families;
using Complot.Api.Features.Catalog.Inventory;
using Complot.Api.Features.Catalog.Suppliers;
using Complot.Api.Features.Catalog.Varieties;

namespace Complot.Api.Features.Catalog;

public static class CatalogEndpoints
{
    public static void MapCatalog(this IEndpointRouteBuilder api)
    {
        api.MapFamilies();
        api.MapCategories();
        api.MapSuppliers();
        api.MapVarieties();
        api.MapInventoryLots();
        api.MapCatalogExchange();
    }

    public static IServiceCollection AddCatalog(this IServiceCollection services) => services.AddScoped<CatalogImporter>();
}
