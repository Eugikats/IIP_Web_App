using InsuranceIntelligence.Api.CommandCenter;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Integrations;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Integrations;

/// <summary>
/// A person's own Google Drive and OneDrive connection (<c>/api/v1/me/file-connections</c>). It is
/// self-scoped like <c>/api/v1/me</c>: whoever is signed in connects, browses and reads only their own
/// account, so it needs no organisation permission and never reaches another person's files. Tokens stay
/// on the server; a browser only ever learns whether a connection exists and for which account.
/// </summary>
public static class FileConnectionEndpoints
{
    public sealed record StartRequest(string? ReturnTo, bool SharePoint = false);

    public sealed record ExchangeRequest(string? Code, string? State);

    public static Task<IResult> Status(IUserFileService files, ITenantContextSetter setter, InfrastructureOptions options, HttpContext http, CancellationToken ct) =>
        Run(setter, options, http, async () => Results.Ok(await files.StatusAsync(ct)));

    public static Task<IResult> Start(string provider, StartRequest? request, IUserFileService files, ITenantContextSetter setter, InfrastructureOptions options, HttpContext http, CancellationToken ct) =>
        Run(setter, options, http, async () =>
            Results.Ok(await files.StartAsync(Parse(provider), request?.ReturnTo, request?.SharePoint ?? false, ct)));

    public static Task<IResult> Exchange(string provider, ExchangeRequest request, IUserFileService files, ITenantContextSetter setter, InfrastructureOptions options, HttpContext http, CancellationToken ct) =>
        Run(setter, options, http, async () =>
            Results.Ok(await files.ExchangeAsync(Parse(provider), request.Code ?? string.Empty, request.State ?? string.Empty, ct)));

    public static Task<IResult> Disconnect(string provider, IUserFileService files, ITenantContextSetter setter, InfrastructureOptions options, HttpContext http, CancellationToken ct) =>
        Run(setter, options, http, async () =>
        {
            await files.DisconnectAsync(Parse(provider), ct);
            return Results.NoContent();
        });

    public static Task<IResult> Browse(string provider, string? folderId, IUserFileService files, ITenantContextSetter setter, InfrastructureOptions options, HttpContext http, CancellationToken ct) =>
        Run(setter, options, http, async () => Results.Ok(await files.BrowseAsync(Parse(provider), folderId, ct)));

    public static Task<IResult> Content(string provider, string fileId, IUserFileService files, ITenantContextSetter setter, InfrastructureOptions options, HttpContext http, CancellationToken ct) =>
        Run(setter, options, http, async () =>
        {
            var file = await files.DownloadAsync(Parse(provider), fileId, ct);
            http.Response.Headers["X-File-Name"] = Uri.EscapeDataString(file.Name);
            http.Response.Headers.CacheControl = "no-store";
            return Results.File(file.Content, file.ContentType);
        });

    public static Task<IResult> Picker(IUserFileService files, ITenantContextSetter setter, InfrastructureOptions options, HttpContext http, CancellationToken ct) =>
        Run(setter, options, http, async () =>
        {
            http.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await files.PickerSessionAsync(ct));
        });

    private static FileProvider Parse(string provider) => provider.ToLowerInvariant() switch
    {
        "google" => FileProvider.Google,
        "microsoft" => FileProvider.Microsoft,
        _ => throw new FileConnectionException("bad_request", "Unknown file provider."),
    };

    private static async Task<IResult> Run(ITenantContextSetter setter, InfrastructureOptions options, HttpContext http, Func<Task<IResult>> action)
    {
        if (!CommandCenterQueryEndpoints.TrySetTenantContext(http, setter, options, out var error))
        {
            return error;
        }

        try
        {
            return await action();
        }
        catch (FileConnectionException ex)
        {
            var status = ex.Code switch
            {
                "not_connected" => StatusCodes.Status409Conflict,
                "not_configured" => StatusCodes.Status503ServiceUnavailable,
                "not_found" => StatusCodes.Status404NotFound,
                "too_large" => StatusCodes.Status413PayloadTooLarge,
                "provider_error" => StatusCodes.Status502BadGateway,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Json(new { code = ex.Code, message = ex.Message }, statusCode: status);
        }
    }
}
