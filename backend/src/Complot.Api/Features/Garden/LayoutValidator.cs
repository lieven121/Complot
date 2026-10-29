using Complot.Api.Common;
using Complot.Api.Data;
using Complot.Domain.Garden;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

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

/// <summary>A line through 2–200 points on the map, at least 10 cm long.</summary>
public sealed class PolylineValidator : AbstractValidator<List<PointDto>>
{
    public PolylineValidator()
    {
        RuleFor(p => p.Count).InclusiveBetween(2, 200).OverridePropertyName("points");
        RuleForEach(p => p).ChildRules(p =>
        {
            p.RuleFor(x => x.X).InclusiveBetween(-10_000, 10_000);
            p.RuleFor(x => x.Y).InclusiveBetween(-10_000, 10_000);
        }).OverridePropertyName("points");
        RuleFor(p => p).Must(p => Length(p) >= 0.1).WithErrorCode("lineTooShort").OverridePropertyName("points");
    }

    private static double Length(List<PointDto> p) => p.Zip(p.Skip(1), (a, b) => Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2))).Sum();
}

public sealed class SiteLayoutValidator : AbstractValidator<SiteLayoutDto>
{
    public SiteLayoutValidator(ComplotDbContext db)
    {
        var shape = new ShapeDtoValidator();
        var line = new PolylineValidator();

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
            p.RuleFor(x => x.Points).NotNull().SetValidator(line);
            p.RuleFor(x => x.Width).InclusiveBetween(0.1, 20);
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
            t.RuleFor(x => x.Outline).Must(k => k is ShapeKind.Ellipse or ShapeKind.Rect).WithErrorCode("isInEnum");
            t.RuleFor(x => x.CrownWidth).InclusiveBetween(0.1, 100);
            t.RuleFor(x => x.CrownDepth).InclusiveBetween(0.1, 100);
            t.RuleFor(x => x.VarietyId)
                .MustAsync((id, ct) => db.Varieties.AnyAsync(v => v.Id == id, ct)).WithErrorCode("unknownVariety")
                .When(x => x.VarietyId is not null);
            t.RuleForEach(x => x.Actions).SetValidator(new YearlyActionDtoValidator());
            t.RuleFor(x => x.Pollination!).SetValidator(new PollinationDtoValidator()).When(x => x.Pollination is not null);
        });
        RuleForEach(x => x.Markers).ChildRules(m =>
        {
            m.RuleFor(x => x.Kind).IsInEnum();
            m.RuleFor(x => x.Label).MaximumLength(100);
            m.RuleFor(x => x.Color).HexColor();
            m.RuleFor(x => x.Shape!).NotNull().SetValidator(shape).When(x => x.Kind == MarkerKind.Area);
            m.RuleFor(x => x.Shape!.Kind).Must(k => k is ShapeKind.Rect or ShapeKind.Ellipse).WithErrorCode("isInEnum")
                .When(x => x.Kind == MarkerKind.Area && x.Shape is not null);
            m.RuleFor(x => x.Points!).NotNull().SetValidator(line).When(x => x.Kind == MarkerKind.Line);
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
