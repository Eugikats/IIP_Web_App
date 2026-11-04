using InsuranceIntelligence.Application.Products;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Application.Documents;

public sealed record FieldProposalDto(
    Guid ProposalId,
    string FieldKey,
    string FieldLabel,
    string Value,
    string? CurrentValue,
    Guid DocumentId,
    string? DocumentName,
    int? Page,
    decimal Confidence,
    string Status,
    bool ConflictsWithConfirmed,
    string? Warning,
    string? DecidedByUserId,
    DateTimeOffset? DecidedAt,
    string? AcceptedValue);

public enum FieldProposalOutcome
{
    Done,
    NotFound,
    NotPending,
    NeedsReplace,
    Invalid,
}

public sealed record FieldProposalResult(FieldProposalOutcome Outcome, string? Message = null, FieldProposalDto? Proposal = null)
{
    public bool Ok => Outcome == FieldProposalOutcome.Done;
}

public interface IFieldProposalService
{
    /// <summary>Reads the values a document's extraction found and offers the ones that fit the
    /// claim's questions as proposals. Changes nothing on the claim. Returns how many were offered.</summary>
    Task<int> ProposeAsync(CaseDocument document, ClaimCase claimCase, string correlationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FieldProposalDto>> ListAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default);

    /// <summary>An officer puts the proposed value, or their correction of it, on the claim. A value a
    /// person already put there is replaced only when <paramref name="replaceConfirmed"/> says so.</summary>
    Task<FieldProposalResult> AcceptAsync(TenantScope scope, Guid caseId, Guid proposalId, string? correctedValue, bool replaceConfirmed, string actorId, string correlationId, CancellationToken cancellationToken = default);

