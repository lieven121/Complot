using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.OpenApi;

namespace Complot.Api.Hosting;

public static class HostingSetup
{
    /// <summary>True while the build generates the OpenAPI document (the app is started without serving requests).</summary>
    public static bool IsOpenApiGeneration => Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

    public static IServiceCollection AddComplotHosting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StorageOptions>().Bind(configuration.GetSection(StorageOptions.Section));
        services.AddOptions<SeedOptions>().Bind(configuration.GetSection(SeedOptions.Section));
        services.AddSingleton(TimeProvider.System);

        // Keys for sign-in cookies and the encrypted Google tokens survive restarts (and container rebuilds).
        var storage = configuration.GetSection(StorageOptions.Section).Get<StorageOptions>() ?? new StorageOptions();
        services.AddDataProtection()
            .SetApplicationName("Complot")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(storage.FullDataDir, "keys")));

        services.AddProblemDetails();
        services.AddHealthChecks();
        // OpenAPI 3.0: the NSwag TypeScript generator reads 3.0 most reliably.
        services.AddOpenApi(options =>
        {
            options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
            // DTOs are closed types; without this NSwag adds an `[key: string]: any` index signature to every interface.
            options.AddSchemaTransformer((schema, _, _) =>
            {
                if (schema.Properties is { Count: > 0 }) schema.AdditionalPropertiesAllowed = false;
                return Task.CompletedTask;
            });
        });
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            // Numbers are numbers: keeps the OpenAPI schema (and the generated TypeScript) precise.
            options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        });

        services.AddValidatorsFromAssemblyContaining<Program>(ServiceLifetime.Scoped, includeInternalTypes: true);
        return services;
    }
}

public sealed class StorageOptions
{
    public const string Section = "Storage";

    /// <summary>Directory for the SQLite database and uploaded files. Relative paths resolve from the working directory.</summary>
    public string DataDir { get; set; } = "app-data";

    public string FullDataDir => Path.GetFullPath(DataDir);
    public string DatabasePath => Path.Combine(FullDataDir, "complot.db");
    public string PhotoDir => Path.Combine(FullDataDir, "photos");
}

public sealed class SeedOptions
{
    public const string Section = "Seed";

    /// <summary>Fill an empty database with the sample catalog from the demo.</summary>
    public bool DemoData { get; set; }
}
