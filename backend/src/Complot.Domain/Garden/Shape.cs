namespace Complot.Domain.Garden;

public enum ShapeKind
{
    Rect,
    Ellipse,
    Keyhole,
}

/// <summary>
/// Outline of a map item in meters: a bounding box (x → east, y → south) and the shape drawn inside it.
/// A keyhole bed is a circle with a path notch and a compost basket in the middle.
/// </summary>
public sealed record Shape
{
    /// <summary>Share of the circle that is planted in a keyhole bed (notch and basket excluded).</summary>
    private const double KeyholePlantedShare = 0.85;

    public Shape(ShapeKind kind, double x, double y, double width, double height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Kind = kind;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public ShapeKind Kind { get; private init; }
    public double X { get; private init; }
    public double Y { get; private init; }
    public double Width { get; private init; }
    public double Height { get; private init; }

    /// <summary>Plantable area in m².</summary>
    public double Area => Kind switch
    {
        ShapeKind.Ellipse => Math.PI * Width * Height / 4,
        ShapeKind.Keyhole => Math.PI * Math.Pow(Math.Min(Width, Height) / 2, 2) * KeyholePlantedShare,
        _ => Width * Height,
    };

    /// <summary>Shortest distance between the bounding boxes (0 when they touch or overlap).</summary>
    public double DistanceTo(Shape other)
    {
        var dx = Math.Max(0, Math.Max(X, other.X) - Math.Min(X + Width, other.X + other.Width));
        var dy = Math.Max(0, Math.Max(Y, other.Y) - Math.Min(Y + Height, other.Y + other.Height));
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
