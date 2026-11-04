using InsuranceIntelligence.Application.ClientPortal;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Onboarding;
using InsuranceIntelligence.Application.Products;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.ClientPortal;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Identity;

namespace InsuranceIntelligence.Application.Identity;

public sealed record SignUpCommand(string? CompanyName, string? AdminName, string? Email, string? Password, bool AcceptTerms, string? Website);

public sealed record SignInCommand(string? Email, string? Password, string? WorkspaceId, string? TenantId = null);

public sealed record WorkspaceChoice(string TenantId, string WorkspaceId, string Name);

public sealed record SignedInUser(string UserId, string Name, string Email, string TenantId, string WorkspaceId, string Role);

/// <summary>Outcome is one of SignedIn, InvalidCredentials, ChooseWorkspace, NoWorkspace or NotConfigured.</summary>
public sealed record SignInResult(string Outcome, string? AccessToken, DateTimeOffset? ExpiresAt, SignedInUser? User, IReadOnlyList<WorkspaceChoice>? Workspaces);

public sealed record Validated<T>(T? Value, IReadOnlyDictionary<string, string>? Problems)
{
    public bool IsValid => Problems is null;
}

public sealed record InvitationView(Guid InvitationId, string Email, string Role, string Status, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, string InvitedBy);

public sealed record InvitationPreview(string WorkspaceName, string Email, string Role, bool HasAccount);

/// <summary>Signs and checks the platform's own access tokens (for companies without Microsoft 365). Not configured means no key is set.</summary>
public interface ILocalTokenIssuer
{
    bool IsConfigured { get; }

    (string Token, DateTimeOffset ExpiresAt) Issue(string userId, string email, string name, TenantScope scope, string role);
}

