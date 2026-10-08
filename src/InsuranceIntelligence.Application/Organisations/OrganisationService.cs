using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Application.Onboarding;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Organisations;

namespace InsuranceIntelligence.Application.Organisations;

/// <summary>Raised for a rule the caller broke; <see cref="Code"/> says which, for the API to map.</summary>
public sealed class OrganisationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;

    public static OrganisationException Invalid(string message) => new("invalid", message);
    public static OrganisationException NotFound(string message) => new("not_found", message);
    public static OrganisationException Forbidden(string message) => new("forbidden", message);
    public static OrganisationException Conflict(string message) => new("conflict", message);
}

public sealed record MyOrganisation(
    string OrganisationId, string Name, OrganisationType Type, string WorkspaceId, IReadOnlyList<string> Roles, string Access);

public sealed record InvitationCreated(Guid InvitationId, string Email, string RoleCode, DateTimeOffset ExpiresAt, string Token);

public sealed record InvitationView(
    Guid InvitationId, string Email, string RoleCode, InvitationStatus Status, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);

public sealed record LinkView(
    Guid LinkId, string OtherOrganisationId, string OtherOrganisationName, OrganisationType OtherType,
    BrokerLinkStatus Status, LinkInitiator InitiatedBy, bool AwaitingMyDecision, DateTimeOffset UpdatedAt);

public sealed record BrokerInsurerSummary(
    string InsurerOrganisationId, string InsurerName, int ClaimsSubmitted, int OpenClaims, IReadOnlyDictionary<string, int> ByStatus);

public sealed record BrokerDashboard(string BrokerOrganisationId, int InsurerCount, int ClaimsSubmitted, int OpenClaims, IReadOnlyList<BrokerInsurerSummary> Insurers);

