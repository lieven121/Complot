using Complot.Domain.Common;

namespace Complot.Domain.Catalog;

/// <summary>Botanical family. Crop rotation is checked per family.</summary>
public sealed class PlantFamily : Entity
{
    public required string LatinName { get; set; }
    public required string NameNl { get; set; }
    public required string NameEn { get; set; }

    /// <summary>Years a bed should rest before the same family returns. 0 = no rotation needed.</summary>
    public int RotationRestYears { get; set; }
}
