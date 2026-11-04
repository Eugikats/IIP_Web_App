using System.Security.Cryptography;
using InsuranceIntelligence.Api.Documents;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Cases;
using InsuranceIntelligence.Application.Compliance;
using InsuranceIntelligence.Application.Documents;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Contestability;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Uploads;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Net.Http.Headers;

namespace InsuranceIntelligence.Api.ClientPortal;

public sealed record StartUploadRequest(string? FileName, string? ContentType, long Size, string? DocumentTypeKey, string? Fingerprint);

public sealed record CompleteUploadRequest(string? Sha256);

/// <summary>
/// Sending a file in pieces so a slow or dropped connection does not lose it (build-loop item 23). The
/// claimant starts an upload for one file, sends its quarter-megabyte pieces in any order and any number of
/// times, asks which pieces have arrived (so a reload or a lost signal picks up where it stopped), and finishes
/// it. Finishing puts the pieces back together and hands the file to exactly the same code as a normal upload,
/// so consent, slot rules, the untouched original, its hash and the reading all apply unchanged. Everything is
/// limited to the claimant's own claim and session.
/// </summary>
public static class ResumableUploadEndpoints
{
    public static async Task<IResult> Start(
        Guid caseId,
        StartUploadRequest request,
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IDocumentChecklistProvider checklistProvider,
        IUploadSessionRepository sessions,
        IUploadPartStore parts,
        IClaimantConsentService consentService,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (claim, scope, actor, failure) = await ResolveClaimAsync(caseId, caseRepository, options, httpContext, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!await consentService.HasConsentAsync(scope, actor, cancellationToken))
        {
            return ClaimantConsentEndpoints.ConsentRequired();
        }

        var fileName = request?.FileName?.Trim();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return Validation("fileName", "The file needs a name.");
        }

        var sizeError = DocumentUploadValidation.Validate(request!.Size, fileName);
        if (sizeError is not null)
        {
            return Validation("files", sizeError);
        }

        var documentTypeKey = string.IsNullOrWhiteSpace(request.DocumentTypeKey) ? null : request.DocumentTypeKey.Trim();
        if (documentTypeKey is not null)
        {
            var existing = await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken);
            var slot = (await checklistProvider.BuildChecklistAsync(claim!, existing, cancellationToken))
                .Slots.FirstOrDefault(candidate => string.Equals(candidate.Key, documentTypeKey, StringComparison.OrdinalIgnoreCase));
            if (slot is null)
            {
                return Validation("documentTypeKey", "That is not one of the documents asked for on this claim.");
            }

