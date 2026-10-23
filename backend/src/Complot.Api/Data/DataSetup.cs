using Complot.Api.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Complot.Api.Data;

public static class DataSetup
{
    public static IServiceCollection AddComplotData(this IServiceCollection services)
    {
        services.AddSingleton<TimestampInterceptor>();
        services.AddDbContext<ComplotDbContext>((sp, options) =>
        {
            var storage = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            options
                .UseSqlite($"Data Source={storage.DatabasePath}")
                .AddInterceptors(sp.GetRequiredService<TimestampInterceptor>());
        });
        services.AddScoped<DatabaseInitializer>();
        return services;
    }

    /// <summary>Applies migrations and optional seed data before the app starts serving requests.</summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        if (HostingSetup.IsOpenApiGeneration) return;
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
    }
}
