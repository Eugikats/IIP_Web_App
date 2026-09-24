using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Policies;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.LocalTestDoubles;

namespace InsuranceIntelligence.Api.Tests;

public sealed class PolicyImportServiceTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");

    private static readonly IReadOnlyList<string> Headers = ["Policy Number", "Product", "Cover Start", "Cover End"];

    private static readonly IReadOnlyDictionary<string, string> Mapping = new Dictionary<string, string>
    {
        ["Policy Number"] = PolicyImportFields.PolicyNumber,
        ["Product"] = PolicyImportFields.ProductLine,
        ["Cover Start"] = PolicyImportFields.EffectiveFrom,
        ["Cover End"] = PolicyImportFields.EffectiveTo,
    };

    [Fact]
    public async Task CommitAsync_ValidRows_ImportsAllAndAudits()
    {
        var (service, policies, audit) = Build();
        List<IReadOnlyList<string>> rows =
        [
            ["POL-2001", "Motor", "2026-01-01", "2026-12-31"],
            ["POL-2002", "Marine", "2026-02-01", "2027-01-31"],
        ];

        var result = await service.CommitAsync(new PolicyImportCommitRequest(Headers, Mapping, rows));

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Empty(result.Errors);

        var saved = await policies.ListAsync(Scope, CancellationToken.None);
        Assert.Equal(2, saved.Count);
        Assert.All(saved, policy => Assert.Equal(PolicyRecordSource.FileImport, policy.RecordSource));
        Assert.All(saved, policy => Assert.NotNull(policy.LastRefreshedAt));

        Assert.Contains(audit.Written, auditEvent => auditEvent.ActionName == "policy.import.committed");
    }

    [Fact]
    public async Task CommitAsync_RowWithBadDate_IsReportedAsAFailureWithoutBlockingOtherRows()
    {
        var (service, policies, _) = Build();
        List<IReadOnlyList<string>> rows =
        [
            ["POL-2003", "Motor", "not-a-date", "2026-12-31"],
            ["POL-2004", "Marine", "2026-02-01", "2027-01-31"],
        ];

        var result = await service.CommitAsync(new PolicyImportCommitRequest(Headers, Mapping, rows));

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.FailedCount);
        var failure = Assert.Single(result.Errors);
        Assert.Equal(2, failure.RowNumber);

        var saved = await policies.ListAsync(Scope, CancellationToken.None);
        Assert.Single(saved);
        Assert.Equal("POL-2004", saved.Single().PolicyNumber);
    }

    [Fact]
    public async Task CommitAsync_ReimportOfAnExistingPolicyNumber_RefreshesInPlaceRatherThanDuplicating()
    {
        var (service, policies, _) = Build();
        await policies.UpsertAsync(
            new Policy("existing-guid-1", Scope.TenantId, Scope.WorkspaceId, "POL-2005", "Motor", null, null, null, null, null),
            CancellationToken.None);

        List<IReadOnlyList<string>> rows = [["POL-2005", "Motor", "2026-03-01", "2027-02-28"]];
        var result = await service.CommitAsync(new PolicyImportCommitRequest(Headers, Mapping, rows));

        Assert.Equal(1, result.ImportedCount);
        var saved = await policies.ListAsync(Scope, CancellationToken.None);
        var single = Assert.Single(saved);
        Assert.Equal("existing-guid-1", single.PolicyId);
        Assert.Equal(PolicyRecordSource.FileImport, single.RecordSource);
    }

    [Fact]
    public async Task CommitAsync_DuplicatePolicyNumberWithinTheSameFile_RefreshesRatherThanDuplicates()
    {
        var (service, policies, _) = Build();
        List<IReadOnlyList<string>> rows =
        [
            ["POL-2006", "Motor", "2026-01-01", "2026-12-31"],
            ["POL-2006", "Motor", "2026-06-01", "2027-05-31"],
        ];

        var result = await service.CommitAsync(new PolicyImportCommitRequest(Headers, Mapping, rows));

        Assert.Equal(2, result.ImportedCount);
        var saved = await policies.ListAsync(Scope, CancellationToken.None);
        var single = Assert.Single(saved);
        Assert.Equal(new DateOnly(2026, 6, 1), single.EffectiveFrom);
    }

    [Fact]
    public async Task CommitAsync_MissingRequiredMapping_ThrowsValidationBeforeProcessingAnyRow()
    {
        var (service, policies, _) = Build();
        var incompleteMapping = new Dictionary<string, string> { ["Policy Number"] = PolicyImportFields.PolicyNumber };
        List<IReadOnlyList<string>> rows = [["POL-2007"]];

        var ex = await Assert.ThrowsAsync<PolicyImportValidationException>(() =>
            service.CommitAsync(new PolicyImportCommitRequest(["Policy Number"], incompleteMapping, rows)));

        Assert.Equal("mapping", ex.Field);
        Assert.Empty(await policies.ListAsync(Scope, CancellationToken.None));
    }

    [Fact]
    public async Task CommitAsync_NoRows_ThrowsValidation()
    {
        var (service, _, _) = Build();

        var ex = await Assert.ThrowsAsync<PolicyImportValidationException>(() =>
            service.CommitAsync(new PolicyImportCommitRequest(Headers, Mapping, [])));

        Assert.Equal("rows", ex.Field);
    }

    private static (IPolicyImportService Service, InMemoryPolicyRepository Policies, RecordingAuditWriter Audit) Build()
    {
        var context = new TenantContextAccessor();
        context.SetCurrent(new TenantContext(Scope.TenantId, Scope.WorkspaceId, "officer-1", [PlatformRole.ClaimsOfficer.ToString()]));
        var policies = new InMemoryPolicyRepository();
        var audit = new RecordingAuditWriter();
        var service = new PolicyImportService(context, new AllowAllPermissionService(), policies, audit);
        return (service, policies, audit);
    }

    private sealed class AllowAllPermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingAuditWriter : IAuditWriter
    {
        public List<AuditEvent> Written { get; } = [];

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Written.Add(auditEvent);
            return Task.CompletedTask;
        }
    }
}
