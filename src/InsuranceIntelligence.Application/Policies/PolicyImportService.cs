using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Application.Policies;

/// <summary>Outcome N: "upload an export of policies... map columns on screen, see a validation
/// report, load read only." The file itself was already parsed into headers/rows before this
/// runs (InsuranceIntelligence.Api.Policies.PolicyImportFileParser); this is the second half,
/// applying the confirmed column mapping and PolicyImportRowValidator's deterministic rules row by
/// row, then saving only the rows that passed.</summary>
public sealed class PolicyImportService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    IPolicyRepository policyRepository,
    IAuditWriter auditWriter) : IPolicyImportService
{
    public async Task<PolicyImportResult> CommitAsync(PolicyImportCommitRequest request, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageProducts, cancellationToken);

        if (request is null || request.Rows.Count == 0)
        {
            throw new PolicyImportValidationException("rows", "No rows to import.");
        }

        var mappedFields = request.ColumnMapping.Values.ToHashSet(StringComparer.Ordinal);
        var missingRequired = PolicyImportFields.Required.Where(field => !mappedFields.Contains(field)).ToArray();
        if (missingRequired.Length > 0)
        {
            throw new PolicyImportValidationException(
                "mapping",
                $"{string.Join(", ", missingRequired.Select(PolicyImportFields.HumanLabel))} must be mapped to a column before importing.");
        }

        var context = tenantContextAccessor.Current;
        var existingPolicies = await policyRepository.ListAsync(context.Scope, cancellationToken);
        var existingIdByNumber = existingPolicies
            .GroupBy(policy => policy.PolicyNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().PolicyId, StringComparer.OrdinalIgnoreCase);

        var now = DateTimeOffset.UtcNow;
        var errors = new List<PolicyImportRowError>();
        var importedCount = 0;

        for (var index = 0; index < request.Rows.Count; index++)
        {
            // Row 1 is the header; the first data row is row 2, matching what a person sees if
            // they open the file themselves, so an error message points at a real row they can find.
            var rowNumber = index + 2;
            var mappedRow = BuildMappedRow(request.Headers, request.ColumnMapping, request.Rows[index]);
            var policyNumber = mappedRow.GetValueOrDefault(PolicyImportFields.PolicyNumber);
            var existingId = !string.IsNullOrWhiteSpace(policyNumber) && existingIdByNumber.TryGetValue(policyNumber, out var foundId)
                ? foundId
                : Guid.NewGuid().ToString();

            var outcome = PolicyImportRowValidator.Validate(context.Scope, rowNumber, mappedRow, existingId, now);
            if (!outcome.IsValid)
            {
                errors.Add(new PolicyImportRowError(rowNumber, outcome.Errors));
                continue;
            }

            await policyRepository.UpsertAsync(outcome.Policy!, cancellationToken);
            // A later row in the same file for the same policy number refreshes the row this loop
            // already wrote, rather than creating a second record for it.
            existingIdByNumber[outcome.Policy!.PolicyNumber] = outcome.Policy.PolicyId;
            importedCount++;
        }

        var correlationId = Guid.NewGuid().ToString("N");
        await auditWriter.WriteAsync(
            AuditEvent.PolicyImportCommitted(context.Scope, context.UserId, request.Rows.Count, importedCount, errors.Count, correlationId),
            cancellationToken);

        return new PolicyImportResult(request.Rows.Count, importedCount, errors.Count, errors);
    }

    private static IReadOnlyDictionary<string, string> BuildMappedRow(
        IReadOnlyList<string> headers,
        IReadOnlyDictionary<string, string> columnMapping,
        IReadOnlyList<string> rawRow)
    {
        var mapped = new Dictionary<string, string>();
        for (var column = 0; column < headers.Count && column < rawRow.Count; column++)
        {
            if (columnMapping.TryGetValue(headers[column], out var field) && !string.IsNullOrWhiteSpace(field))
            {
                mapped[field] = rawRow[column];
            }
        }

        return mapped;
    }
}
