using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Identity;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Identity;

public sealed class PersistedLocalCredential : PersistedScopedJson;
public sealed class PersistedSignupRequest : PersistedScopedJson;
public sealed class PersistedPasswordReset : PersistedScopedJson;
public sealed class PersistedStaffInvitation : PersistedScopedJson;

/// <summary>
/// Sign-in, sign-up, password-reset and invitation records (build-loop item 30). Credentials, sign-up requests and resets are
/// platform-level and found by a key derived from the email or the link's secret, so there is no search across tenants. Invitations
/// belong to the workspace that sent them and are listed and revoked only inside it; the accept link finds one by its secret's key.
/// </summary>
public sealed class EfLocalIdentityStore(InsuranceIntelligenceDbContext dbContext) : JsonRepositoryBase(dbContext), ILocalIdentityStore
{
    private static readonly TenantScope Platform = TenantScope.Create("iip-platform", "identity");

    public async Task<LocalCredential?> GetCredentialAsync(string normalisedEmail, CancellationToken cancellationToken = default)
    {
        var record = await DbContext.LocalCredentials.AsNoTracking().FirstOrDefaultAsync(item => item.Id == LocalCredential.StorageKey(normalisedEmail), cancellationToken);
        return record is null ? null : Deserialize<LocalCredential>(record.PayloadJson);
    }

    public Task SaveCredentialAsync(LocalCredential credential, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.LocalCredentials, LocalCredential.StorageKey(credential.Email), Platform, Serialize(credential), cancellationToken: cancellationToken);

    public async Task<SignupRequest?> GetSignupAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var record = await DbContext.SignupRequests.AsNoTracking().FirstOrDefaultAsync(item => item.Id == IdentityTokens.KeyFor("signup", tokenHash), cancellationToken);
        return record is null ? null : Deserialize<SignupRequest>(record.PayloadJson);
    }

    public Task SaveSignupAsync(SignupRequest request, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.SignupRequests, IdentityTokens.KeyFor("signup", request.TokenHash), Platform, Serialize(request), cancellationToken: cancellationToken);

    public async Task<PasswordReset?> GetResetAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var record = await DbContext.PasswordResets.AsNoTracking().FirstOrDefaultAsync(item => item.Id == IdentityTokens.KeyFor("reset", tokenHash), cancellationToken);
        return record is null ? null : Deserialize<PasswordReset>(record.PayloadJson);
    }

    public Task SaveResetAsync(PasswordReset reset, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.PasswordResets, IdentityTokens.KeyFor("reset", reset.TokenHash), Platform, Serialize(reset), cancellationToken: cancellationToken);

    public async Task<StaffInvitation?> GetInvitationByTokenAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var record = await DbContext.StaffInvitations.AsNoTracking().FirstOrDefaultAsync(item => item.Id == IdentityTokens.KeyFor("invitation", tokenHash), cancellationToken);
        return record is null ? null : Deserialize<StaffInvitation>(record.PayloadJson);
    }

    public async Task<StaffInvitation?> GetInvitationAsync(TenantScope scope, Guid invitationId, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.StaffInvitations.AsNoTracking(), scope).FirstOrDefaultAsync(item => item.Id == invitationId, cancellationToken);
        return record is null ? null : Deserialize<StaffInvitation>(record.PayloadJson);
    }

    public async Task<IReadOnlyList<StaffInvitation>> ListInvitationsAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        (await Scoped(DbContext.StaffInvitations.AsNoTracking(), scope).ToListAsync(cancellationToken))
            .Select(record => Deserialize<StaffInvitation>(record.PayloadJson))
            .OrderByDescending(item => item.CreatedAt)
            .ToArray();

    public Task SaveInvitationAsync(StaffInvitation invitation, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.StaffInvitations, invitation.InvitationId, invitation.Scope, Serialize(invitation), cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<TenantScope>> ListMembershipsAsync(string userId, CancellationToken cancellationToken = default) =>
        (await DbContext.UserTenantMemberships.AsNoTracking().Where(item => item.UserId == userId && item.IsActive).ToListAsync(cancellationToken))
            .Select(item => TenantScope.Create(item.TenantId, item.WorkspaceId))
            .ToArray();
}
