using Complot.Domain.Catalog;
using FluentValidation;

namespace Complot.Api.Common;

/// <summary>Month window as sent over the API (months 1–12, may wrap the year).</summary>
public sealed record MonthWindowDto(int From, int To);

/// <summary>Inclusive day range, e.g. germination 7–14 days.</summary>
public sealed record DayRangeDto(int Min, int Max);

public static class SharedDtoMapping
{
    public static MonthWindowDto? ToDto(this MonthWindow? window) => window is null ? null : new(window.From, window.To);
    public static MonthWindow? ToDomain(this MonthWindowDto? dto) => dto is null ? null : new(dto.From, dto.To);
    public static DayRangeDto? ToDto(this DayRange? range) => range is null ? null : new(range.Min, range.Max);
    public static DayRange? ToDomain(this DayRangeDto? dto) => dto is null ? null : new(dto.Min, dto.Max);
}

public sealed class MonthWindowDtoValidator : AbstractValidator<MonthWindowDto>
{
    public MonthWindowDtoValidator()
    {
        RuleFor(x => x.From).InclusiveBetween(1, 12);
        RuleFor(x => x.To).InclusiveBetween(1, 12);
    }
}

public sealed class DayRangeDtoValidator : AbstractValidator<DayRangeDto>
{
    public DayRangeDtoValidator()
    {
        RuleFor(x => x.Min).InclusiveBetween(0, 365);
        RuleFor(x => x.Max).InclusiveBetween(0, 365).GreaterThanOrEqualTo(x => x.Min);
    }
}
