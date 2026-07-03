using System.Text.Json;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Customers;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class CustomerQueryServiceTests
{
    [Fact]
    public async Task GetCustomerProfileAsync_RequiresCustomersReadPermission()
    {
        var service = new CustomerQueryService(
            CurrentContext("tenant-a", "workspace-a", "user-1"),
            new FixedPermissionService(),
            new RecordingCustomerReadRepository(Profile("customer-1")),
            new RecordingAuditWriter());

        await Assert.ThrowsAsync<PermissionDeniedException>(() => service.GetCustomerProfileAsync("customer-1"));
    }

    [Fact]
    public async Task GetCustomerProfileAsync_ReturnsOnlyTenantWorkspaceCustomer()
    {
        var repository = new RecordingCustomerReadRepository(Profile("customer-1"));
        var service = new CustomerQueryService(
            CurrentContext("tenant-a", "workspace-a", "user-1"),
            new FixedPermissionService(Permission.ViewCustomers),
            repository,
            new RecordingAuditWriter());

        var profile = await service.GetCustomerProfileAsync(" customer-1 ");

        Assert.NotNull(profile);
        Assert.Equal("tenant-a", repository.LastTenantId);
        Assert.Equal("workspace-a", repository.LastWorkspaceId);
        Assert.Equal("customer-1", repository.LastCustomerId);
        Assert.Equal("customer-1", profile.CustomerId);
    }

    [Fact]
    public async Task GetCustomerProfileAsync_WritesSensitiveReadAudit()
    {
        var auditWriter = new RecordingAuditWriter();
        var service = new CustomerQueryService(
            CurrentContext("tenant-a", "workspace-a", "user-1"),
            new FixedPermissionService(Permission.ViewCustomers),
            new RecordingCustomerReadRepository(Profile("customer-1")),
            auditWriter);

        await service.GetCustomerProfileAsync("customer-1");

        var auditEvent = Assert.Single(auditWriter.Events);
        Assert.Equal(TenantScope.Create("tenant-a", "workspace-a"), auditEvent.Scope);
        Assert.Equal("user-1", auditEvent.UserId);
        Assert.Equal("customer.profile.viewed", auditEvent.ActionName);
        Assert.Equal("CustomerQuery", auditEvent.ModelProvider);
        Assert.Contains("customer-1", auditEvent.EvidenceIds);
    }

    [Fact]
    public async Task GetCustomerProfileAsync_ReturnsNullWhenNotFoundOrNotVisible()
    {
        var auditWriter = new RecordingAuditWriter();
        var service = new CustomerQueryService(
            CurrentContext("tenant-a", "workspace-a", "user-1"),
            new FixedPermissionService(Permission.ViewCustomers),
            new RecordingCustomerReadRepository(null),
            auditWriter);

        var profile = await service.GetCustomerProfileAsync("customer-1");

        Assert.Null(profile);
        Assert.Empty(auditWriter.Events);
    }

    [Fact]
    public async Task GetCustomerProfileAsync_DoesNotExposeRawProviderPayloadsOrHiddenRiskNotes()
    {
        var service = new CustomerQueryService(
            CurrentContext("tenant-a", "workspace-a", "user-1"),
            new FixedPermissionService(Permission.ViewCustomers),
            new RecordingCustomerReadRepository(new CustomerProfileReadModel(
                "customer-1",
                new Dictionary<string, string>
                {
                    ["fullName"] = "Amina Okello",
                    ["rawProviderPayload"] = "{ secret: true }",
                    ["hiddenRiskNotes"] = "Internal risk note."
                },
                [
                    new Dictionary<string, string>
                    {
                        ["policyNumber"] = "POL-001",
                        ["provider_payload"] = "raw data"
                    }
                ],
                [],
                [],
                [],
                new Dictionary<string, string>(),
                [],
                [],
                [])),
            new RecordingAuditWriter());

        var profile = await service.GetCustomerProfileAsync("customer-1");
        Assert.NotNull(profile);
        var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("fullName", json, StringComparison.Ordinal);
        Assert.Contains("policyNumber", json, StringComparison.Ordinal);
        Assert.DoesNotContain("rawProviderPayload", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("provider_payload", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hiddenRiskNotes", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Internal risk note", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CustomerProfileDto_SerializesToExpectedCamelCaseContract()
    {
        var profile = Profile("customer-1");

        var json = JsonSerializer.Serialize(ToDto(profile), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"customerId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"identity\"", json, StringComparison.Ordinal);
        Assert.Contains("\"policies\"", json, StringComparison.Ordinal);
        Assert.Contains("\"claims\"", json, StringComparison.Ordinal);
        Assert.Contains("\"interactions\"", json, StringComparison.Ordinal);
        Assert.Contains("\"documents\"", json, StringComparison.Ordinal);
        Assert.Contains("\"consent\"", json, StringComparison.Ordinal);
        Assert.Contains("\"payments\"", json, StringComparison.Ordinal);
        Assert.Contains("\"opportunities\"", json, StringComparison.Ordinal);
        Assert.Contains("\"auditHistory\"", json, StringComparison.Ordinal);
        Assert.Contains("\"fullName\"", json, StringComparison.Ordinal);
        Assert.Contains("\"policyNumber\"", json, StringComparison.Ordinal);
    }

    private static CustomerProfileReadModel Profile(string customerId) =>
        new(
            customerId,
            new Dictionary<string, string>
            {
                ["fullName"] = "Amina Okello",
                ["email"] = "amina@example.invalid"
            },
            [
                new Dictionary<string, string>
                {
                    ["policyNumber"] = "POL-001",
                    ["productLine"] = "Motor"
                }
            ],
            [
                new Dictionary<string, string>
                {
                    ["claimId"] = "claim-1",
                    ["status"] = "Open"
                }
            ],
            [
                new Dictionary<string, string>
                {
                    ["channel"] = "email",
                    ["summary"] = "Customer requested claim status."
                }
            ],
            [],
            new Dictionary<string, string>
            {
                ["marketingConsent"] = "false"
            },
            [],
            [],
            [
                new AuditEventRowDto(
                    "customer.created",
                    ActorType.User.ToString(),
                    "user-1",
                    "customer",
                    customerId,
                    "2026-07-03T10:45:00.0000000+00:00",
                    "correlation-1")
            ]);

    private static CustomerProfileDto ToDto(CustomerProfileReadModel profile) =>
        new(
            profile.CustomerId,
            profile.Identity,
            profile.Policies,
            profile.Claims,
            profile.Interactions,
            profile.Documents,
            profile.Consent,
            profile.Payments,
            profile.Opportunities,
            profile.AuditHistory);

    private static TenantContextAccessor CurrentContext(string tenantId, string workspaceId, string userId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetCurrent(new TenantContext(tenantId, workspaceId, userId, []));
        return accessor;
    }

    private sealed class FixedPermissionService(params Permission[] permissions) : IPermissionService
    {
        private readonly HashSet<Permission> _permissions = permissions.ToHashSet();

        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) =>
            Task.FromResult(_permissions.Contains(permission));

        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default)
        {
            if (!_permissions.Contains(permission))
            {
                throw new PermissionDeniedException(permission);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCustomerReadRepository(CustomerProfileReadModel? profile) : ICustomerReadRepository
    {
        public string? LastTenantId { get; private set; }
        public string? LastWorkspaceId { get; private set; }
        public string? LastCustomerId { get; private set; }

        public Task<CustomerProfileReadModel?> GetCustomerProfileAsync(
            string tenantId,
            string workspaceId,
            string customerId,
            CancellationToken cancellationToken = default)
        {
            LastTenantId = tenantId;
            LastWorkspaceId = workspaceId;
            LastCustomerId = customerId;
            return Task.FromResult(profile);
        }
    }

    private sealed class RecordingAuditWriter : IAuditWriter
    {
        public List<AuditEvent> Events { get; } = [];

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }
}
