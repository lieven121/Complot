using Complot.Api.Auth;
using Complot.Api.Common;

namespace Complot.Api.Features;

public static class EndpointRegistration
{
    public static IEndpointRouteBuilder MapComplotEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapAuthRoutes();

        var api = app.MapGroup("/api");
        api.MapMeEndpoint();

        // Unknown API routes are a 404 problem, never the SPA's index.html.
        app.MapFallback("/api/{**path}", () => ApiProblems.NotFound()).ExcludeFromDescription();
        return app;
    }
}