            var slotError = ClientPortalEndpoints.SlotUploadError(slot, [new DeclaredFormFile(fileName, request.Size)], existing);
            if (slotError is not null)
            {
                return Validation("files", slotError);
            }
        }

        await ExpireStaleAsync(scope, caseId, sessions, parts, cancellationToken);

        var fingerprint = string.IsNullOrWhiteSpace(request.Fingerprint) ? $"{fileName}:{request.Size}:{documentTypeKey}" : request.Fingerprint.Trim();
        var open = await sessions.FindOpenAsync(scope, caseId, actor, fingerprint, cancellationToken);
        if (open is not null && open.Size == request.Size)
        {
            return Results.Ok(await DescribeAsync(open, parts, cancellationToken));
        }

        var now = DateTimeOffset.UtcNow;
        var session = new UploadSession
        {
            UploadId = Guid.NewGuid(),
            Scope = scope,
            CaseId = caseId,
            ActorId = actor,
            FileName = fileName,
            ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType.Trim(),
            Size = request.Size,
            ChunkSize = ResumableUploadPolicy.ChunkSize,
            TotalChunks = ResumableUploadPolicy.TotalChunks(request.Size),
            DocumentTypeKey = documentTypeKey,
            Fingerprint = fingerprint,
            CreatedAt = now,
            ExpiresAt = now.Add(ResumableUploadPolicy.SessionLifetime),
        };
        await sessions.SaveAsync(session, cancellationToken);
        return Results.Ok(await DescribeAsync(session, parts, cancellationToken));
    }

    public static async Task<IResult> Status(
        Guid caseId,
        Guid uploadId,
        ICaseRepository caseRepository,
        IUploadSessionRepository sessions,
        IUploadPartStore parts,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (session, failure) = await ResolveSessionAsync(caseId, uploadId, caseRepository, sessions, options, httpContext, cancellationToken);
        return failure ?? Results.Ok(await DescribeAsync(session!, parts, cancellationToken));
    }

    public static async Task<IResult> PutChunk(
        Guid caseId,
        Guid uploadId,
        int index,
        ICaseRepository caseRepository,
        IUploadSessionRepository sessions,
        IUploadPartStore parts,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (session, failure) = await ResolveSessionAsync(caseId, uploadId, caseRepository, sessions, options, httpContext, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (session!.State != UploadSessionState.Open || ResumableUploadPolicy.IsExpired(session, DateTimeOffset.UtcNow))
        {
            return Results.Json(new { message = "This upload has ended. Start it again." }, statusCode: StatusCodes.Status410Gone);
        }

        var expected = ResumableUploadPolicy.ExpectedLength(session.Size, index);
        if (expected < 0)
        {
            return Validation("index", "That piece does not exist for this file.");
        }

        // Read no more than the piece may hold, plus one byte to notice a piece that is too long.
        var buffer = new byte[expected + 1];
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await httpContext.Request.Body.ReadAsync(buffer.AsMemory(read), cancellationToken);
            if (count == 0)
            {
                break;
            }

            read += count;
        }

        if (read != expected)
        {
            return Validation("body", $"Piece {index} must be {expected} bytes, but {(read > expected ? "more than that" : read.ToString())} arrived.");
        }

        await parts.SavePartAsync(session.Scope, uploadId, index, buffer.AsSpan(0, expected).ToArray(), cancellationToken);
        return Results.Ok(new { index, received = (await parts.ListIndexesAsync(session.Scope, uploadId, cancellationToken)).Count, totalChunks = session.TotalChunks });
    }

    public static async Task<IResult> Complete(
        Guid caseId,
        Guid uploadId,
        CompleteUploadRequest request,
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IDocumentStorageProvider storageProvider,
        IDocumentProcessingProvider processingProvider,
        IDocumentChecklistProvider checklistProvider,
        IDocumentFieldValidationRuleRepository fieldValidationRuleRepository,
        ICommunicationRepository communicationRepository,
        IClaimReviewRequestRepository reviewRequestRepository,
        IAuditRepository auditRepository,
        IUploadSessionRepository sessions,
        IUploadPartStore parts,
        IClaimantConsentService consentService,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        IImageDerivativeProvider? derivativeProvider = null,
        ISlotPdfService? slotPdfService = null,
        ISlotPdfRepository? slotPdfRepository = null,
        IClaimantStatusPresenter? claimantStatus = null,
        ILoggerFactory? derivativeLoggerFactory = null)
    {
        var (session, failure) = await ResolveSessionAsync(caseId, uploadId, caseRepository, sessions, options, httpContext, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var claim = (await caseRepository.GetAsync(caseId, cancellationToken))!;

        // Finishing twice (the first answer was lost on the way back) must not store the file twice.
        if (session!.State == UploadSessionState.Completed)
        {
            return await ClientPortalEndpoints.GetClaim(
                caseId, caseRepository, documentRepository, checklistProvider, communicationRepository, reviewRequestRepository,
                options, httpContext, cancellationToken, auditRepository, slotPdfRepository, claimantStatus);
        }

        if (session.State != UploadSessionState.Open || ResumableUploadPolicy.IsExpired(session, DateTimeOffset.UtcNow))
        {
            return Results.Json(new { message = "This upload has ended. Start it again." }, statusCode: StatusCodes.Status410Gone);
        }

        if (!await consentService.HasConsentAsync(session.Scope, session.ActorId, cancellationToken))
        {
            return ClaimantConsentEndpoints.ConsentRequired();
        }

        var received = await parts.ListIndexesAsync(session.Scope, uploadId, cancellationToken);
        var missing = Enumerable.Range(0, session.TotalChunks).Except(received).ToArray();
        if (missing.Length > 0)
        {
            return Results.Conflict(new { message = "Some pieces have not arrived yet.", missing });
        }

        var assembled = new MemoryStream(capacity: (int)session.Size);
        for (var index = 0; index < session.TotalChunks; index++)
        {
            var part = await parts.ReadPartAsync(session.Scope, uploadId, index, cancellationToken);
            if (part is null)
            {
                return Results.Conflict(new { message = "Some pieces have not arrived yet.", missing = new[] { index } });
            }

            assembled.Write(part);
        }

        if (assembled.Length != session.Size)
        {
            return Results.Conflict(new { message = "The pieces do not add up to the file that was announced. Start it again." });
        }

        if (!string.IsNullOrWhiteSpace(request?.Sha256))
        {
            var actual = Convert.ToHexString(SHA256.HashData(assembled.GetBuffer().AsSpan(0, (int)assembled.Length))).ToLowerInvariant();
            if (!string.Equals(actual, request.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                // Something changed on the way. Throw the pieces away so a fresh start is clean.
                await parts.DeleteAllAsync(session.Scope, uploadId, cancellationToken);
                await sessions.SaveAsync(session with { State = UploadSessionState.Cancelled }, cancellationToken);
                return Results.Conflict(new { message = "The file arrived damaged. Please send it again." });
            }
        }

        assembled.Position = 0;
        var file = new AssembledFormFile(session.FileName, session.ContentType, assembled);
        var result = await ClientPortalEndpoints.StoreUploadedFilesAsync(
            caseId, session.Scope, claim, [file], session.DocumentTypeKey, caseRepository, documentRepository, storageProvider, processingProvider,
            checklistProvider, fieldValidationRuleRepository, communicationRepository, auditRepository, httpContext, derivativeProvider,
            slotPdfService, slotPdfRepository, claimantStatus, derivativeLoggerFactory, cancellationToken);

        if (result is IStatusCodeHttpResult { StatusCode: >= 200 and < 300 })
        {
            await sessions.SaveAsync(session with { State = UploadSessionState.Completed }, cancellationToken);
            await parts.DeleteAllAsync(session.Scope, uploadId, cancellationToken);
        }

        return result;
    }

    public static async Task<IResult> Cancel(
        Guid caseId,
        Guid uploadId,
        ICaseRepository caseRepository,
        IUploadSessionRepository sessions,
        IUploadPartStore parts,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (session, failure) = await ResolveSessionAsync(caseId, uploadId, caseRepository, sessions, options, httpContext, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        await parts.DeleteAllAsync(session!.Scope, uploadId, cancellationToken);
        if (session.State == UploadSessionState.Open)
        {
            await sessions.SaveAsync(session with { State = UploadSessionState.Cancelled }, cancellationToken);
        }

        return Results.NoContent();
    }

    private static async Task<(ClaimCase? Claim, TenantScope Scope, string Actor, IResult? Failure)> ResolveClaimAsync(
        Guid caseId,
        ICaseRepository caseRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!ClientPortalEndpoints.ResolveScope(httpContext, out var scope, out var validationError))
        {
            return (null, default!, string.Empty, Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [validationError] }));
        }

        var claim = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claim is null || claim.Scope != scope || !ClientPortalEndpoints.BelongsToCustomer(claim, httpContext, options))
        {
            return (null, scope, string.Empty, Results.NotFound(new { message = "Claim was not found for the signed-in customer." }));
        }

        return (claim, scope, ClientPortalEndpoints.ResolveActor(httpContext), null);
    }

    private static async Task<(UploadSession? Session, IResult? Failure)> ResolveSessionAsync(
        Guid caseId,
        Guid uploadId,
        ICaseRepository caseRepository,
        IUploadSessionRepository sessions,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (_, scope, actor, failure) = await ResolveClaimAsync(caseId, caseRepository, options, httpContext, cancellationToken);
        if (failure is not null)
        {
            return (null, failure);
        }

        var session = await sessions.GetAsync(scope, uploadId, cancellationToken);
        return session is null || session.CaseId != caseId || session.ActorId != actor
            ? (null, Results.NotFound(new { message = "That upload was not found." }))
            : (session, null);
    }

    private static async Task<object> DescribeAsync(UploadSession session, IUploadPartStore parts, CancellationToken cancellationToken) =>
        new
        {
            uploadId = session.UploadId,
            state = session.State.ToString(),
            size = session.Size,
            chunkSize = session.ChunkSize,
            totalChunks = session.TotalChunks,
            received = await parts.ListIndexesAsync(session.Scope, session.UploadId, cancellationToken),
            expiresAt = session.ExpiresAt,
        };

    /// <summary>Open sessions past their time are closed and their pieces deleted, whenever the same claim starts another.</summary>
    private static async Task ExpireStaleAsync(TenantScope scope, Guid caseId, IUploadSessionRepository sessions, IUploadPartStore parts, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var stale in (await sessions.ListForCaseAsync(scope, caseId, cancellationToken)).Where(item => item.State == UploadSessionState.Open && ResumableUploadPolicy.IsExpired(item, now)))
        {
            await parts.DeleteAllAsync(scope, stale.UploadId, cancellationToken);
            await sessions.SaveAsync(stale with { State = UploadSessionState.Cancelled }, cancellationToken);
        }
    }

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}

