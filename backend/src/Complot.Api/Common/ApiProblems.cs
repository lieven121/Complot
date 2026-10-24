using FluentValidation.Results;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Complot.Api.Common;

/// <summary>
/// One machine-readable error. The frontend translates <see cref="Code"/> (e.g. "notEmpty", "category.inUse")
/// with i18n and fills the message with <see cref="Params"/>; the API never returns UI text.
/// </summary>
public sealed record ErrorDetail(string Code, IReadOnlyDictionary<string, object?>? Params = null);

/// <summary>ProblemDetails responses shared by all endpoints.</summary>
public static class ApiProblems
{
    /// <summary>400 with <c>errors</c>: camelCase field path → error details.</summary>
    public static ProblemHttpResult Validation(IReadOnlyDictionary<string, ErrorDetail[]> errors) =>
        TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Validation failed",
            extensions: new Dictionary<string, object?> { ["errors"] = errors });

    public static ProblemHttpResult Validation(ValidationResult result) => Validation(FieldErrors.From(result));

    /// <summary>409 with a <c>code</c>, e.g. "category.inUse".</summary>
    public static ProblemHttpResult Conflict(string code) =>
        TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict",
            extensions: new Dictionary<string, object?> { ["code"] = code });

    /// <summary>403 with a <c>code</c>, e.g. "access.readOnly".</summary>
    public static ProblemHttpResult Forbidden(string code) =>
        TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden",
            extensions: new Dictionary<string, object?> { ["code"] = code });

    public static ProblemHttpResult NotFound() => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
}

public static class FieldErrors
{
    private static readonly HashSet<string> InternalPlaceholders = ["PropertyName", "PropertyValue", "PropertyPath"];

    public static Dictionary<string, ErrorDetail[]> From(ValidationResult result) => result.Errors
        .GroupBy(e => ToCamelPath(e.PropertyName))
        .ToDictionary(g => g.Key, g => g.Select(ToDetail).ToArray());

    /// <summary>FluentValidation's "NotEmptyValidator" becomes "notEmpty"; custom codes are kept as-is.</summary>
    public static ErrorDetail ToDetail(ValidationFailure failure)
    {
        var code = failure.ErrorCode.EndsWith("Validator", StringComparison.Ordinal)
            ? CamelCase(failure.ErrorCode[..^"Validator".Length])
            : failure.ErrorCode;
        var parameters = failure.FormattedMessagePlaceholderValues?
            .Where(p => !InternalPlaceholders.Contains(p.Key))
            .ToDictionary(p => CamelCase(p.Key), p => p.Value is IConvertible ? p.Value : p.Value?.ToString());
        return new ErrorDetail(code, parameters is { Count: > 0 } ? parameters : null);
    }

    /// <summary>"Suppliers[0].Price" → "suppliers[0].price".</summary>
    public static string ToCamelPath(string propertyPath) =>
        string.Join('.', propertyPath.Split('.').Select(CamelCase));

    private static string CamelCase(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
}
