using System.Collections.Concurrent;
using InsuranceIntelligence.Domain.ClientPortal;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.LocalTestDoubles;

public sealed class InMemoryClaimantMagicLinkTokenRepository : IClaimantMagicLinkTokenRepository
{
    private readonly ConcurrentDictionary<Guid, ClaimantMagicLinkToken> tokens = new();

    public Task SaveAsync(ClaimantMagicLinkToken token, CancellationToken cancellationToken = default)
    {
        tokens[token.TokenId] = token;
        return Task.CompletedTask;
    }

    public Task<ClaimantMagicLinkToken?> GetByTokenHashAsync(string tokenHash, TenantScope scope, CancellationToken cancellationToken = default) =>
        Task.FromResult(tokens.Values.FirstOrDefault(token => token.TokenHash == tokenHash && token.Scope.Matches(scope)));
}

public sealed class InMemoryClaimantPortalSessionRepository : IClaimantPortalSessionRepository
{
    private readonly ConcurrentDictionary<Guid, ClaimantPortalSession> sessions = new();

    public Task SaveAsync(ClaimantPortalSession session, CancellationToken cancellationToken = default)
    {
        sessions[session.SessionId] = session;
        return Task.CompletedTask;
    }

    public Task<ClaimantPortalSession?> GetByTokenHashAsync(string tokenHash, TenantScope scope, CancellationToken cancellationToken = default) =>
        Task.FromResult(sessions.Values.FirstOrDefault(session => session.SessionTokenHash == tokenHash && session.Scope.Matches(scope)));
}