    Task<FieldProposalResult> RejectAsync(TenantScope scope, Guid caseId, Guid proposalId, string actorId, string correlationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Extracted data into the claim (build-loop item 15). What a document says becomes a proposal for
/// one of the claim's questions, with its source document, page and confidence. Nothing reaches the
/// claim until an officer accepts it, and a person-confirmed answer is never overwritten by a
/// document on its own. Audit events record question keys and decisions, never the values.
/// </summary>
public sealed class FieldProposalService(
    IFieldProposalRepository proposalRepository,
    IClaimFieldService claimFieldService,
    ICaseRepository caseRepository,
    ICaseDocumentRepository documentRepository,
    IDocumentTypeCatalogRepository catalogueRepository,
    IAuditRepository auditRepository) : IFieldProposalService
{
    public async Task<int> ProposeAsync(CaseDocument document, ClaimCase claimCase, string correlationId, CancellationToken cancellationToken = default)
    {
        if (document.IsSuperseded || document.Extraction.Fields.Count == 0)
        {
            return 0;
        }

        var schema = await SchemaForAsync(document, claimCase.Scope, cancellationToken);
        if (schema.Count == 0)
        {
            return 0;
        }

        var definitions = await claimFieldService.DefinitionsForAsync(claimCase, cancellationToken);
        var current = ClaimFieldValues.For(claimCase);
        var existing = await proposalRepository.ListForCaseAsync(claimCase.Scope, claimCase.CaseId, cancellationToken);
        var extracted = document.Extraction.Fields
            .GroupBy(pair => ExtractionSchemas.Normalise(pair.Key))
            .ToDictionary(group => group.Key, group => group.First());

        var offered = new List<string>();
        foreach (var spec in schema)
        {
            var definition = definitions.FirstOrDefault(candidate => string.Equals(candidate.Key, spec.ClaimFieldKey, StringComparison.OrdinalIgnoreCase));
            if (definition is null || !extracted.TryGetValue(ExtractionSchemas.Normalise(spec.ExtractedKey), out var pair) || string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            var raw = pair.Value.Trim();
            var checkedValue = ClaimFieldValidator.Validate([definition], new Dictionary<string, string> { [definition.Key] = raw }, requireRequired: false, customerAudience: false);
            var value = checkedValue.IsValid && checkedValue.Values.TryGetValue(definition.Key, out var normalised) ? normalised : raw;
            var warning = checkedValue.IsValid ? null : checkedValue.Errors.Values.FirstOrDefault();

            var same = existing.Where(proposal =>
                proposal.SourceDocumentId == document.DocumentId
                && string.Equals(proposal.FieldKey, definition.Key, StringComparison.OrdinalIgnoreCase)).ToArray();

            // An officer's earlier decision on this very reading stands.
            if (same.Any(proposal => proposal.Status is FieldProposalStatus.Accepted or FieldProposalStatus.Rejected
                && string.Equals(proposal.Value, value, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // The claim already says the same thing: nothing to propose.
            var hasValue = current.TryGetValue(definition.Key, out var onClaim) && !string.IsNullOrWhiteSpace(onClaim);
            if (hasValue && string.Equals(onClaim, value, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (same.Any(proposal => proposal.Status == FieldProposalStatus.Proposed && string.Equals(proposal.Value, value, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            foreach (var older in same.Where(proposal => proposal.Status == FieldProposalStatus.Proposed))
            {
                await proposalRepository.SaveAsync(older with { Status = FieldProposalStatus.Superseded }, cancellationToken);
            }

            await proposalRepository.SaveAsync(
                new FieldProposal
                {
                    ProposalId = Guid.NewGuid(),
                    CaseId = claimCase.CaseId,
                    Scope = claimCase.Scope,
                    FieldKey = definition.Key,
                    Value = value,
                    SourceDocumentId = document.DocumentId,
                    SourceExtractedKey = pair.Key,
                    Page = document.Extraction.FieldBoundingBoxes is { } boxes && boxes.TryGetValue(pair.Key, out var box) ? box.PageNumber : null,
                    Confidence = document.Extraction.FieldConfidences.TryGetValue(pair.Key, out var confidence) ? confidence : 0m,
                    Status = FieldProposalStatus.Proposed,
                    ConflictsWithConfirmed = hasValue,
                    Warning = warning,
                    CreatedAt = DateTimeOffset.UtcNow,
                },
                cancellationToken);
            offered.Add(definition.Key);
        }

        if (offered.Count > 0)
        {
            await auditRepository.WriteAsync(
                AuditEvent.FieldProposalEvent(claimCase.Scope, claimCase.CaseId, "system", ActorType.System, "claim.fields.proposed", offered, document.DocumentId.ToString(), correlationId),
                cancellationToken);
        }

        return offered.Count;
    }

    public async Task<IReadOnlyList<FieldProposalDto>> ListAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default)
    {
        var claim = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claim is null || claim.Scope != scope)
        {
            return [];
        }

        var proposals = await proposalRepository.ListForCaseAsync(scope, caseId, cancellationToken);
        var documents = (await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken)).ToDictionary(document => document.DocumentId);
        var current = ClaimFieldValues.For(claim);
        return proposals
            .Where(proposal => proposal.Status != FieldProposalStatus.Superseded)
            .OrderBy(proposal => proposal.Status == FieldProposalStatus.Proposed ? 0 : 1)
            .ThenByDescending(proposal => proposal.CreatedAt)
            .Select(proposal => ToDto(proposal, current, documents))
            .ToArray();
    }

    public async Task<FieldProposalResult> AcceptAsync(TenantScope scope, Guid caseId, Guid proposalId, string? correctedValue, bool replaceConfirmed, string actorId, string correlationId, CancellationToken cancellationToken = default)
    {
        var claim = await caseRepository.GetAsync(caseId, cancellationToken);
        var proposal = claim is null || claim.Scope != scope ? null : await proposalRepository.GetAsync(scope, caseId, proposalId, cancellationToken);
        if (claim is null || proposal is null)
        {
            return new FieldProposalResult(FieldProposalOutcome.NotFound, "That suggestion was not found.");
        }

        if (proposal.Status != FieldProposalStatus.Proposed)
        {
            return new FieldProposalResult(FieldProposalOutcome.NotPending, "That suggestion has already been decided.");
        }

        var current = ClaimFieldValues.For(claim);
        if (current.TryGetValue(proposal.FieldKey, out var onClaim) && !string.IsNullOrWhiteSpace(onClaim)
            && !string.Equals(onClaim, correctedValue ?? proposal.Value, StringComparison.OrdinalIgnoreCase) && !replaceConfirmed)
        {
            return new FieldProposalResult(FieldProposalOutcome.NeedsReplace, "The claim already has a different answer put there by a person. Choose to replace it if the document is right.");
        }

        var value = string.IsNullOrWhiteSpace(correctedValue) ? proposal.Value : correctedValue.Trim();
        var corrected = !string.Equals(value, proposal.Value, StringComparison.Ordinal);
        var saved = await claimFieldService.SaveValuesAsync(
            claim,
            new Dictionary<string, string> { [proposal.FieldKey] = value },
            customerAudience: false,
            actorId,
            correlationId,
            cancellationToken,
            new FieldProvenance("Document", actorId, DateTimeOffset.UtcNow, proposal.SourceDocumentId, proposal.Page, proposal.Confidence, corrected));
        if (!saved.IsValid)
        {
            return new FieldProposalResult(FieldProposalOutcome.Invalid, saved.Errors.Values.FirstOrDefault() ?? "That value does not fit the question.");
        }

        var accepted = proposal with { Status = FieldProposalStatus.Accepted, DecidedByUserId = actorId, DecidedAt = DateTimeOffset.UtcNow, AcceptedValue = value };
        await proposalRepository.SaveAsync(accepted, cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.FieldProposalEvent(scope, caseId, actorId, ActorType.User, corrected ? "claim.fields.proposal.corrected" : "claim.fields.proposal.accepted", [proposal.FieldKey], proposal.SourceDocumentId.ToString(), correlationId),
            cancellationToken);
        var documents = (await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken)).ToDictionary(document => document.DocumentId);
        return new FieldProposalResult(FieldProposalOutcome.Done, null, ToDto(accepted, ClaimFieldValues.For(saved.Claim!), documents));
    }

    public async Task<FieldProposalResult> RejectAsync(TenantScope scope, Guid caseId, Guid proposalId, string actorId, string correlationId, CancellationToken cancellationToken = default)
    {
        var claim = await caseRepository.GetAsync(caseId, cancellationToken);
        var proposal = claim is null || claim.Scope != scope ? null : await proposalRepository.GetAsync(scope, caseId, proposalId, cancellationToken);
        if (claim is null || proposal is null)
        {
            return new FieldProposalResult(FieldProposalOutcome.NotFound, "That suggestion was not found.");
        }

        if (proposal.Status != FieldProposalStatus.Proposed)
        {
            return new FieldProposalResult(FieldProposalOutcome.NotPending, "That suggestion has already been decided.");
        }

        var rejected = proposal with { Status = FieldProposalStatus.Rejected, DecidedByUserId = actorId, DecidedAt = DateTimeOffset.UtcNow };
        await proposalRepository.SaveAsync(rejected, cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.FieldProposalEvent(scope, caseId, actorId, ActorType.User, "claim.fields.proposal.rejected", [proposal.FieldKey], proposal.SourceDocumentId.ToString(), correlationId),
            cancellationToken);
        var documents = (await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken)).ToDictionary(document => document.DocumentId);
        return new FieldProposalResult(FieldProposalOutcome.Done, null, ToDto(rejected, ClaimFieldValues.For(claim), documents));
    }

    private async Task<IReadOnlyList<ExtractionFieldSpec>> SchemaForAsync(CaseDocument document, TenantScope scope, CancellationToken cancellationToken)
    {
        var key = document.EffectiveKey;
        var definition = await catalogueRepository.GetCurrentAsync(scope, key, cancellationToken);
        if (definition?.ExtractionFields is { Count: > 0 } configured)
        {
            return configured;
        }

        return Enum.TryParse<DocumentType>(key, ignoreCase: true, out var builtIn) ? ExtractionSchemas.Defaults(builtIn) : [];
    }

    private static FieldProposalDto ToDto(FieldProposal proposal, IReadOnlyDictionary<string, string> current, IReadOnlyDictionary<Guid, CaseDocument> documents) =>
        new(
            proposal.ProposalId,
            proposal.FieldKey,
            ClaimFieldValues.LabelFor(proposal.FieldKey),
            proposal.Value,
            current.TryGetValue(proposal.FieldKey, out var onClaim) ? onClaim : null,
            proposal.SourceDocumentId,
            documents.TryGetValue(proposal.SourceDocumentId, out var document) ? document.DisplayName ?? document.OriginalFileName : null,
            proposal.Page,
            proposal.Confidence,
            proposal.Status.ToString(),
            proposal.ConflictsWithConfirmed,
            proposal.Warning,
            proposal.DecidedByUserId,
            proposal.DecidedAt,
            proposal.AcceptedValue);
}
