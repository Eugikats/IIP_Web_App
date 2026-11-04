using InsuranceIntelligence.Api.Documents;
using InsuranceIntelligence.Application.Cases;
using InsuranceIntelligence.Application.Compliance;
using InsuranceIntelligence.Application.Customers;
using InsuranceIntelligence.Application.Documents;
using InsuranceIntelligence.Application.Products;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.ClientPortal;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Products;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.ClientPortal;

public sealed record UseDocumentOnFileRequest(Guid SourceDocumentId, string? DocumentTypeKey);

public sealed record UseDetailsOnFileRequest(IReadOnlyList<string>? Keys);

/// <summary>
/// A returning customer is offered what they already gave (build-loop item 26): the details and documents from their earlier
/// claims in the same workspace, never anyone else's. Details are offered to confirm; a document is copied into this claim only when
/// the customer presses "use the one on file", and then goes through exactly the same checks and reading as a file they just sent.
/// </summary>
public static class CustomerMemoryEndpoints
{
    public static async Task<IResult> GetOnFile(
        Guid caseId,
        ICaseRepository caseRepository,
        ICustomerMemoryService memory,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (claim, failure) = await OwnClaimAsync(caseId, caseRepository, options, httpContext, cancellationToken);
        return failure ?? Results.Ok(ToDto(await memory.ForClaimAsync(claim!, cancellationToken)));
    }

    public static async Task<IResult> GetKnownDetails(
        string? productLine,
        string? claimType,
        ICustomerMemoryService memory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Items.TryGetValue(ClaimantSessionEndpointFilter.SessionItemKey, out var stashed) || stashed is not ClaimantPortalSession session)
        {
            return Results.Unauthorized();
        }

        // Only a link made for starting a claim, which is bound to a verified policy and contact, may ask before a claim exists.
        if (session.CaseId is not null)
        {
            return Results.Conflict(new { message = "This access link is for an existing claim." });
        }