/// <summary>Sends the platform's own emails (verification, invitation, reset). Returns false when no email sender is configured, so the
/// caller can say so instead of pretending a message went out.</summary>
public interface IIdentityMailer
{
    Task<bool> SendAsync(TenantScope scope, string recipient, string subject, string body, string correlationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Self-serve company sign-up and the platform's own sign-in (build-loop item 30), for insurers without Microsoft 365 and alongside
/// Microsoft Entra for those that have it. A company asks to join, proves its administrator's email address, and gets a workspace with
/// its administrator, its customer portal name and starter products with no platform operator involved. People are added to a
/// workspace by invitation. Every email link carries a secret of which only a fingerprint is kept; a wrong email or password, and an
/// account that is locked after too many tries, look the same to the person typing.
/// </summary>
public sealed class LocalIdentityService(
    ILocalIdentityStore store,
    IUserAccountRepository userAccounts,
    ITenantMembershipWriter membershipWriter,
    ITenantSettingsRepository settingsRepository,
    IPortalIdentityRepository portalIdentities,
    ILocalTokenIssuer tokenIssuer,
    IIdentityMailer mailer,
    IPortalAddress portalAddress,
    IAuditRepository auditRepository,
    ITenantContextSetter tenantContextSetter,
    IProductCommandService productCommands,
    TimeProvider? clock = null,
    InsuranceIntelligence.Application.Billing.PlanService? plans = null)
{
    public static readonly TimeSpan SignupLifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan ResetLifetime = TimeSpan.FromHours(1);
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    public const int MaxFailedAttempts = 5;
    public const string StarterActor = "system:signup";
    private const string DefaultWorkspaceId = "workspace-claims";
    private static readonly TenantScope Platform = TenantScope.Create("iip-platform", "identity");

    private readonly TimeProvider time = clock ?? TimeProvider.System;

    private DateTimeOffset Now => time.GetUtcNow();

    // ---- sign up ----

    public Validated<SignUpCommand> ValidateSignUp(SignUpCommand command)
    {
        var problems = new Dictionary<string, string>();
        var company = command.CompanyName?.Trim() ?? string.Empty;
        if (company.Length is < 2 or > 120) problems["companyName"] = "Enter your company name, up to 120 characters.";
        var name = command.AdminName?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 80) problems["adminName"] = "Enter your name, up to 80 characters.";
        var email = command.Email?.Trim() ?? string.Empty;
        if (!IsEmail(email)) problems["email"] = "Enter a valid email address.";
        else if (PasswordPolicy.Check(command.Password, email, company) is { } passwordProblem) problems["password"] = passwordProblem;
        if (!command.AcceptTerms) problems["acceptTerms"] = "Please accept the terms of use to continue.";
        return problems.Count == 0 ? new Validated<SignUpCommand>(command, null) : new Validated<SignUpCommand>(null, problems);
    }

    /// <summary>Starts a sign-up. Always answers the same, whether or not the email already has an account. Returns false when the
    /// platform cannot send email, because then the person could never finish.</summary>
    public async Task<bool> SignUpAsync(SignUpCommand command, string correlationId, CancellationToken cancellationToken = default)
    {
        // A field no person sees; software that fills in every field gets a polite pretend success and nothing is sent.
        if (!string.IsNullOrWhiteSpace(command.Website))
        {
            return true;
        }

        var email = LocalCredential.NormaliseEmail(command.Email!);
        var baseUrl = portalAddress.BaseUrlFor(Platform);
        if (baseUrl is null)
        {
            return false;
        }

        if (await store.GetCredentialAsync(email, cancellationToken) is not null)
        {
            return await mailer.SendAsync(Platform, email, "You already have an IIP account",
                $"Someone used this email address to start a new IIP workspace, but it already has an account.\n\nSign in: {baseUrl}/signin\nForgotten your password: {baseUrl}/forgot-password\n\nIf this was not you, ignore this email.",
                correlationId, cancellationToken);
        }

        var secret = IdentityTokens.NewSecret();
        var (hash, salt, iterations) = PasswordHasher.Hash(command.Password!);
        var now = Now;
        var sent = await mailer.SendAsync(Platform, email, "Confirm your email to open your IIP workspace",
            $"Confirm that this is your email address to open the workspace for {command.CompanyName!.Trim()}.\n\n{baseUrl}/signup/verify?token={Uri.EscapeDataString(secret)}\n\nThe link works once and expires in 24 hours. If you did not ask for this, ignore this email and nothing will be created.",
            correlationId, cancellationToken);
        if (!sent)
        {
            return false;
        }

        await store.SaveSignupAsync(new SignupRequest
        {
            TokenHash = IdentityTokens.Hash(secret),
            CompanyName = command.CompanyName!.Trim(),
            AdminName = command.AdminName!.Trim(),
            Email = email,
            PasswordHash = hash,
            PasswordSalt = salt,
            Iterations = iterations,
            CreatedAt = now,
            ExpiresAt = now + SignupLifetime,
        }, cancellationToken);
        await auditRepository.WriteAsync(Audit(Platform, "system:signup", "signup.requested", "A company asked to join; email not yet confirmed.", correlationId), cancellationToken);
        return true;
    }

    /// <summary>Confirms the email address and creates the workspace. Null when the link is wrong, used or expired.</summary>
    public async Task<SignedInUser?> VerifySignUpAsync(string? secret, string correlationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return null;
        }

        var request = await store.GetSignupAsync(IdentityTokens.Hash(secret), cancellationToken);
        if (request is null || request.ConsumedAt is not null || request.ExpiresAt <= Now)
        {
            return null;
        }

        if (await store.GetCredentialAsync(request.Email, cancellationToken) is not null)
        {
            return null;
        }

        var now = Now;
        var tenantId = $"tenant-{Guid.NewGuid():N}"[..17];
        var scope = TenantScope.Create(tenantId, DefaultWorkspaceId);
        var userId = Guid.NewGuid().ToString("N");

        await store.SaveCredentialAsync(new LocalCredential
        {
            UserId = userId,
            Email = request.Email,
            PasswordHash = request.PasswordHash,
            PasswordSalt = request.PasswordSalt,
            Iterations = request.Iterations,
            CreatedAt = now,
            PasswordChangedAt = now,
        }, cancellationToken);
        await userAccounts.SaveAsync(new InsuranceUserAccount { UserId = userId, Scope = scope, Name = request.AdminName, Email = request.Email, Role = PlatformRole.Admin }, cancellationToken);
        await membershipWriter.GrantMembershipAsync(tenantId, DefaultWorkspaceId, userId, nameof(PlatformRole.Admin), cancellationToken);
        await settingsRepository.SaveAsync(new TenantSettings
        {
            Scope = scope,
            AssistantOrganisation = request.CompanyName,
            UpdatedByUserId = StarterActor,
            UpdatedAt = now,
        }, cancellationToken);
        await portalIdentities.SaveAsync(new PortalIdentity
        {
            Scope = scope,
            Slug = await FreeSlugAsync(request.CompanyName, cancellationToken),
            DisplayName = request.CompanyName.Length > 80 ? request.CompanyName[..80] : request.CompanyName,
            UpdatedByUserId = StarterActor,
            UpdatedAt = now,
        }, cancellationToken);
        await CreateStarterProductsAsync(scope, userId, cancellationToken);
        if (plans is not null)
        {
            // A company that signs up for itself starts on the Free plan, and its administrator receives the receipts.
            await plans.StartFreeAsync(scope, request.Email, cancellationToken);
        }

        await store.SaveSignupAsync(request with { ConsumedAt = now }, cancellationToken);
        await auditRepository.WriteAsync(Audit(scope, userId, "workspace.created", $"Workspace opened for {request.CompanyName} through self-serve sign-up.", correlationId), cancellationToken);
        return new SignedInUser(userId, request.AdminName, request.Email, tenantId, DefaultWorkspaceId, nameof(PlatformRole.Admin));
    }

