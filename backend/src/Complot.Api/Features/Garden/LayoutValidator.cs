using Complot.Api.Common;
using Complot.Domain.Garden;
using FluentValidation;

namespace Complot.Api.Features.Garden;

public sealed class SiteInputValidator : AbstractValidator<SiteInput>
{
    public SiteInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class ShapeDtoValidator : AbstractValidator<ShapeDto>
{
    public ShapeDtoValidator()
    {
        RuleFor(s => s.Kind).IsInEnum();
        RuleFor(s => s.X).InclusiveBetween(-10_000, 10_000);
        RuleFor(s => s.Y).InclusiveBetween(-10_000, 10_000);
        RuleFor(s => s.Width).InclusiveBetween(0.1, 1_000);
        RuleFor(s => s.Height).InclusiveBetween(0.1, 1_000);
    }
}

/// <summary>A line from (X, Y) by (Width, Height): either direction, at least 10 cm long.</summary>
public sealed class LineShapeValidator : AbstractValidator<ShapeDto>
{
    public LineShapeValidator()
    {
        RuleFor(s => s.X).InclusiveBetween(-10_000, 10_000);
        RuleFor(s => s.Y).InclusiveBetween(-10_000, 10_000);
        RuleFor(s => s.Width).InclusiveBetween(-1_000, 1_000);
        RuleFor(s => s.Height).InclusiveBetween(-1_000, 1_000);
        RuleFor(s => s).Must(s => Math.Sqrt(s.Width * s.Width + s.Height * s.Height) >= 0.1).WithErrorCode("lineTooShort")
            .OverridePropertyName("width");
    }
}

public sealed class SiteLayoutValidator : AbstractValidator<SiteLayoutDto>
{
    public SiteLayoutValidator()
    {
        var shape = new ShapeDtoValidator();

        RuleForEach(x => x.Plots).ChildRules(p =>
        {
            p.RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            p.RuleFor(x => x.Type).IsInEnum();
            p.RuleFor(x => x.Shape).NotNull().SetValidator(shape);
        });
        RuleForEach(x => x.Beds).ChildRules(b =>
        {
            b.RuleFor(x => x.Code).NotEmpty().MaximumLength(12);
            b.RuleFor(x => x.Kind).IsInEnum();
            b.RuleFor(x => x.Shape).NotNull().SetValidator(shape);
            b.RuleFor(x => x.Sun).IsInEnum();
            b.RuleFor(x => x.Soil).MaximumLength(200);
            b.RuleFor(x => x.Notes).MaximumLength(2000);
        });
        RuleForEach(x => x.Beds)
            .Must((layout, bed) => layout.Plots.Any(p => p.Id == bed.PlotId)).WithErrorCode("unknownPlot");
        RuleFor(x => x.Beds)
            .Must(beds => beds.Select(b => b.Code.Trim().ToUpperInvariant()).Distinct().Count() == beds.Count)
            .WithErrorCode("duplicateBedCode");
        RuleForEach(x => x.Paths).ChildRules(p =>
        {
            p.RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            p.RuleFor(x => x.Material).IsInEnum();
            p.RuleFor(x => x.Shape).NotNull().SetValidator(shape);
        });
        RuleForEach(x => x.Features).ChildRules(f =>
        {
            f.RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            f.RuleFor(x => x.Type).IsInEnum();
            f.RuleFor(x => x.Shape).NotNull().SetValidator(shape);
        });
        RuleForEach(x => x.Trees).ChildRules(t =>
        {
            t.RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            t.RuleFor(x => x.Cultivar).MaximumLength(120);
            t.RuleFor(x => x.Kind).IsInEnum();
            t.RuleFor(x => x.X).InclusiveBetween(-10_000, 10_000);
            t.RuleFor(x => x.Y).InclusiveBetween(-10_000, 10_000);
            t.RuleFor(x => x.CrownDiameter).InclusiveBetween(0.1, 100);
            t.RuleForEach(x => x.Actions).SetValidator(new YearlyActionDtoValidator());
        });
        RuleForEach(x => x.Markers).ChildRules(m =>
        {
            m.RuleFor(x => x.Kind).IsInEnum();
            m.RuleFor(x => x.Label).MaximumLength(100);
            m.RuleFor(x => x.Color).HexColor();
            m.RuleFor(x => x.Shape).NotNull().SetValidator(shape).When(x => x.Kind == MarkerKind.Area);
            m.RuleFor(x => x.Shape.Kind).Must(k => k is ShapeKind.Rect or ShapeKind.Ellipse).WithErrorCode("isInEnum")
                .When(x => x.Kind == MarkerKind.Area && x.Shape is not null);
            m.RuleFor(x => x.Shape).NotNull().SetValidator(new LineShapeValidator()).When(x => x.Kind == MarkerKind.Line);
            m.RuleFor(x => x.Thickness).InclusiveBetween(0.02, 5);
        });
        RuleFor(x => x)
            .Must(x => AllIds(x).Distinct().Count() == AllIds(x).Count()).WithErrorCode("duplicateId")
            .OverridePropertyName("layout");
    }

    private static IEnumerable<Guid> AllIds(SiteLayoutDto x) =>
        x.Plots.Select(p => p.Id).Concat(x.Beds.Select(b => b.Id)).Concat(x.Paths.Select(p => p.Id))
            .Concat(x.Features.Select(f => f.Id)).Concat(x.Trees.Select(t => t.Id)).Concat(x.Markers.Select(m => m.Id));
}
