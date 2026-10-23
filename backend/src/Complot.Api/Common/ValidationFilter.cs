using FluentValidation;

namespace Complot.Api.Common;

/// <summary>Validates the first endpoint argument of type <typeparamref name="T"/> with its FluentValidation validator.</summary>
internal sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (context.Arguments.OfType<T>().FirstOrDefault() is { } input)
        {
            var result = await validator.ValidateAsync(input, context.HttpContext.RequestAborted);
            if (!result.IsValid) return ApiProblems.Validation(result);
        }
        return await next(context);
    }
}

public static class ValidationFilterExtensions
{
    public static RouteHandlerBuilder Validate<T>(this RouteHandlerBuilder builder) => builder
        .AddEndpointFilter<ValidationFilter<T>>()
        .ProducesProblem(StatusCodes.Status400BadRequest);
}
