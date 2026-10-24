using Complot.Api.Auth;

namespace Complot.Api.Features.Work;

public static class WorkEndpoints
{
    public static void MapWork(this IEndpointRouteBuilder api)
    {
        api.MapGroup("").RequireEditorForWrites().MapPeople();
        api.MapTasks();
        api.MapNotes();
    }

    public static IServiceCollection AddWork(this IServiceCollection services) => services.AddScoped<TaskService>();
}
