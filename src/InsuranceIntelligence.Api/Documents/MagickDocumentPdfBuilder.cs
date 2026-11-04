using ImageMagick;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Api.Documents;

/// <summary>
/// Builds a PDF from pictures with Magick.NET: each picture is turned upright, limited to about A4
/// at 200 dpi, and becomes one page sized to its picture. The pictures are only read. A picture that
/// cannot be read makes the whole build return null, so a PDF is never silently missing a page.
/// </summary>
public sealed class MagickDocumentPdfBuilder(ILogger<MagickDocumentPdfBuilder> logger) : IDocumentPdfBuilder
{
    // About A4 at 200 dpi: sharp enough to read small print, small enough to send and store.
    private const int MaxLongEdge = 2339;
    private const int MaxShortEdge = 1654;

    static MagickDocumentPdfBuilder()
    {
        ResourceLimits.Width = 16_000;
        ResourceLimits.Height = 16_000;
        ResourceLimits.Area = 120_000_000;
        ResourceLimits.Memory = 512UL * 1024 * 1024;
        ResourceLimits.Time = 30;
    }

    public async Task<BuiltPdf?> BuildAsync(IReadOnlyList<(Stream Picture, string FileName)> pages, CancellationToken cancellationToken = default)
    {
        if (pages.Count == 0)
        {
            return null;
        }

        try
        {
            using var collection = new MagickImageCollection();
            foreach (var (picture, _) in pages)
            {
                using var memory = new MemoryStream();
                await picture.CopyToAsync(memory, cancellationToken);
                memory.Position = 0;
                var image = new MagickImage(memory);
                image.AutoOrient();
                image.ColorSpace = ColorSpace.sRGB;
                var landscape = image.Width > image.Height;
                var maxWidth = landscape ? MaxLongEdge : MaxShortEdge;
                var maxHeight = landscape ? MaxShortEdge : MaxLongEdge;
                if (image.Width > maxWidth || image.Height > maxHeight)
                {
                    image.Resize(new MagickGeometry((uint)maxWidth, (uint)maxHeight));
                }

                image.Strip();
                image.Density = new Density(200);
                image.Format = MagickFormat.Jpeg;
                image.Quality = 82;
                collection.Add(image);
            }

            using var output = new MemoryStream();
            collection.Write(output, MagickFormat.Pdf);
            return new BuiltPdf(output.ToArray(), collection.Count);
        }
        catch (MagickException exception)
        {
            logger.LogInformation("A PDF could not be built from the pictures in a slot: {Reason}", exception.GetType().Name);
            return null;
        }
    }
}
