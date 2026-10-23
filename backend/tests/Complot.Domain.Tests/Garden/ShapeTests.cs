using Complot.Domain.Catalog;
using Complot.Domain.Garden;

namespace Complot.Domain.Tests.Garden;

public sealed class ShapeTests
{
    [Fact]
    public void Area_depends_on_the_shape_kind()
    {
        Assert.Equal(4.8, new Shape(ShapeKind.Rect, 0, 0, 1.2, 4).Area, 6);
        Assert.Equal(Math.PI, new Shape(ShapeKind.Ellipse, 0, 0, 2, 2).Area, 6);
        Assert.True(new Shape(ShapeKind.Keyhole, 0, 0, 2, 2).Area < Math.PI);
    }

    [Theory]
    [InlineData(1.7, 0, 0.5)]   // side by side with a 50 cm path
    [InlineData(1.2, 0, 0)]     // touching
    [InlineData(0, 4.5, 0.5)]   // below
    public void Distance_is_measured_between_bounding_boxes(double x, double y, double expected) =>
        Assert.Equal(expected, new Shape(ShapeKind.Rect, 0, 0, 1.2, 4).DistanceTo(new Shape(ShapeKind.Rect, x, y, 1.2, 4)), 6);

    [Fact]
    public void Beds_in_the_same_plot_within_60_cm_are_neighbours()
    {
        var plot = Guid.NewGuid();
        var a = new Bed { Code = "A1", PlotId = plot, Shape = new Shape(ShapeKind.Rect, 0, 0, 1.2, 4), Sun = SunExposure.Full };
        var b = new Bed { Code = "A2", PlotId = plot, Shape = new Shape(ShapeKind.Rect, 1.7, 0, 1.2, 4) };
        var far = new Bed { Code = "A3", PlotId = plot, Shape = new Shape(ShapeKind.Rect, 3.4, 0, 1.2, 4) };
        var otherPlot = new Bed { Code = "K1", PlotId = Guid.NewGuid(), Shape = new Shape(ShapeKind.Rect, 1.7, 0, 1.2, 4) };

        Assert.True(a.IsNeighbourOf(b));
        Assert.False(a.IsNeighbourOf(far));
        Assert.False(a.IsNeighbourOf(otherPlot));
        Assert.False(a.IsNeighbourOf(a));
    }
}
