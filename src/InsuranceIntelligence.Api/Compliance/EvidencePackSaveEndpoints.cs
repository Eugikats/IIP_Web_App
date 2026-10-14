using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.CommandCenter;
using InsuranceIntelligence.Application.Compliance;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Integrations;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Compliance;

/// <summary>
/// "Save to my storage" for an exported evidence pack. Anything that leaves the platform is sent by a named person who
/// chose to send it: this is that person pressing the button, into their own Google Drive or OneDrive through their own
/// connection, never into anyone else's. The pack must already be an export of this case (the same check the download
/// uses). The audit entry on the case records who, where and the file name, never the contents.
/// </summary>
public static class EvidencePackSaveEndpoints
{
    public sealed record SaveRequest(string? ObjectKey, string? Provider);

    public static async Task<IResult> Save(
        Guid caseId,
        SaveRequest request,
        IComplianceQueryService compliance,
        IUserFileService files,
        IAuditWriter audit,
        ITenantContextSetter setter,
        ITenantContextAccessor tenant,
        TimeProvider clock,
        InfrastructureOptions options,
        HttpContext http,
        CancellationToken ct)
    {
        if (DemoAuthorization.Require(http, options, Permission.ExportEvidencePack) is { } forbidden)
        {
            return forbidden;
        }

        if (!CommandCenterQueryEndpoints.TrySetTenantContext(http, setter, options, out var error))
        {
            return error;
        }

        FileProvider provider;
        if (string.Equals(request.Provider, "google", StringComparison.OrdinalIgnoreCase)) provider = FileProvider.Google;
        else if (string.Equals(request.Provider, "microsoft", StringComparison.OrdinalIgnoreCase)) provider = FileProvider.Microsoft;
        else return Invalid("provider", "Choose Google Drive or OneDrive.");

        if (string.IsNullOrWhiteSpace(request.ObjectKey) || request.ObjectKey.Length > 500)
        {
            return Invalid("objectKey", "Say which export to save.");
        }

        try
        {
            var download = await compliance.DownloadAuditPackAsync(caseId, request.ObjectKey, ct);
            if (download is null)
            {
                return Results.NotFound(new { message = "No exported evidence pack was found for this case with that object key." });
            }

            await using var content = download.Content;
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);
            var saved = await files.UploadAsync(provider, download.FileName, download.ContentType, buffer.ToArray(), ct);

            var context = tenant.Current;
            await audit.WriteAsync(
                new AuditEvent(
                    Guid.NewGuid(), context.Scope, caseId, null, context.UserId, ActorType.User, "evidence_pack.saved_to_storage", clock.GetUtcNow(),
                    null, null, [], "Compliance", null, "sent", null,
                    $"Evidence pack saved to {(provider == FileProvider.Google ? "Google Drive" : "OneDrive")} as \"{saved.Name}\" by the person who exported it.",
                    http.TraceIdentifier),
                ct);

            return Results.Ok(new { saved = new { name = saved.Name, webUrl = saved.WebUrl, provider = saved.Provider } });
        }
        catch (FileConnectionException failure)
        {
            var status = failure.Code switch
            {
                "not_connected" => StatusCodes.Status409Conflict,
                "not_configured" => StatusCodes.Status503ServiceUnavailable,
                "too_large" => StatusCodes.Status413PayloadTooLarge,
                "provider_error" => StatusCodes.Status502BadGateway,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Json(new { code = failure.Code, message = failure.Message }, statusCode: status);
        }
        catch (ComplianceQueryValidationException failure)
        {
            return Invalid(failure.Field, failure.Message);
        }
        catch (ComplianceConfigurationException)
        {
            return Results.BadRequest(new { deliveryStatus = "ConfigurationRequired", message = "Evidence pack storage is unavailable." });
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    private static IResult Invalid(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}
