using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class DocumentGeometryTests
{
    // A US Letter page as a PDF reports it: 8.5 by 11 inches.
    private const double LetterWidth = 8.5;
    private const double LetterHeight = 11;

    [Fact]
    public void PolygonInInches_IsScaledToFractionsOfThePage()
    {
        // A value one inch in from the left and two down, 4 inches wide and half an inch tall.
        float[] polygon = [1f, 2f, 5f, 2f, 5f, 2.5f, 1f, 2.5f];

        var box = DocumentGeometry.FromPolygon(1, polygon, LetterWidth, LetterHeight);

        Assert.NotNull(box);
        Assert.Equal(1d / 8.5d, box!.X, 6);
        Assert.Equal(2d / 11d, box.Y, 6);
        Assert.Equal(4d / 8.5d, box.Width, 6);
        Assert.Equal(0.5d / 11d, box.Height, 6);
        Assert.True(DocumentGeometry.IsPageFraction(box));
        Assert.Equal(8, box.Polygon!.Count);
    }

    [Fact]
    public void RotatedPolygon_KeepsItsCornersAndAWideEnoughRectangle()
    {
        float[] polygon = [2f, 1f, 3f, 2f, 2f, 3f, 1f, 2f];

        var box = DocumentGeometry.FromPolygon(2, polygon, 10, 10)!;

        Assert.Equal(0.1d, box.X, 6);
        Assert.Equal(0.1d, box.Y, 6);
        Assert.Equal(0.2d, box.Width, 6);
        Assert.Equal(0.2d, box.Height, 6);
        Assert.Equal(2, box.PageNumber);
        Assert.Equal(0.2d, box.Polygon![0], 6);
        Assert.Equal(0.1d, box.Polygon[1], 6);
    }

    [Theory]
    [InlineData(0, 8.5, 11)]
    [InlineData(1, 0, 11)]
    [InlineData(1, 8.5, 0)]
    public void UnusablePageOrNumber_GivesNoBoxInsteadOfAGuess(int page, double width, double height)
    {
        float[] polygon = [1f, 1f, 2f, 1f, 2f, 2f, 1f, 2f];

        Assert.Null(DocumentGeometry.FromPolygon(page, polygon, width, height));
    }

    [Fact]
    public void ShortOrOddPolygon_GivesNoBox()
    {
        Assert.Null(DocumentGeometry.FromPolygon(1, [1f, 1f, 2f, 2f], 10, 10));
        Assert.Null(DocumentGeometry.FromPolygon(1, [1f, 1f, 2f, 1f, 2f, 2f, 1f], 10, 10));
    }

    [Fact]
    public void PolygonOutsideThePage_IsClampedToIt()
    {
        float[] polygon = [-1f, -1f, 11f, -1f, 11f, 11f, -1f, 11f];

        var box = DocumentGeometry.FromPolygon(1, polygon, 10, 10)!;

        Assert.Equal(0d, box.X);
        Assert.Equal(1d, box.Width);
        Assert.True(DocumentGeometry.IsPageFraction(box));
    }

    [Fact]
    public void SeveralRegions_AreKeptAsParts_AndASingleRegionIsReturnedAsIs()
    {
        var first = DocumentGeometry.FromPolygon(1, [1f, 1f, 4f, 1f, 4f, 2f, 1f, 2f], 10, 10)!;
        var second = DocumentGeometry.FromPolygon(1, [1f, 2f, 3f, 2f, 3f, 3f, 1f, 3f], 10, 10)!;

        Assert.Same(first, DocumentGeometry.Combine([first]));
        Assert.Null(DocumentGeometry.Combine([]));

        var both = DocumentGeometry.Combine([first, second])!;
        Assert.Equal(first.X, both.X);
        Assert.Equal(2, both.Parts!.Count);
    }

    [Fact]
    public void BoxesSavedBeforeScaling_AreNotTreatedAsPageFractions()
    {
        // The old mapper stored inches straight from the polygon.
        var legacy = new BoundingBox(1, 1.2, 2.4, 3.1, 0.4);

        Assert.False(DocumentGeometry.IsPageFraction(legacy));
    }
}
