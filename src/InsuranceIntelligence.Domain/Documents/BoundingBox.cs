namespace InsuranceIntelligence.Domain.Documents;

/// <summary>
/// Where a value sits on a page, as returned by the document processor and scaled to the page.
/// X, Y, Width and Height are fractions of the page (0 to 1), measured from the top left, and are the
/// axis-aligned rectangle around the value. <paramref name="Polygon"/> keeps the exact corners
/// (x0, y0, x1, y1, ...) as fractions of the page, so a rotated or slanted value is drawn as it is.
/// A value that spans several lines or pages lists each piece in <paramref name="Parts"/>.
/// </summary>
public sealed record BoundingBox(
    int PageNumber,
    double X,
    double Y,
    double Width,
    double Height,
    IReadOnlyList<double>? Polygon = null,
    IReadOnlyList<BoundingBox>? Parts = null);
