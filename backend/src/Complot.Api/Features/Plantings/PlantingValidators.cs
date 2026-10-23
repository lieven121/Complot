using Complot.Api.Common;
using Complot.Api.Data;
using FluentValidation;

namespace Complot.Api.Features.Plantings;

public sealed class PlanPlantingInputValidator : AbstractValidator<PlanPlantingInput>
{
    public PlanPlantingInputValidator(ComplotDbContext db)
    {
        RuleFor(x => x.BedId).MustAsync((id, ct) => db.Beds.ExistsAsync(id, ct)).WithErrorCode("unknownBed");
        RuleFor(x => x.VarietyId).MustAsync((id, ct) => db.Varieties.ExistsAsync(id, ct)).WithErrorCode("unknownVariety");
        RuleFor(x => x.Share).InclusiveBetween(0.1, 1);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class UpdatePlantingInputValidator : AbstractValidator<UpdatePlantingInput>
{
    public UpdatePlantingInputValidator()
    {
        RuleFor(x => x.Share).InclusiveBetween(0.1, 1);
        RuleFor(x => x.PlantOutOn).GreaterThanOrEqualTo(x => x.SowOn).When(x => x.PlantOutOn is not null);
        RuleFor(x => x.HarvestFrom).GreaterThanOrEqualTo(x => x.PlantOutOn ?? x.SowOn).When(x => x.HarvestFrom is not null);
        RuleFor(x => x.HarvestTo).NotNull().When(x => x.HarvestFrom is not null);
        RuleFor(x => x.HarvestTo).GreaterThanOrEqualTo(x => x.HarvestFrom!.Value).When(x => x.HarvestTo is not null && x.HarvestFrom is not null);
        RuleFor(x => x.ClearOn).GreaterThanOrEqualTo(x => x.HarvestTo ?? x.PlantOutOn ?? x.SowOn).When(x => x.ClearOn is not null);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
