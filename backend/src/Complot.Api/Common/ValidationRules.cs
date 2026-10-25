using FluentValidation;

namespace Complot.Api.Common;

/// <summary>Validation rules shared by several inputs.</summary>
public static class ValidationRules
{
    /// <summary>A colour as "#rrggbb" (error code "color").</summary>
    public static IRuleBuilderOptions<T, string> HexColor<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Matches("^#[0-9a-fA-F]{6}$").WithErrorCode("color");
}
