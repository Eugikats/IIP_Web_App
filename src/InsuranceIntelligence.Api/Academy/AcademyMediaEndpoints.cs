using InsuranceIntelligence.Api.Azure;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Academy;

public static class AcademyMediaEndpoints
{
    private static readonly IReadOnlyDictionary<string, string> Types = new Dictionary<string, string>
    {
        ["video.mp4"] = "video/mp4",
        ["audio.mp3"] = "audio/mpeg",
        ["captions.vtt"] = "text/vtt",
        ["slides.pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ["transcript.md"] = "text/markdown",
    };

    public static Task<IResult> Get(string courseId, string lessonId, string fileName, HttpContext http,
        InfrastructureOptions options, ITenantContextAccessor tenant, AcademyOperator academyOperator,
        AcademyLearnerService service, CancellationToken ct) =>
        AcademyLearnerEndpoints.Run(http, options, tenant, academyOperator, async learner =>
        {
            if (!Types.TryGetValue(fileName, out var contentType)) return Results.NotFound();
            var unitId = await service.MediaUnitAsync(learner.UserId, courseId, lessonId, ct);
            if (unitId.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-'))
                return Results.Problem("Lesson media is not configured.", statusCode: 503);
            try
            {
                var container = AzureProviderSupport.BlobContainer(options.AzureBlob, "academy-media");
                var blob = container.GetBlobClient($"flagships/v1/{unitId}/{fileName}");
                if (!await blob.ExistsAsync(ct)) return Results.Problem("Lesson media is not configured.", statusCode: 503);
                http.Response.Headers.CacheControl = "private, no-store";
                return Results.Stream(await blob.OpenReadAsync(cancellationToken: ct), contentType, enableRangeProcessing: true);
            }
            catch (InvalidOperationException)
            {
                return Results.Problem("Lesson media is not configured.", statusCode: 503);
            }
            catch (global::Azure.RequestFailedException)
            {
                return Results.Problem("Lesson media is temporarily unavailable. Try again later.", statusCode: 503);
            }
        });
}
