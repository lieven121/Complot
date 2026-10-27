using Complot.Api.Common;
using Complot.Api.Data;
using Complot.Domain.Catalog;
using FluentValidation;

namespace Complot.Api.Features.Catalog.Varieties;

/// <summary>The single set of rules for a variety, used by the CRUD endpoints and by the AI/JSON import.</summary>
public sealed class VarietyInputValidator : AbstractValidator<VarietyInput>
{
    public VarietyInputValidator(ComplotDbContext db)
    {
        RuleFor(x => x.CategoryId).MustAsync((id, ct) => db.Categories.ExistsAsync(id, ct)).WithErrorCode("unknownCategory");
        RuleFor(x => x.FamilyId)
            .MustAsync((id, ct) => db.Families.ExistsAsync(id!.Value, ct)).WithErrorCode("unknownFamily")
            .When(x => x.FamilyId is not null);

        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Cultivar).MaximumLength(120);
        RuleFor(x => x.LatinName).MaximumLength(120);
        RuleFor(x => x.Notes).MaximumLength(4000);
        RuleFor(x => x.Life).IsInEnum();

        RuleFor(x => x.SowDepthCm).InclusiveBetween(0, 100).When(x => x.SowDepthCm is not null);
        RuleFor(x => x.SpacingInRowCm).InclusiveBetween(0, 1000).When(x => x.SpacingInRowCm is not null);
        RuleFor(x => x.SpacingBetweenRowsCm).InclusiveBetween(0, 1000).When(x => x.SpacingBetweenRowsCm is not null);
        RuleFor(x => x.HeightCm).InclusiveBetween(0, 5000).When(x => x.HeightCm is not null);
        RuleFor(x => x.Sun).NotEmpty();
        RuleForEach(x => x.Sun).IsInEnum();

        RuleFor(x => x.SowIndoor!).SetValidator(new MonthWindowDtoValidator()).When(x => x.SowIndoor is not null);
        RuleFor(x => x.SowOutdoor!).SetValidator(new MonthWindowDtoValidator()).When(x => x.SowOutdoor is not null);
        RuleFor(x => x.PlantOut!).SetValidator(new MonthWindowDtoValidator()).When(x => x.PlantOut is not null);
        RuleFor(x => x.Harvest).NotNull().SetValidator(new MonthWindowDtoValidator());
        RuleFor(x => x.Germination!).SetValidator(new DayRangeDtoValidator()).When(x => x.Germination is not null);
        RuleFor(x => x)
            .Must(x => x.SowIndoor is not null || x.SowOutdoor is not null || x.PlantOut is not null)
            .WithErrorCode("noSowingWindow").OverridePropertyName("sowOutdoor");

        RuleFor(x => x.WeeksIndoor).InclusiveBetween(1, 52).When(x => x.WeeksIndoor is not null);
        RuleFor(x => x.DaysToFirstHarvest).NotNull().When(x => x.Life == Life.Annual);
        RuleFor(x => x.DaysToFirstHarvest).InclusiveBetween(1, 1000).When(x => x.DaysToFirstHarvest is not null);
        RuleFor(x => x.HarvestWeeks).InclusiveBetween(0, 104).When(x => x.HarvestWeeks is not null);
        RuleFor(x => x.MaxYearsInBed).InclusiveBetween(1, 100).When(x => x.MaxYearsInBed is not null);
        RuleFor(x => x.SeedsPerGram).GreaterThan(0).When(x => x.SeedsPerGram is not null);

        RuleForEach(x => x.Actions).SetValidator(new YearlyActionDtoValidator());
        RuleFor(x => x.Pollination!).SetValidator(new PollinationDtoValidator()).When(x => x.Pollination is not null);

        RuleForEach(x => x.Suppliers).ChildRules(supplier =>
        {
            supplier.RuleFor(s => s.SupplierId).MustAsync((id, ct) => db.Suppliers.ExistsAsync(id, ct)).WithErrorCode("unknownSupplier");
            supplier.RuleFor(s => s.Price).GreaterThanOrEqualTo(0);
            supplier.RuleFor(s => s.Unit).MaximumLength(60);
            supplier.RuleFor(s => s.Url).MaximumLength(500);
            supplier.RuleFor(s => s.PiecesPerUnit).GreaterThan(0).When(s => s.PiecesPerUnit is not null);
        });
        RuleFor(x => x.Suppliers)
            .Must(s => s.Select(x => x.SupplierId).Distinct().Count() == s.Count).WithErrorCode("duplicateSupplier");

        RuleForEach(x => x.ToolNeeds).ChildRules(need =>
        {
            need.RuleFor(n => n.ToolId).MustAsync((id, ct) => db.Tools.ExistsAsync(id, ct)).WithErrorCode("unknownTool");
            need.RuleFor(n => n.Per).IsInEnum();
            need.RuleFor(n => n.Quantity).GreaterThan(0).LessThanOrEqualTo(1000);
        });
        RuleFor(x => x.ToolNeeds)
            .Must(n => n!.Select(x => x.ToolId).Distinct().Count() == n!.Count).WithErrorCode("duplicateTool")
            .When(x => x.ToolNeeds is not null);
    }
}