    private async Task<string> FreeSlugAsync(string company, CancellationToken cancellationToken)
    {
        var words = new string(company.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray());
        var parts = words.Split('-', StringSplitOptions.RemoveEmptyEntries);
        var baseSlug = string.Join('-', parts);
        if (baseSlug.Length == 0 || !char.IsAsciiLetterLower(baseSlug[0])) baseSlug = "co-" + baseSlug;
        baseSlug = baseSlug.Length > 30 ? baseSlug[..30].TrimEnd('-') : baseSlug;
        var taken = (await portalIdentities.ListAllAsync(cancellationToken)).Select(item => item.Slug).ToHashSet(StringComparer.Ordinal);
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var candidate = attempt == 0 ? baseSlug : $"{baseSlug}-{Guid.NewGuid():N}"[..Math.Min(40, baseSlug.Length + 5)];
            if (PortalIdentity.ValidateSlug(candidate) is null && !taken.Contains(candidate))
            {
                return candidate;
            }
        }

        return $"co-{Guid.NewGuid():N}"[..16];
    }

    /// <summary>A new workspace starts with three common lines of cover as drafts, using the built-in stages, questions and document rules
    /// for each. They are not published: an insurer has to add its approval reference and have a second person approve them first.</summary>
    private async Task CreateStarterProductsAsync(TenantScope scope, string adminUserId, CancellationToken cancellationToken)
    {
        try
        {
            tenantContextSetter.SetCurrent(new TenantContext(scope.TenantId, scope.WorkspaceId, adminUserId, [nameof(PlatformRole.Admin)]));
            foreach (var (name, productClass) in new[] { ("Motor comprehensive", "Motor"), ("Medical inpatient", "Medical"), ("Home and property", "Property") })
            {
                await productCommands.CreateDraftAsync(new CreateProductRequest(name, productClass, "Starter product created with the workspace.", UseDefaultStageLibrary: true), cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The workspace is useful without them; the administrator can add products themselves.
            await auditRepository.WriteAsync(Audit(scope, StarterActor, "workspace.starter_products_failed", $"Starter products could not be created ({exception.GetType().Name}).", "signup"), cancellationToken);
        }
    }

    // ---- sign in ----

    public async Task<SignInResult> SignInAsync(SignInCommand command, string correlationId, CancellationToken cancellationToken = default)
    {
        if (!tokenIssuer.IsConfigured)
        {
            return new SignInResult("NotConfigured", null, null, null, null);
        }

        var email = LocalCredential.NormaliseEmail(command.Email ?? string.Empty);
        var password = command.Password ?? string.Empty;
        var credential = email.Length == 0 ? null : await store.GetCredentialAsync(email, cancellationToken);
        if (credential is null)
        {
            PasswordHasher.BurnTime(password);
            return Invalid();
        }

        var now = Now;
        if (credential.IsLocked(now))
        {
            PasswordHasher.BurnTime(password);
            return Invalid();
        }

        if (!PasswordHasher.Verify(password, credential.PasswordHash, credential.PasswordSalt, credential.Iterations))
        {
            var failed = credential.FailedAttempts + 1;
            var locking = failed >= MaxFailedAttempts;
            await store.SaveCredentialAsync(credential with { FailedAttempts = locking ? 0 : failed, LockedUntil = locking ? now + LockoutDuration : credential.LockedUntil }, cancellationToken);
            if (locking)
            {
                await auditRepository.WriteAsync(Audit(Platform, credential.UserId, "auth.sign_in.locked", "Too many wrong passwords; sign-in paused for 15 minutes.", correlationId), cancellationToken);
            }

            return Invalid();
        }

        var memberships = await store.ListMembershipsAsync(credential.UserId, cancellationToken);
        if (memberships.Count == 0)
        {
            return new SignInResult("NoWorkspace", null, null, null, null);
        }

        var chosen = memberships.Count == 1
            ? memberships[0]
            : memberships.FirstOrDefault(item => string.Equals(item.WorkspaceId, command.WorkspaceId, StringComparison.Ordinal)
                && (command.TenantId is null || string.Equals(item.TenantId, command.TenantId, StringComparison.Ordinal)));
        if (chosen is not { } scope)
        {
            var choices = new List<WorkspaceChoice>();
            foreach (var membership in memberships)
            {
                choices.Add(new WorkspaceChoice(membership.TenantId, membership.WorkspaceId, await NameOfAsync(membership, cancellationToken)));
            }

            return new SignInResult("ChooseWorkspace", null, null, null, choices);
        }

        var account = await userAccounts.GetAsync(credential.UserId, scope, cancellationToken);
        if (account is null || !string.Equals(account.Status, "Active", StringComparison.Ordinal))
        {
            return Invalid();
        }

        // A password made with fewer rounds than today's is quietly remade now that we hold the plain password.
        var updated = credential with { FailedAttempts = 0, LockedUntil = null };
        if (credential.Iterations < PasswordHasher.CurrentIterations)
        {
            var (hash, salt, iterations) = PasswordHasher.Hash(password);
            updated = updated with { PasswordHash = hash, PasswordSalt = salt, Iterations = iterations };
        }

        await store.SaveCredentialAsync(updated, cancellationToken);
        await userAccounts.SaveAsync(account with { LastLogin = now }, cancellationToken);
        var (token, expiresAt) = tokenIssuer.Issue(credential.UserId, credential.Email, account.Name, scope, account.Role.ToString());
        await auditRepository.WriteAsync(Audit(scope, credential.UserId, "auth.sign_in.succeeded", "Signed in with the platform's own sign-in.", correlationId), cancellationToken);
        return new SignInResult("SignedIn", token, expiresAt, new SignedInUser(credential.UserId, account.Name, credential.Email, scope.TenantId, scope.WorkspaceId, account.Role.ToString()), null);
    }

    private static SignInResult Invalid() => new("InvalidCredentials", null, null, null, null);

    private async Task<string> NameOfAsync(TenantScope scope, CancellationToken cancellationToken)
    {
        var identity = await portalIdentities.GetAsync(scope, cancellationToken);
        return identity?.DisplayName ?? $"{scope.TenantId} / {scope.WorkspaceId}";
    }

    // ---- passwords ----

    /// <summary>Always answers the same. Returns false only when the platform cannot send email.</summary>
    public async Task<bool> ForgotPasswordAsync(string? emailInput, string correlationId, CancellationToken cancellationToken = default)
    {
        var email = LocalCredential.NormaliseEmail(emailInput ?? string.Empty);
        var baseUrl = portalAddress.BaseUrlFor(Platform);
        if (baseUrl is null)
        {
            return false;
        }

        var credential = email.Length == 0 ? null : await store.GetCredentialAsync(email, cancellationToken);
        if (credential is null)
        {
            return true;
        }

        var secret = IdentityTokens.NewSecret();
        var now = Now;
        var sent = await mailer.SendAsync(Platform, email, "Reset your IIP password",
            $"Someone asked to reset the password for this email address.\n\n{baseUrl}/reset-password?token={Uri.EscapeDataString(secret)}\n\nThe link works once and expires in an hour. If you did not ask, ignore this email: your password has not changed.",
            correlationId, cancellationToken);
        if (!sent)
        {
            return false;
        }

        await store.SaveResetAsync(new PasswordReset { TokenHash = IdentityTokens.Hash(secret), UserId = credential.UserId, Email = email, CreatedAt = now, ExpiresAt = now + ResetLifetime }, cancellationToken);
        await auditRepository.WriteAsync(Audit(Platform, credential.UserId, "auth.password_reset.requested", "A password reset link was sent.", correlationId), cancellationToken);
        return true;
    }

    /// <summary>Null when it worked; otherwise why not. A used or expired link and a weak password are told apart so the person can act.</summary>
    public async Task<string?> ResetPasswordAsync(string? secret, string? newPassword, string correlationId, CancellationToken cancellationToken = default)
    {
        var reset = string.IsNullOrWhiteSpace(secret) ? null : await store.GetResetAsync(IdentityTokens.Hash(secret), cancellationToken);
        if (reset is null || reset.ConsumedAt is not null || reset.ExpiresAt <= Now)
        {
            return "This reset link is no longer valid. Ask for a new one.";
        }

        if (PasswordPolicy.Check(newPassword, reset.Email) is { } problem)
        {
            return problem;
        }

        var credential = await store.GetCredentialAsync(reset.Email, cancellationToken);
        if (credential is null || credential.UserId != reset.UserId)
        {
            return "This reset link is no longer valid. Ask for a new one.";
        }

        var (hash, salt, iterations) = PasswordHasher.Hash(newPassword!);
        await store.SaveCredentialAsync(credential with { PasswordHash = hash, PasswordSalt = salt, Iterations = iterations, FailedAttempts = 0, LockedUntil = null, PasswordChangedAt = Now }, cancellationToken);
        await store.SaveResetAsync(reset with { ConsumedAt = Now }, cancellationToken);
        await auditRepository.WriteAsync(Audit(Platform, credential.UserId, "auth.password_reset.completed", "Password changed with a reset link.", correlationId), cancellationToken);
        return null;
    }

    /// <summary>For a signed-in person. Null when changed; otherwise why not.</summary>
    public async Task<string?> ChangePasswordAsync(string userId, string email, string? current, string? replacement, string correlationId, CancellationToken cancellationToken = default)
    {
        var credential = await store.GetCredentialAsync(LocalCredential.NormaliseEmail(email), cancellationToken);
        if (credential is null || credential.UserId != userId)
        {
            return "Your password is managed by your organisation's Microsoft account.";
        }

        if (!PasswordHasher.Verify(current ?? string.Empty, credential.PasswordHash, credential.PasswordSalt, credential.Iterations))
        {
            return "Your current password is not right.";
        }

        if (PasswordPolicy.Check(replacement, credential.Email) is { } problem)
        {
            return problem;
        }

        var (hash, salt, iterations) = PasswordHasher.Hash(replacement!);
        await store.SaveCredentialAsync(credential with { PasswordHash = hash, PasswordSalt = salt, Iterations = iterations, PasswordChangedAt = Now }, cancellationToken);
        await auditRepository.WriteAsync(Audit(Platform, userId, "auth.password.changed", "Password changed by its owner.", correlationId), cancellationToken);
        return null;
    }

    // ---- invitations ----

    /// <summary>Invites a person. Returns the invitation, or a plain reason it cannot be made.</summary>
    public async Task<(InvitationView? Invitation, string? Problem, bool MailNotConfigured)> InviteAsync(
        TenantScope scope, string actorUserId, string? emailInput, string? roleInput, string correlationId, CancellationToken cancellationToken = default)
    {
        var email = (emailInput ?? string.Empty).Trim();
        if (!IsEmail(email))
        {
            return (null, "Enter a valid email address.", false);
        }

        if (!Enum.TryParse<PlatformRole>(roleInput, ignoreCase: true, out var role) || !Enum.IsDefined(role))
        {
            return (null, "Choose a role for the person.", false);
        }

        var normalised = LocalCredential.NormaliseEmail(email);
        var members = await userAccounts.ListAsync(scope, cancellationToken);
        if (members.Any(member => string.Equals(member.Email, normalised, StringComparison.OrdinalIgnoreCase) && member.Status == "Active"))
        {
            return (null, "That person is already a member of this workspace.", false);
        }

        var now = Now;
        var existing = await store.ListInvitationsAsync(scope, cancellationToken);
        var settings = await settingsRepository.GetAsync(scope, cancellationToken);
        var pending = existing.Where(item => item.Status(now) == "Pending" && !string.Equals(item.Email, normalised, StringComparison.OrdinalIgnoreCase)).Count();
        var seatLimit = await SeatLimitAsync(scope, settings, cancellationToken);
        if (members.Count(member => member.Status == "Active") + pending >= seatLimit)
        {
            return (null, $"This workspace is at its limit of {seatLimit} people, counting invitations that are still open. Upgrade the plan or revoke an invitation.", false);
        }

        var baseUrl = portalAddress.BaseUrlFor(scope);
        if (baseUrl is null)
        {
            return (null, null, true);
        }

        // One open invitation per person: a new one replaces the old.
        foreach (var old in existing.Where(item => item.Status(now) == "Pending" && string.Equals(item.Email, normalised, StringComparison.OrdinalIgnoreCase)))
        {
            await store.SaveInvitationAsync(old with { RevokedAt = now }, cancellationToken);
        }

        var secret = IdentityTokens.NewSecret();
        var hash = IdentityTokens.Hash(secret);
        var workspaceName = await NameOfAsync(scope, cancellationToken);
        var sent = await mailer.SendAsync(scope, normalised, $"You are invited to join {workspaceName} on IIP",
            $"You have been invited to join {workspaceName} as {role}.\n\n{baseUrl}/accept-invite?token={Uri.EscapeDataString(secret)}{portalAddress.LinkQueryFor(scope)}\n\nThe invitation works once and expires in 7 days. If you were not expecting it, ignore this email.",
            correlationId, cancellationToken);
        if (!sent)
        {
            return (null, null, true);
        }

        var invitation = new StaffInvitation
        {
            InvitationId = IdentityTokens.KeyFor("invitation", hash),
            Scope = scope,
            TokenHash = hash,
            Email = normalised,
            Role = role.ToString(),
            InvitedByUserId = actorUserId,
            CreatedAt = now,
            ExpiresAt = now + InvitationLifetime,
        };
        await store.SaveInvitationAsync(invitation, cancellationToken);
        await auditRepository.WriteAsync(Audit(scope, actorUserId, "workspace.invitation.sent", $"Invited a person as {role}.", correlationId), cancellationToken);
        return (ToView(invitation, now), null, false);
    }

    public async Task<IReadOnlyList<InvitationView>> ListInvitationsAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var now = Now;
        return (await store.ListInvitationsAsync(scope, cancellationToken)).Select(item => ToView(item, now)).ToArray();
    }

    public async Task<bool> RevokeInvitationAsync(TenantScope scope, string actorUserId, Guid invitationId, string correlationId, CancellationToken cancellationToken = default)
    {
        var invitation = await store.GetInvitationAsync(scope, invitationId, cancellationToken);
        if (invitation is null || invitation.Status(Now) != "Pending")
        {
            return false;
        }

        await store.SaveInvitationAsync(invitation with { RevokedAt = Now }, cancellationToken);
        await auditRepository.WriteAsync(Audit(scope, actorUserId, "workspace.invitation.revoked", "An open invitation was withdrawn.", correlationId), cancellationToken);
        return true;
    }

    public async Task<InvitationPreview?> PreviewInvitationAsync(string? secret, CancellationToken cancellationToken = default)
    {
        var invitation = await FindOpenInvitationAsync(secret, cancellationToken);
        if (invitation is null)
        {
            return null;
        }

        var hasAccount = await store.GetCredentialAsync(invitation.Email, cancellationToken) is not null;
        return new InvitationPreview(await NameOfAsync(invitation.Scope, cancellationToken), invitation.Email, invitation.Role, hasAccount);
    }

    /// <summary>Accepts an invitation. A new person chooses a name and password; someone who already has a sign-in proves it with their password.</summary>
    public async Task<(SignedInUser? User, string? Problem)> AcceptInvitationAsync(
        string? secret, string? name, string? password, string correlationId, CancellationToken cancellationToken = default)
    {
        var invitation = await FindOpenInvitationAsync(secret, cancellationToken);
        if (invitation is null)
        {
            return (null, "This invitation is no longer valid. Ask for a new one.");
        }

        var settings = await settingsRepository.GetAsync(invitation.Scope, cancellationToken);
        var members = await userAccounts.ListAsync(invitation.Scope, cancellationToken);
        if (members.Count(member => member.Status == "Active") >= await SeatLimitAsync(invitation.Scope, settings, cancellationToken))
        {
            return (null, "This workspace is at its limit of people. Ask the administrator to upgrade the plan.");
        }

        var now = Now;
        var credential = await store.GetCredentialAsync(invitation.Email, cancellationToken);
        string userId;
        string displayName;
        if (credential is null)
        {
            displayName = name?.Trim() ?? string.Empty;
            if (displayName.Length is < 2 or > 80)
            {
                return (null, "Enter your name, up to 80 characters.");
            }

            if (PasswordPolicy.Check(password, invitation.Email) is { } problem)
            {
                return (null, problem);
            }

            var (hash, salt, iterations) = PasswordHasher.Hash(password!);
            userId = Guid.NewGuid().ToString("N");
            await store.SaveCredentialAsync(new LocalCredential
            {
                UserId = userId, Email = invitation.Email, PasswordHash = hash, PasswordSalt = salt, Iterations = iterations, CreatedAt = now, PasswordChangedAt = now,
            }, cancellationToken);
        }
        else
        {
            if (credential.IsLocked(now) || !PasswordHasher.Verify(password ?? string.Empty, credential.PasswordHash, credential.PasswordSalt, credential.Iterations))
            {
                return (null, "That password is not right for the account this invitation is for.");
            }

            userId = credential.UserId;
            displayName = members.FirstOrDefault(member => member.UserId == userId)?.Name ?? name?.Trim() ?? invitation.Email;
        }

        var role = Enum.Parse<PlatformRole>(invitation.Role, ignoreCase: true);
        await userAccounts.SaveAsync(new InsuranceUserAccount { UserId = userId, Scope = invitation.Scope, Name = displayName, Email = invitation.Email, Role = role }, cancellationToken);
        await membershipWriter.GrantMembershipAsync(invitation.Scope.TenantId, invitation.Scope.WorkspaceId, userId, role.ToString(), cancellationToken);
        await store.SaveInvitationAsync(invitation with { AcceptedAt = now }, cancellationToken);
        await auditRepository.WriteAsync(Audit(invitation.Scope, userId, "workspace.invitation.accepted", $"Joined as {role}.", correlationId), cancellationToken);
        return (new SignedInUser(userId, displayName, invitation.Email, invitation.Scope.TenantId, invitation.Scope.WorkspaceId, role.ToString()), null);
    }

    private async Task<StaffInvitation?> FindOpenInvitationAsync(string? secret, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return null;
        }

        var invitation = await store.GetInvitationByTokenAsync(IdentityTokens.Hash(secret), cancellationToken);
        return invitation is not null && invitation.Status(Now) == "Pending" ? invitation : null;
    }

    private static InvitationView ToView(StaffInvitation invitation, DateTimeOffset now) =>
        new(invitation.InvitationId, invitation.Email, invitation.Role, invitation.Status(now), invitation.CreatedAt, invitation.ExpiresAt, invitation.InvitedByUserId);

    private static bool IsEmail(string value) =>
        value.Length is > 3 and <= 120 && System.Net.Mail.MailAddress.TryCreate(value, out var parsed) && string.Equals(parsed.Address, value, StringComparison.OrdinalIgnoreCase) && value.Contains('.');

    // The plan's seats are a ceiling over the workspace's own setting; a workspace on no plan keeps its setting.
    private async Task<int> SeatLimitAsync(TenantScope scope, TenantSettings settings, CancellationToken cancellationToken) =>
        plans is null ? settings.SeatLimit : (await plans.GetEntitlementsAsync(scope, cancellationToken)).Seats;

    private AuditEvent Audit(TenantScope scope, string actor, string action, string reason, string correlationId) =>
        new(
            Guid.NewGuid(), scope, null, null, actor, ActorType.User, action, Now,
            BeforeStatus: null, AfterStatus: null, EvidenceIds: [], ModelProvider: null, Confidence: null,
            ValidationResult: "accepted", ApprovalId: null, AuditReason: reason, CorrelationId: correlationId);
}
