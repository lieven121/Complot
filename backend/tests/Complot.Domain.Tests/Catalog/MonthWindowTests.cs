using Complot.Domain.Catalog;

namespace Complot.Domain.Tests.Catalog;

public sealed class MonthWindowTests
{
    [Theory]
    [InlineData(3, 6, 3, true)]
    [InlineData(3, 6, 6, true)]
    [InlineData(3, 6, 7, false)]
    [InlineData(3, 6, 2, false)]
    [InlineData(10, 2, 12, true)]
    [InlineData(10, 2, 1, true)]
    [InlineData(10, 2, 2, true)]
    [InlineData(10, 2, 6, false)]
    [InlineData(7, 7, 7, true)]
    public void Contains_handles_normal_and_year_wrapping_windows(int from, int to, int month, bool expected) =>
        Assert.Equal(expected, new MonthWindow(from, to).Contains(month));

    [Theory]
    [InlineData(0, 5)]
    [InlineData(13, 5)]
    [InlineData(5, 0)]
    [InlineData(5, 13)]
    public void Rejects_months_outside_1_to_12(int from, int to) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new MonthWindow(from, to));

    [Fact]
    public void Windows_with_the_same_months_are_equal() =>
        Assert.Equal(new MonthWindow(4, 9), new MonthWindow(4, 9));
}
