using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Complot.Api.Tests.Infrastructure;

/// <summary>Runs the API in-process against its own temporary SQLite database.</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), "complot-tests", Guid.NewGuid().ToString("N"));

    protected virtual IReadOnlyDictionary<string, string> Settings => new Dictionary<string, string>
    {
        ["Storage:DataDir"] = _dataDir,
        ["Seed:DemoData"] = "false",
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        foreach (var (key, value) in Settings) builder.UseSetting(key, value);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dataDir, recursive: true); }
        catch (IOException) { /* best effort: a locked temp file must not fail the run */ }
    }
}

/// <summary>API with demo data seeded.</summary>
public sealed class SeededApiFactory : ApiFactory
{
    protected override IReadOnlyDictionary<string, string> Settings =>
        new Dictionary<string, string>(base.Settings) { ["Seed:DemoData"] = "true" };
}

/// <summary>API with OIDC configured (the identity provider itself is never contacted in these tests).</summary>
public sealed class OidcApiFactory : ApiFactory
{
    protected override IReadOnlyDictionary<string, string> Settings => new Dictionary<string, string>(base.Settings)
    {
        ["Auth:Oidc:Authority"] = "https://login.example.invalid",
        ["Auth:Oidc:ClientId"] = "complot",
    };
}
