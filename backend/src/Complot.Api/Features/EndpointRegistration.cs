using Complot.Api.Auth;
using Complot.Api.Common;
using Complot.Api.Features.Admin;
using Complot.Api.Features.Catalog;
using Complot.Api.Features.Garden;
using Complot.Api.Features.Plantings;
using Complot.Api.Features.Work;

namespace Complot.Api.Features;

public static class EndpointRegistration
{
    public static IEndpointRouteBuilder MapComplotEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapAuthRoutes();

        var root = app.MapGroup("/api");
        root.MapMeEndpoint();

        // Everything else needs access: a signed-in user who is still waiting gets 403 "access.pending".
        var api = root.MapGroup("").RequireActiveUser();
        api.MapGroup("").RequireEditorForWrites().MapCatalog();
        api.MapSites();
        api.MapPlantings();
        api.MapWork();
        api.MapAdmin();

        // Unknown API routes are a 404 problem, never the SPA's index.html.
        app.MapFallback("/api/{**path}", () => ApiProblems.NotFound()).ExcludeFromDescription();
        return app;
    }
}