/// <summary>
/// Sign-up, invitations and the broker/insurer relationship. The rules that matter live here:
/// a person only ever acts in an organisation they belong to; a broker reaches an insurer only
/// through a link both sides agreed to; a broker sees only the claims their own organisation
/// submitted, never the insurer's other work.
/// </summary>
public sealed class OrganisationService(
    IOrganisationRepository repository,
    ITenantMembershipWriter membershipWriter,
    IAuditRepository auditRepository,
    ICaseRepository caseRepository)
{
    public const int MaxOrganisationsCreatedPerUser = 5;
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    private static readonly PlatformRole[] InsurerRoles =
    [
        PlatformRole.Admin, PlatformRole.ClaimsOfficer, PlatformRole.ClaimsSupervisor, PlatformRole.CustomerService,
        PlatformRole.Compliance, PlatformRole.KnowledgeManager, PlatformRole.ITAdmin,
    ];

    private static readonly PlatformRole[] BrokerRoles = [PlatformRole.Admin, PlatformRole.Broker];

    // ---- Sign-up

    public async Task<Organisation> CreateAsync(
        string userId, string name, OrganisationType type, string correlationId, CancellationToken cancellationToken = default,
        OrganisationProduct product = OrganisationProduct.Insurance)
    {
        RequireUser(userId);
        var cleanName = string.Join(' ', (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (cleanName.Length is < 2 or > 100 || cleanName.Any(char.IsControl))
        {
            throw OrganisationException.Invalid("Give your organisation a name of 2 to 100 characters.");
        }

        if (await repository.CountCreatedByAsync(userId, cancellationToken) >= MaxOrganisationsCreatedPerUser)
        {
            throw OrganisationException.Conflict("This account has already created the most organisations it can. Contact support for more.");
        }

        var now = DateTimeOffset.UtcNow;
        var organisation = new Organisation(
            $"org-{Guid.NewGuid():N}"[..16], cleanName, type, Organisation.DefaultWorkspace,
            Organisation.ActiveStatus, Organisation.TrialPlan, userId, now, Product: product);
        await repository.AddAsync(organisation, cancellationToken);
        await membershipWriter.GrantMembershipAsync(
            organisation.OrganisationId, organisation.DefaultWorkspaceId, userId, nameof(PlatformRole.Admin), cancellationToken);
        await AuditAsync(organisation.Scope, userId, "organisation.created", [organisation.OrganisationId], $"{type} organisation created for IIP for {product}.", correlationId, cancellationToken);
        return organisation;
    }

    /// <summary>
    /// Makes a tenant that existed before organisations (the field-test workspace) into an organisation,
    /// without moving or copying any data: the organisation's id is the tenant id its records are already
    /// stored under. Safe to run again. Existing memberships keep working because they are already stored
    /// against that tenant.
    /// </summary>
    public async Task<Organisation> AdoptExistingTenantAsync(
        TenantScope scope, string userId, string name, OrganisationType type, OrganisationProduct product, string correlationId,
        CancellationToken cancellationToken = default)
    {
        RequireUser(userId);
        var existing = await repository.GetAsync(scope.TenantId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var cleanName = string.Join(' ', (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (cleanName.Length is < 2 or > 100 || cleanName.Any(char.IsControl))
        {
            throw OrganisationException.Invalid("Give the organisation a name of 2 to 100 characters.");
        }

        var organisation = new Organisation(
            scope.TenantId, cleanName, type, scope.WorkspaceId, Organisation.ActiveStatus, Organisation.TrialPlan, userId,
            DateTimeOffset.UtcNow, Product: product);
        await repository.AddAsync(organisation, cancellationToken);
        await AuditAsync(scope, userId, "organisation.adopted_existing_tenant", [organisation.OrganisationId],
            $"Existing tenant registered as the organisation '{cleanName}'. No data was moved.", correlationId, cancellationToken);
        return organisation;
    }

    public async Task<IReadOnlyList<MyOrganisation>> ListMineAsync(string userId, CancellationToken cancellationToken = default)
    {
        RequireUser(userId);
        var memberships = await repository.ListMembershipsForUserAsync(userId, cancellationToken);
        var mine = new List<MyOrganisation>();
        var direct = await repository.GetManyAsync(memberships.Select(m => m.OrganisationId), cancellationToken);
        foreach (var organisation in direct.Where(o => o.IsActive))
        {
            var membership = memberships.First(m => m.OrganisationId == organisation.OrganisationId);
            mine.Add(new MyOrganisation(organisation.OrganisationId, organisation.Name, organisation.Type, membership.WorkspaceId, membership.RoleCodes, "Member"));
        }

        // Insurers a broker the user belongs to works with: reachable, as a broker, while the link stands.
        foreach (var broker in direct.Where(o => o.IsActive && o.Type == OrganisationType.Broker))
        {
            var links = (await repository.ListLinksForOrganisationAsync(broker.OrganisationId, cancellationToken))
                .Where(l => l.Status == BrokerLinkStatus.Active && l.BrokerOrganisationId == broker.OrganisationId)
                .ToArray();
            var insurers = await repository.GetManyAsync(links.Select(l => l.InsurerOrganisationId), cancellationToken);
            foreach (var insurer in insurers.Where(i => i.IsActive && i.Type == OrganisationType.Insurer))
            {
                if (mine.Any(m => m.OrganisationId == insurer.OrganisationId))
                {
                    continue;
                }

                mine.Add(new MyOrganisation(insurer.OrganisationId, insurer.Name, insurer.Type, insurer.DefaultWorkspaceId, [nameof(PlatformRole.Broker)], $"Broker via {broker.Name}"));
            }
        }

        return mine.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    // ---- Invitations

    public async Task<InvitationCreated> InviteAsync(
        string organisationId, string actorUserId, string email, string roleCode, string correlationId, CancellationToken cancellationToken = default)
    {
        var organisation = await RequireOrganisationAsync(organisationId, cancellationToken);
        var normalizedEmail = NormalizeEmail(email);
        var allowed = organisation.Type == OrganisationType.Insurer ? InsurerRoles : BrokerRoles;
        if (!Enum.TryParse<PlatformRole>(roleCode, ignoreCase: true, out var role) || !allowed.Contains(role))
        {
            throw OrganisationException.Invalid($"That role cannot be given in a {organisation.Type.ToString().ToLowerInvariant()} organisation.");
        }

        var existing = await repository.ListInvitationsAsync(organisationId, cancellationToken);
        if (existing.Count(i => i.Status == InvitationStatus.Pending && i.ExpiresAt > DateTimeOffset.UtcNow) >= 200)
        {
            throw OrganisationException.Conflict("Too many invitations are waiting. Revoke some before sending more.");
        }

        // A fresh invitation to the same person replaces the old one, so only one secret is live.
        foreach (var old in existing.Where(i => i.Status == InvitationStatus.Pending && i.Email == normalizedEmail))
        {
            await repository.UpdateInvitationAsync(old with { Status = InvitationStatus.Revoked }, cancellationToken);
        }

        var token = NewToken();
        var now = DateTimeOffset.UtcNow;
        var invitation = new OrganisationInvitation(
            Guid.NewGuid(), organisationId, normalizedEmail, role.ToString(), Hash(token), InvitationStatus.Pending,
            actorUserId, now, now + InvitationLifetime);
        await repository.AddInvitationAsync(invitation, cancellationToken);
        await AuditAsync(organisation.Scope, actorUserId, "organisation.invitation_created", [invitation.InvitationId.ToString()], $"Invited a person as {role}.", correlationId, cancellationToken);
        return new InvitationCreated(invitation.InvitationId, normalizedEmail, role.ToString(), invitation.ExpiresAt, token);
    }

    public async Task<Organisation> AcceptInvitationAsync(
        string userId, string? userEmail, string token, string correlationId, CancellationToken cancellationToken = default)
    {
        RequireUser(userId);
        var invitation = string.IsNullOrWhiteSpace(token) ? null : await repository.GetInvitationByTokenHashAsync(Hash(token.Trim()), cancellationToken);
        // The same answer for an unknown, used, revoked or expired secret: nothing to learn by guessing.
        if (invitation is null || invitation.Status != InvitationStatus.Pending || invitation.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw OrganisationException.NotFound("This invitation is not valid. Ask for a new one.");
        }

        if (string.IsNullOrWhiteSpace(userEmail))
        {
            throw OrganisationException.Forbidden("Your sign-in does not include an email address, so the invitation cannot be matched to you.");
        }

        if (!string.Equals(NormalizeEmail(userEmail), invitation.Email, StringComparison.Ordinal))
        {
            throw OrganisationException.Forbidden("This invitation was sent to a different email address.");
        }

        var organisation = await RequireOrganisationAsync(invitation.OrganisationId, cancellationToken);
        await membershipWriter.GrantMembershipAsync(
            organisation.OrganisationId, organisation.DefaultWorkspaceId, userId, invitation.RoleCode, cancellationToken);
        await repository.UpdateInvitationAsync(
            invitation with { Status = InvitationStatus.Accepted, AcceptedByUserId = userId, AcceptedAt = DateTimeOffset.UtcNow }, cancellationToken);
        await AuditAsync(organisation.Scope, userId, "organisation.invitation_accepted", [invitation.InvitationId.ToString()], $"Joined as {invitation.RoleCode}.", correlationId, cancellationToken);
        return organisation;
    }

    public async Task RevokeInvitationAsync(
        string organisationId, string actorUserId, Guid invitationId, string correlationId, CancellationToken cancellationToken = default)
    {
        var organisation = await RequireOrganisationAsync(organisationId, cancellationToken);
        var invitation = await repository.GetInvitationAsync(invitationId, cancellationToken);
        if (invitation is null || invitation.OrganisationId != organisationId)
        {
            throw OrganisationException.NotFound("No such invitation.");
        }

        if (invitation.Status == InvitationStatus.Pending)
        {
            await repository.UpdateInvitationAsync(invitation with { Status = InvitationStatus.Revoked }, cancellationToken);
            await AuditAsync(organisation.Scope, actorUserId, "organisation.invitation_revoked", [invitationId.ToString()], "Invitation revoked.", correlationId, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<InvitationView>> ListInvitationsAsync(string organisationId, CancellationToken cancellationToken = default) =>
        (await repository.ListInvitationsAsync(organisationId, cancellationToken))
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvitationView(i.InvitationId, i.Email, i.RoleCode, i.Status, i.CreatedAt, i.ExpiresAt))
            .ToArray();

    // ---- Broker and insurer links

    /// <summary>Either side can ask. The other side decides.</summary>
    public async Task<BrokerLink> RequestLinkAsync(
        string actorOrganisationId, string actorUserId, string otherOrganisationId, string correlationId, CancellationToken cancellationToken = default)
    {
        var actor = await RequireOrganisationAsync(actorOrganisationId, cancellationToken);
        var other = await repository.GetAsync(otherOrganisationId?.Trim() ?? string.Empty, cancellationToken);
        if (other is null || !other.IsActive)
        {
            throw OrganisationException.NotFound("No organisation with that id was found.");
        }

        if (actor.Type == other.Type)
        {
            throw OrganisationException.Invalid("A link joins one broker and one insurer.");
        }

        var broker = actor.Type == OrganisationType.Broker ? actor : other;
        var insurer = actor.Type == OrganisationType.Insurer ? actor : other;
        var current = (await repository.ListLinksForOrganisationAsync(actorOrganisationId, cancellationToken))
            .Where(l => l.BrokerOrganisationId == broker.OrganisationId && l.InsurerOrganisationId == insurer.OrganisationId)
            .FirstOrDefault(l => l.Status is BrokerLinkStatus.Requested or BrokerLinkStatus.Active);
        if (current is not null)
        {
            throw OrganisationException.Conflict(current.Status == BrokerLinkStatus.Active ? "These organisations are already linked." : "A request between these organisations is already waiting.");
        }

        var now = DateTimeOffset.UtcNow;
        var link = new BrokerLink(
            Guid.NewGuid(), broker.OrganisationId, insurer.OrganisationId, BrokerLinkStatus.Requested,
            actor.Type == OrganisationType.Broker ? LinkInitiator.Broker : LinkInitiator.Insurer, actorUserId, null, now, now);
        await repository.AddLinkAsync(link, cancellationToken);
        await AuditBothAsync(link, actorUserId, "organisation.broker_link_requested", correlationId, cancellationToken);
        return link;
    }

    /// <summary>The side that did not ask approves or declines.</summary>
    public async Task<BrokerLink> DecideLinkAsync(
        string actorOrganisationId, string actorUserId, Guid linkId, bool approve, string correlationId, CancellationToken cancellationToken = default)
    {
        var link = await RequireLinkForAsync(actorOrganisationId, linkId, cancellationToken);
        var actorIsBroker = link.BrokerOrganisationId == actorOrganisationId;
        var actorAsked = (link.InitiatedBy == LinkInitiator.Broker) == actorIsBroker;
        if (actorAsked)
        {
            throw OrganisationException.Forbidden("The other organisation has to answer a request you made.");
        }

        if (link.Status != BrokerLinkStatus.Requested)
        {
            throw OrganisationException.Conflict("This request has already been answered.");
        }

        var decided = link with
        {
            Status = approve ? BrokerLinkStatus.Active : BrokerLinkStatus.Declined,
            DecidedByUserId = actorUserId,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await repository.UpdateLinkAsync(decided, cancellationToken);
        await AuditBothAsync(decided, actorUserId, approve ? "organisation.broker_link_approved" : "organisation.broker_link_declined", correlationId, cancellationToken);
        return decided;
    }

    /// <summary>Either side can end it, at any time. Access ends with the next request.</summary>
    public async Task<BrokerLink> RevokeLinkAsync(
        string actorOrganisationId, string actorUserId, Guid linkId, string correlationId, CancellationToken cancellationToken = default)
    {
        var link = await RequireLinkForAsync(actorOrganisationId, linkId, cancellationToken);
        if (link.Status is BrokerLinkStatus.Revoked or BrokerLinkStatus.Declined)
        {
            return link;
        }

        var revoked = link with { Status = BrokerLinkStatus.Revoked, DecidedByUserId = actorUserId, UpdatedAt = DateTimeOffset.UtcNow };
        await repository.UpdateLinkAsync(revoked, cancellationToken);
        await AuditBothAsync(revoked, actorUserId, "organisation.broker_link_revoked", correlationId, cancellationToken);
        return revoked;
    }

    public async Task<IReadOnlyList<LinkView>> ListLinksAsync(string organisationId, CancellationToken cancellationToken = default)
    {
        var links = await repository.ListLinksForOrganisationAsync(organisationId, cancellationToken);
        var others = (await repository.GetManyAsync(
            links.Select(l => l.BrokerOrganisationId == organisationId ? l.InsurerOrganisationId : l.BrokerOrganisationId), cancellationToken))
            .ToDictionary(o => o.OrganisationId);
        var views = new List<LinkView>();
        foreach (var link in links.OrderByDescending(l => l.UpdatedAt))
        {
            var iAmBroker = link.BrokerOrganisationId == organisationId;
            var otherId = iAmBroker ? link.InsurerOrganisationId : link.BrokerOrganisationId;
            if (!others.TryGetValue(otherId, out var other))
            {
                continue;
            }

            var iAsked = (link.InitiatedBy == LinkInitiator.Broker) == iAmBroker;
            views.Add(new LinkView(
                link.LinkId, other.OrganisationId, other.Name, other.Type, link.Status, link.InitiatedBy,
                AwaitingMyDecision: link.Status == BrokerLinkStatus.Requested && !iAsked, link.UpdatedAt));
        }

        return views;
    }

    // ---- Broker dashboard: one view across every insurer, each read in that insurer's own scope

    public async Task<BrokerDashboard> BrokerDashboardAsync(string brokerOrganisationId, CancellationToken cancellationToken = default)
    {
        var broker = await RequireOrganisationAsync(brokerOrganisationId, cancellationToken);
        if (broker.Type != OrganisationType.Broker)
        {
            throw OrganisationException.Invalid("Only a broker organisation has a broker dashboard.");
        }

        var members = (await repository.ListMemberUserIdsAsync(brokerOrganisationId, cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var links = (await repository.ListLinksForOrganisationAsync(brokerOrganisationId, cancellationToken))
            .Where(l => l.Status == BrokerLinkStatus.Active && l.BrokerOrganisationId == brokerOrganisationId)
            .ToArray();
        var insurers = (await repository.GetManyAsync(links.Select(l => l.InsurerOrganisationId), cancellationToken))
            .Where(i => i.IsActive && i.Type == OrganisationType.Insurer)
            .ToArray();

        var summaries = new List<BrokerInsurerSummary>();
        foreach (var insurer in insurers.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
        {
            // Read in the insurer's own scope, and keep only what this broker's people submitted.
            var cases = (await caseRepository.ListAsync(insurer.Scope, cancellationToken))
                .Where(c => c.Scope.Matches(insurer.Scope) && members.Contains(c.CreatedByUserId))
                .ToArray();
            var byStatus = cases.GroupBy(c => c.Status.ToString()).ToDictionary(g => g.Key, g => g.Count());
            var open = cases.Count(c => c.Status is not (CaseStatus.Settled or CaseStatus.Closed));
            summaries.Add(new BrokerInsurerSummary(insurer.OrganisationId, insurer.Name, cases.Length, open, byStatus));
        }

        return new BrokerDashboard(
            brokerOrganisationId, summaries.Count, summaries.Sum(s => s.ClaimsSubmitted), summaries.Sum(s => s.OpenClaims), summaries);
    }

    /// <summary>Ends the organisation: marks it deleted, ends every membership and link, and records
    /// who did it. The caller removes the organisation's data separately; this is the part that
    /// makes sure nobody can get back in while that happens.</summary>
    public async Task<Organisation> CloseAsync(
        string organisationId, string actorUserId, string confirmName, string correlationId, CancellationToken cancellationToken = default)
    {
        var organisation = await RequireOrganisationAsync(organisationId, cancellationToken);
        if (!string.Equals(confirmName?.Trim(), organisation.Name, StringComparison.Ordinal))
        {
            throw OrganisationException.Invalid("Type the organisation's exact name to confirm.");
        }

        foreach (var link in await repository.ListLinksForOrganisationAsync(organisationId, cancellationToken))
        {
            if (link.Status is BrokerLinkStatus.Active or BrokerLinkStatus.Requested)
            {
                await repository.UpdateLinkAsync(link with { Status = BrokerLinkStatus.Revoked, DecidedByUserId = actorUserId, UpdatedAt = DateTimeOffset.UtcNow }, cancellationToken);
            }
        }

        var closed = organisation with { Status = "Closed" };
        await repository.UpdateOrganisationAsync(closed, cancellationToken);
        await AuditAsync(TenantScope.Create("iip-platform", "organisations"), actorUserId, "organisation.closed", [organisationId], "Organisation closed at its own request.", correlationId, cancellationToken);
        return closed;
    }

    // ---- helpers

    private async Task<Organisation> RequireOrganisationAsync(string organisationId, CancellationToken cancellationToken) =>
        await repository.GetAsync(organisationId, cancellationToken) is { IsActive: true } organisation
            ? organisation
            : throw OrganisationException.NotFound("No such organisation.");

    private async Task<BrokerLink> RequireLinkForAsync(string actorOrganisationId, Guid linkId, CancellationToken cancellationToken)
    {
        var link = await repository.GetLinkAsync(linkId, cancellationToken);
        // Not found, rather than forbidden, for a link between two other organisations.
        if (link is null || (link.BrokerOrganisationId != actorOrganisationId && link.InsurerOrganisationId != actorOrganisationId))
        {
            throw OrganisationException.NotFound("No such request.");
        }

        return link;
    }

    private static void RequireUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw OrganisationException.Forbidden("Sign in to continue.");
        }
    }

    private static string NormalizeEmail(string email)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length > 254 || normalized.Count(c => c == '@') != 1 || normalized.StartsWith('@') || normalized.EndsWith('@') || normalized.Any(char.IsWhiteSpace))
        {
            throw OrganisationException.Invalid("Enter a valid email address.");
        }

        return normalized;
    }

    private static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private Task AuditAsync(TenantScope scope, string userId, string action, IReadOnlyCollection<string> evidence, string reason, string correlationId, CancellationToken cancellationToken) =>
        auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), scope, null, null, userId, ActorType.User, action, DateTimeOffset.UtcNow,
                null, null, evidence, "Platform", null, "recorded", null, reason, correlationId),
            cancellationToken);

    private async Task AuditBothAsync(BrokerLink link, string userId, string action, string correlationId, CancellationToken cancellationToken)
    {
        var evidence = new[] { link.LinkId.ToString() };
        var reason = $"Broker {link.BrokerOrganisationId} and insurer {link.InsurerOrganisationId}: {link.Status}.";
        foreach (var id in new[] { link.BrokerOrganisationId, link.InsurerOrganisationId })
        {
            await AuditAsync(TenantScope.Create(id, Organisation.DefaultWorkspace), userId, action, evidence, reason, correlationId, cancellationToken);
        }
    }
}
