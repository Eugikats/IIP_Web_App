using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Customers;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.LocalTestDoubles;

namespace InsuranceIntelligence.Api.Tests;

public sealed class CustomerConsentServiceTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");

    [Fact]
    public async Task RecordExplicitOptInAsync_ThenWithdrawAsync_LatestRowIsAuthoritative()
    {
        var (service, customers, _, audit) = Build();
        var customer = new InsuranceIntelligence.Domain.Customers.Customer("cust-1", Scope.TenantId, Scope.WorkspaceId, "Sarah Nakato", "sarah@example.test", "+256772118001");
        await customers.UpsertAsync(customer);

        var granted = await service.RecordExplicitOptInAsync("cust-1", ConsentPurposes.WhatsApp);
        Assert.Equal("ExplicitOptIn", granted.Basis);
        Assert.True(granted.IsCurrentlyPermitted);
        Assert.Null(granted.WithdrawnAt);

        var withdrawn = await service.WithdrawAsync("cust-1", ConsentPurposes.WhatsApp);
        Assert.Equal("Withdrawn", withdrawn.Basis);
        Assert.False(withdrawn.IsCurrentlyPermitted);
        Assert.NotNull(withdrawn.WithdrawnAt);

        var history = await service.ListForCustomerAsync("cust-1");
        Assert.Equal(2, history.Count);
        var current = Assert.Single(history, record => record.RecordId == withdrawn.RecordId);
        Assert.False(current.IsCurrentlyPermitted);
        var superseded = Assert.Single(history, record => record.RecordId == granted.RecordId);
        Assert.False(superseded.IsCurrentlyPermitted); // the old grant is no longer current, even though its own row is a grant

        Assert.Contains(audit.Written, auditEvent => auditEvent.ActionName == "consent.granted");
        Assert.Contains(audit.Written, auditEvent => auditEvent.ActionName == "consent.withdrawn");
    }

    [Fact]
    public async Task RecordExplicitOptInAsync_ForEmailPurpose_RequiresAnEmailOnFile()
    {
        var (service, customers, _, _) = Build();
        await customers.UpsertAsync(new InsuranceIntelligence.Domain.Customers.Customer("cust-2", Scope.TenantId, Scope.WorkspaceId, "No Email Customer", null, "+256772118002"));

        var ex = await Assert.ThrowsAsync<CustomerConsentValidationException>(() =>
            service.RecordExplicitOptInAsync("cust-2", ConsentPurposes.Email));

        Assert.Equal("purpose", ex.Field);
    }

    [Fact]
    public async Task ListForCustomerAsync_UnknownCustomer_ThrowsValidation()
    {
        var (service, _, _, _) = Build();

        await Assert.ThrowsAsync<CustomerConsentValidationException>(() =>
            service.ListForCustomerAsync("does-not-exist"));
    }

    private static (ICustomerConsentService Service, InMemoryCustomerRepository Customers, InMemoryConsentRepository Consents, RecordingAuditWriter Audit) Build()
    {
        var context = new TenantContextAccessor();
        context.SetCurrent(new TenantContext(Scope.TenantId, Scope.WorkspaceId, "officer-1", [PlatformRole.ClaimsOfficer.ToString()]));
        var customers = new InMemoryCustomerRepository();
        var consents = new InMemoryConsentRepository();
        var audit = new RecordingAuditWriter();
        var service = new CustomerConsentService(
            context,
            new AllowAllPermissionService(),
            customers,
            consents,
            audit);
        return (service, customers, consents, audit);
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
