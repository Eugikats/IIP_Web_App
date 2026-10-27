using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.Workspaces;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.LocalTestDoubles;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Api.Tests;

public sealed class ClaimIdentityVerifierTests
{
    private static readonly TenantScope A = TenantScope.Create("insurer-a", "claims");
    private static readonly TenantScope B = TenantScope.Create("insurer-b", "claims");

    [Fact]
    public async Task Code_is_tenant_scoped_hashed_and_expires()
    {
        var clock = new Clock();
        var store = new Records();
        var sms = new Sms();
        var verifier = new ClaimIdentityVerifier(store, sms, new InMemoryAuditRepository(), clock);
        Assert.Equal(ClaimIdentityResult.CodeSent, await verifier.SendAsync(A, "256700000001", "corr"));
        var code = Regex.Match(sms.Body!, @"\b\d{6}\b").Value;
        Assert.Equal(6, code.Length);
        Assert.DoesNotContain(code, store.GetPayload(A)!, StringComparison.Ordinal);
        Assert.False(await verifier.IsVerifiedAsync(B, "256700000001"));
        Assert.Equal(ClaimIdentityResult.Expired, await verifier.VerifyAsync(B, "256700000001", code, "corr"));
        Assert.Equal(ClaimIdentityResult.InvalidCode, await verifier.VerifyAsync(A, "256700000001", "000000" == code ? "111111" : "000000", "corr"));
        Assert.Equal(ClaimIdentityResult.Verified, await verifier.VerifyAsync(A, "256700000001", code, "corr"));
        Assert.True(await verifier.IsVerifiedAsync(A, "256700000001"));
        clock.Advance(TimeSpan.FromMinutes(16));
        Assert.False(await verifier.IsVerifiedAsync(A, "256700000001"));
    }

    [Fact]
    public async Task Wrong_codes_lock_challenge_and_failed_sms_does_not_store_code()
    {
        var clock = new Clock();
        var store = new Records();
        var sms = new Sms();
        var verifier = new ClaimIdentityVerifier(store, sms, new InMemoryAuditRepository(), clock);
        Assert.Equal(ClaimIdentityResult.CodeSent, await verifier.SendAsync(A, "256700000001", "corr"));
        var code = Regex.Match(sms.Body!, @"\b\d{6}\b").Value;
        var wrong = code == "111111" ? "222222" : "111111";
        for (var attempt = 0; attempt < 4; attempt++)
            Assert.Equal(ClaimIdentityResult.InvalidCode, await verifier.VerifyAsync(A, "256700000001", wrong, "corr"));
        Assert.Equal(ClaimIdentityResult.Locked, await verifier.VerifyAsync(A, "256700000001", wrong, "corr"));
        Assert.Equal(ClaimIdentityResult.Locked, await verifier.VerifyAsync(A, "256700000001", code, "corr"));
        clock.Advance(TimeSpan.FromMinutes(11));
        sms.Status = "ConfigurationRequired";
        Assert.Equal(ClaimIdentityResult.NotConfigured, await verifier.SendAsync(A, "256700000001", "corr"));
        Assert.False(await verifier.IsVerifiedAsync(A, "256700000001"));
    }

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 10, 27, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan delta) => now += delta;
    }

    private sealed class Sms : ISmsProvider
    {
        public string? Body { get; private set; }
        public string Status { get; set; } = "Sent";
        public Task<CommunicationSendResult> SendAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default)
        {
            Assert.Equal(CommunicationChannel.SMS, request.Channel);
            Assert.True(request.LowRisk);
            Body = request.MessageBody;
            return Task.FromResult(new CommunicationSendResult(Status, "id", null));
        }
    }

    private sealed class Records : IWorkspaceRecordStore
    {
        private readonly ConcurrentDictionary<string, WorkspaceRecord> values = new();
        private static string Id(TenantScope scope, string kind, string key) => $"{scope.TenantId}:{scope.WorkspaceId}:{kind}:{key}";
        public string? GetPayload(TenantScope scope) => values.Values.FirstOrDefault(value => value.Key == "256700000001" && values.ContainsKey(Id(scope, value.Kind, value.Key)))?.PayloadJson;
        public Task<WorkspaceRecord?> GetAsync(TenantScope scope, string kind, string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(values.GetValueOrDefault(Id(scope, kind, key)));
        public Task SaveAsync(TenantScope scope, string kind, string key, string payloadJson, CancellationToken cancellationToken = default)
        {
            values[Id(scope, kind, key)] = new WorkspaceRecord(kind, key, payloadJson, DateTimeOffset.UtcNow);
            return Task.CompletedTask;
        }
        public Task<bool> TryReplaceAsync(TenantScope scope, string kind, string key, string expectedPayloadJson, string replacementPayloadJson, CancellationToken cancellationToken = default)
        {
            var id = Id(scope, kind, key);
            lock (values)
            {
                if (!values.TryGetValue(id, out var current) || current.PayloadJson != expectedPayloadJson) return Task.FromResult(false);
                values[id] = current with { PayloadJson = replacementPayloadJson };
                return Task.FromResult(true);
            }
        }
        public Task<IReadOnlyList<WorkspaceRecord>> ListAsync(TenantScope scope, string kind, int take = 500, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WorkspaceRecord>>([]);
        public Task DeleteAsync(TenantScope scope, string kind, string key, CancellationToken cancellationToken = default) { values.TryRemove(Id(scope, kind, key), out _); return Task.CompletedTask; }
        public Task<int> CountAsync(TenantScope scope, string kind, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<(TenantScope Scope, WorkspaceRecord Record)>> ListPlatformAsync(string kind, int take = 500, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<(TenantScope, WorkspaceRecord)>>([]);
    }
}