        var contact = session.ContactValue;
        var found = await memory.ForNewClaimAsync(
            session.Scope, session.PolicyNumber, contact.Contains('@') ? null : contact, contact.Contains('@') ? contact : null, productLine, claimType, cancellationToken);
        return Results.Ok(ToDto(found with { Documents = [] }));
    }

    public static async Task<IResult> UseDetails(
        Guid caseId,
        UseDetailsOnFileRequest request,
        ICaseRepository caseRepository,
        ICustomerMemoryService memory,
        IClaimFieldService claimFieldService,
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (claim, failure) = await OwnClaimAsync(caseId, caseRepository, options, httpContext, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var wanted = (request?.Keys ?? []).Where(key => !string.IsNullOrWhiteSpace(key)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (wanted.Count == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["keys"] = ["Choose at least one detail to use."] });
        }

        var offered = (await memory.ForClaimAsync(claim!, cancellationToken)).Values.Where(value => wanted.Contains(value.Key)).ToArray();
        if (offered.Length != wanted.Count)
        {
            return Results.Conflict(new { message = "One of those details is no longer on file for this claim." });
        }

        var actor = ClientPortalEndpoints.ResolveActor(httpContext);
        var saved = await claimFieldService.SaveValuesAsync(
            claim!, offered.ToDictionary(value => value.Key, value => value.Value), customerAudience: true, actor, httpContext.TraceIdentifier, cancellationToken,
            new FieldProvenance("Customer", actor, DateTimeOffset.UtcNow));
        if (!saved.IsValid)
        {
            return Results.ValidationProblem(saved.Errors.ToDictionary(error => error.Key, error => new[] { error.Value }));
        }

        await auditRepository.WriteAsync(AuditEvent.CustomerMemoryUsed(claim!.Scope, caseId, actor, "details", offered.Length, httpContext.TraceIdentifier), cancellationToken);
        return Results.Ok(ToDto(await memory.ForClaimAsync(saved.Claim ?? claim, cancellationToken)));
    }

    public static async Task<IResult> UseDocument(
        Guid caseId,
        UseDocumentOnFileRequest request,
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IDocumentStorageProvider storageProvider,
        IDocumentProcessingProvider processingProvider,
        IDocumentChecklistProvider checklistProvider,
        IDocumentFieldValidationRuleRepository fieldValidationRuleRepository,
        ICommunicationRepository communicationRepository,
        IAuditRepository auditRepository,
        ICustomerMemoryService memory,
        InsuranceIntelligence.Application.Compliance.IClaimantConsentService consentService,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        IImageDerivativeProvider? derivativeProvider = null,
        ISlotPdfService? slotPdfService = null,
        ISlotPdfRepository? slotPdfRepository = null,
        IClaimantStatusPresenter? claimantStatus = null,
        ILoggerFactory? derivativeLoggerFactory = null)
    {
        var (claim, failure) = await OwnClaimAsync(caseId, caseRepository, options, httpContext, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var actor = ClientPortalEndpoints.ResolveActor(httpContext);
        if (!await consentService.HasConsentAsync(claim!.Scope, actor, cancellationToken))
        {
            return ClaimantConsentEndpoints.ConsentRequired();
        }

        var slotKey = request?.DocumentTypeKey?.Trim();
        if (string.IsNullOrWhiteSpace(slotKey))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["documentTypeKey"] = ["Say which document this is for."] });
        }

        var found = await memory.FindDocumentAsync(claim, request!.SourceDocumentId, slotKey, cancellationToken);
        if (found is null)
        {
            return Results.NotFound(new { message = "That document is not on file for you, or this claim does not ask for it." });
        }

        var (offer, source) = found.Value;
        byte[] bytes;
        await using (var stream = await storageProvider.ReadAsync(claim.Scope, source.ObjectKey, cancellationToken))
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            bytes = buffer.ToArray();
        }

        var content = new MemoryStream(bytes.Length);
        content.Write(bytes);
        var file = new AssembledFormFile(source.OriginalFileName, string.IsNullOrWhiteSpace(source.ContentType) ? "application/octet-stream" : source.ContentType, content);
        var result = await ClientPortalEndpoints.StoreUploadedFilesAsync(
            caseId, claim.Scope, claim, [file], slotKey, caseRepository, documentRepository, storageProvider, processingProvider, checklistProvider,
            fieldValidationRuleRepository, communicationRepository, auditRepository, httpContext, derivativeProvider, slotPdfService, slotPdfRepository, claimantStatus, derivativeLoggerFactory, cancellationToken,
            reusedFromClaim: offer.FromClaimReference); // staff see on the copy that it was reused, and from which claim
        if (result is not Microsoft.AspNetCore.Http.IStatusCodeHttpResult { StatusCode: >= 200 and < 300 })
        {
            return result;
        }

        await auditRepository.WriteAsync(AuditEvent.CustomerMemoryUsed(claim.Scope, caseId, actor, "document", 1, httpContext.TraceIdentifier), cancellationToken);
        return result;
    }

    private static async Task<(ClaimCase? Claim, IResult? Failure)> OwnClaimAsync(
        Guid caseId, ICaseRepository caseRepository, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (!ClientPortalEndpoints.ResolveScope(httpContext, out var scope, out var validationError))
        {
            return (null, Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [validationError] }));
        }

        var claim = await caseRepository.GetAsync(caseId, cancellationToken);
        return claim is null || claim.Scope != scope || !ClientPortalEndpoints.BelongsToCustomer(claim, httpContext, options)
            ? (null, Results.NotFound(new { message = "Claim was not found for the signed-in customer." }))
            : (claim, null);
    }

    private static object ToDto(CustomerMemory memory) => new
    {
        values = memory.Values.Select(value => new { key = value.Key, label = value.Label, value = value.Value, fromClaim = value.FromClaimReference, at = value.At }),
        documents = memory.Documents.Select(document => new { documentId = document.DocumentId, documentTypeKey = document.SlotKey, label = document.SlotLabel, fileName = document.FileName, fromClaim = document.FromClaimReference, uploadedAt = document.UploadedAt }),
    };
}