/// <summary>What the claimant said they would send, enough for the slot rules to be checked before any bytes arrive.</summary>
internal sealed class DeclaredFormFile(string fileName, long length) : IFormFile
{
    public string ContentType => string.Empty;
    public string ContentDisposition => string.Empty;
    public IHeaderDictionary Headers => new HeaderDictionary();
    public long Length => length;
    public string Name => "files";
    public string FileName => fileName;
    public void CopyTo(Stream target) => throw new NotSupportedException();
    public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Stream OpenReadStream() => throw new NotSupportedException();
}

/// <summary>A file put back together from its pieces, presented the way a multipart upload presents one.</summary>
internal sealed class AssembledFormFile(string fileName, string contentType, MemoryStream content) : IFormFile
{
    public string ContentType => contentType;
    public string ContentDisposition => $"form-data; name=\"files\"; filename=\"{fileName}\"";
    public IHeaderDictionary Headers => new HeaderDictionary();
    public long Length => content.Length;
    public string Name => "files";
    public string FileName => fileName;
    public void CopyTo(Stream target) => content.WriteTo(target);
    public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) => target.WriteAsync(content.GetBuffer().AsMemory(0, (int)content.Length), cancellationToken).AsTask();
    public Stream OpenReadStream() => new MemoryStream(content.GetBuffer(), 0, (int)content.Length, writable: false);
}
