namespace InsuranceIntelligence.Domain.Documents;

/// <summary>
/// Turns a document processor's page coordinates into fractions of the page. Processors report
/// polygons in the page's own unit (inches for a PDF, pixels for an image), so a value is only
/// usable for highlighting once it has been divided by the page size. Pure, so it is tested without
/// any provider.
/// </summary>
public static class DocumentGeometry
{
    /// <summary>One region: the rectangle around the polygon plus the polygon itself, all as fractions
    /// of the page. Returns null when the page size or the polygon is unusable, so no coordinate is
    /// ever invented.</summary>
    public static BoundingBox? FromPolygon(int pageNumber, IReadOnlyList<float> polygon, double pageWidth, double pageHeight)
    {
        if (pageNumber < 1 || pageWidth <= 0 || pageHeight <= 0 || polygon.Count < 6 || polygon.Count % 2 != 0)
        {
            return null;
        }

        var points = new double[polygon.Count];
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        for (var i = 0; i < polygon.Count; i += 2)
        {
            var x = Clamp01(polygon[i] / pageWidth);
            var y = Clamp01(polygon[i + 1] / pageHeight);
            points[i] = x;
            points[i + 1] = y;
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
        }

        var width = maxX - minX;
        var height = maxY - minY;
        return width <= 0 || height <= 0
            ? null
            : new BoundingBox(pageNumber, minX, minY, width, height, points);
    }

    /// <summary>One box for a value that may sit in several regions. The first region is the headline
    /// rectangle and every region is kept in <see cref="BoundingBox.Parts"/> when there is more than one.</summary>
    public static BoundingBox? Combine(IReadOnlyList<BoundingBox> regions) =>
        regions.Count switch
        {
            0 => null,
            1 => regions[0],
            _ => regions[0] with { Parts = regions },
        };

    /// <summary>True when a stored box is a real fraction of a page. Boxes saved before the page size was
    /// applied hold inches or pixels, and must not be drawn.</summary>
    public static bool IsPageFraction(BoundingBox box) =>
        box.PageNumber >= 1
        && box.Width > 0 && box.Height > 0
        && box.X >= 0 && box.Y >= 0
        && box.X + box.Width <= 1.001 && box.Y + box.Height <= 1.001;

    private static double Clamp01(double value) => Math.Clamp(value, 0d, 1d);
}
