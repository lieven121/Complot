using Complot.Domain.Catalog;

namespace Complot.Domain.Tests.Catalog;

public sealed class PiecesTests
{
    [Theory]
    [InlineData(40, "zaden", null, 40)]
    [InlineData(2, "zakje 25 z.", null, 50)]
    [InlineData(1, "per plant", null, 1)]
    [InlineData(3, "g", 800.0, 2400)]
    [InlineData(1, "zakje 0,3 g", 4000.0, 1200)]
    [InlineData(1, "1 kg pootgoed", 0.015, 15)]
    public void Counts_and_weights_become_pieces(decimal quantity, string unit, double? seedsPerGram, decimal expected) =>
        Assert.Equal(expected, Pieces.Of(quantity, unit, (decimal?)seedsPerGram));

    [Theory]
    [InlineData("zakje", 800.0)]
    [InlineData("g", null)]
    [InlineData("bos", null)]
    public void Packets_and_weights_without_seeds_per_gram_cannot_be_counted(string unit, double? seedsPerGram) =>
        Assert.Null(Pieces.Of(1, unit, (decimal?)seedsPerGram));
}
