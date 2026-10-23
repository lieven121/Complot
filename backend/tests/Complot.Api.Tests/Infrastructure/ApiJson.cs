using System.Text.Json;
using System.Text.Json.Serialization;

namespace Complot.Api.Tests.Infrastructure;

/// <summary>Same JSON conventions as the API (camelCase, enums as camelCase strings).</summary>
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
