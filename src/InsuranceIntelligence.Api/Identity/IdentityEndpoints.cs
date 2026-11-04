using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Identity;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Identity;

public sealed record SignUpBody(string? CompanyName, string? AdminName, string? Email, string? Password, bool AcceptTerms, string? Website);
public sealed record VerifySignUpBody(string? Token);
public sealed record SignInBody(string? Email, string? Password, string? WorkspaceId, string? TenantId = null);
public sealed record ForgotPasswordBody(string? Email);
public sealed record ResetPasswordBody(string? Token, string? Password);
public sealed record ChangePasswordBody(string? CurrentPassword, string? NewPassword);
public sealed record InviteBody(string? Email, string? Role);
public sealed record AcceptInvitationBody(string? Token, string? Name, string? Password);

/// <summary>
/// Self-serve sign-up, the platform's own sign-in, password reset and staff invitations (build-loop item 30). Everything a stranger
/// can reach here answers the same whether or not an email is known, is rate limited, and sends no secret anywhere but the
/// person's own inbox. The access token it issues says who a person is; what they may do still comes from the membership store.
/// </summary>
public static class IdentityEndpoints
{
    public static IResult Capabilities(LocalAuthOptions localAuth, InfrastructureOptions options) =>
        Results.Ok(new
        {
            signUp = localAuth.SelfServeSignUp && localAuth.Enabled,
            passwordSignIn = localAuth.Enabled,
            entra = options.AuthMode.Equals("Entra", StringComparison.OrdinalIgnoreCase),
        });

    public static async Task<IResult> SignUp(SignUpBody body, LocalIdentityService service, LocalAuthOptions localAuth, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (!localAuth.SelfServeSignUp || !localAuth.Enabled)
        {
            return Results.NotFound();
        }

        var command = new SignUpCommand(body.CompanyName, body.AdminName, body.Email, body.Password, body.AcceptTerms, body.Website);
        var checkedCommand = service.ValidateSignUp(command);
        if (!checkedCommand.IsValid && string.IsNullOrWhiteSpace(body.Website))
        {
            return Results.ValidationProblem(checkedCommand.Problems!.ToDictionary(item => item.Key, item => new[] { item.Value }));
        }

        return await service.SignUpAsync(command, httpContext.TraceIdentifier, cancellationToken)
            ? Results.Accepted(value: new { status = "VerificationSent", message = "Check your email. We sent a link to confirm your address." })
            : NotConfiguredMail();
    }

    public static async Task<IResult> VerifySignUp(VerifySignUpBody body, LocalIdentityService service, LocalAuthOptions localAuth, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (!localAuth.SelfServeSignUp || !localAuth.Enabled)
        {
            return Results.NotFound();
        }

        var user = await service.VerifySignUpAsync(body.Token, httpContext.TraceIdentifier, cancellationToken);
        return user is null
            ? Results.BadRequest(new { message = "This confirmation link is not valid, has already been used or has expired. Start again to get a new one." })
            : Results.Ok(new { status = "Created", email = user.Email, message = "Your workspace is ready. Sign in to begin." });
    }

    public static async Task<IResult> SignIn(SignInBody body, LocalIdentityService service, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await service.SignInAsync(new SignInCommand(body.Email, body.Password, body.WorkspaceId, body.TenantId), httpContext.TraceIdentifier, cancellationToken);
        return result.Outcome switch
        {
            "SignedIn" => Results.Ok(new { accessToken = result.AccessToken, expiresAt = result.ExpiresAt, user = result.User }),
            "ChooseWorkspace" => Results.Json(new { outcome = "ChooseWorkspace", message = "You belong to more than one workspace. Choose one.", workspaces = result.Workspaces }, statusCode: StatusCodes.Status409Conflict),
            "NoWorkspace" => Results.Json(new { message = "This account is not a member of any workspace." }, statusCode: StatusCodes.Status403Forbidden),
            "NotConfigured" => Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Not configured", detail: "Signing in with an email and password is not set up on this deployment."),
            // One answer for a wrong email, a wrong password and a locked account, so nothing here says which accounts exist.
            _ => Results.Json(new { message = "The email or password is not right, or the account is paused for a few minutes after too many tries." }, statusCode: StatusCodes.Status401Unauthorized),
        };
    }

    public static async Task<IResult> ForgotPassword(ForgotPasswordBody body, LocalIdentityService service, LocalAuthOptions localAuth, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (!localAuth.Enabled)
        {
            return Results.NotFound();
        }

        return await service.ForgotPasswordAsync(body.Email, httpContext.TraceIdentifier, cancellationToken)
            ? Results.Accepted(value: new { status = "Sent", message = "If that email has an account, we have sent a link to reset the password." })
            : NotConfiguredMail();
    }

    public static async Task<IResult> ResetPassword(ResetPasswordBody body, LocalIdentityService service, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var problem = await service.ResetPasswordAsync(body.Token, body.Password, httpContext.TraceIdentifier, cancellationToken);
        return problem is null ? Results.Ok(new { status = "Changed", message = "Your password has been changed. Sign in with it." }) : Results.BadRequest(new { message = problem });
    }

    public static async Task<IResult> ChangePassword(ChangePasswordBody body, LocalIdentityService service, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        var userId = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
        var email = httpContext.User.FindFirst("email")?.Value ?? string.Empty;
        var problem = await service.ChangePasswordAsync(userId, email, body.CurrentPassword, body.NewPassword, httpContext.TraceIdentifier, cancellationToken);
        return problem is null ? Results.Ok(new { status = "Changed" }) : Results.BadRequest(new { message = problem });
    }

    // ---- invitations ----

    public static async Task<IResult> Invite(InviteBody body, LocalIdentityService service, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out var error))
        {
            return Validation("tenantScope", error);
        }

        var (invitation, problem, mailNotConfigured) = await service.InviteAsync(scope, userId, body.Email, body.Role, httpContext.TraceIdentifier, cancellationToken);
        if (mailNotConfigured)
        {
            return NotConfiguredMail();
        }

        return invitation is null ? Validation("invitation", problem ?? "The invitation could not be made.") : Results.Created($"/api/v1/admin/invitations/{invitation.InvitationId}", invitation);
    }

    public static async Task<IResult> ListInvitations(HttpContext httpContext, LocalIdentityService service, CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var error))
        {
            return Validation("tenantScope", error);
        }

        return Results.Ok(await service.ListInvitationsAsync(scope, cancellationToken));
    }

    public static async Task<IResult> Revoke(Guid invitationId, HttpContext httpContext, LocalIdentityService service, CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out var error))
        {
            return Validation("tenantScope", error);
        }

        return await service.RevokeInvitationAsync(scope, userId, invitationId, httpContext.TraceIdentifier, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound(new { message = "No open invitation with that id in this workspace." });
    }

    public static async Task<IResult> PreviewInvitation(string? token, LocalIdentityService service, CancellationToken cancellationToken)
    {
        var preview = await service.PreviewInvitationAsync(token, cancellationToken);
        return preview is null ? Results.NotFound(new { message = "This invitation is no longer valid." }) : Results.Ok(preview);
    }

    public static async Task<IResult> AcceptInvitation(AcceptInvitationBody body, LocalIdentityService service, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var (user, problem) = await service.AcceptInvitationAsync(body.Token, body.Name, body.Password, httpContext.TraceIdentifier, cancellationToken);
        return user is null ? Results.BadRequest(new { message = problem }) : Results.Ok(new { status = "Joined", email = user.Email, message = "You have joined the workspace. Sign in to begin." });
    }

    private static IResult NotConfiguredMail() =>
        Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Not configured", detail: "This deployment cannot send email yet, so the message could not be sent.");

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}

/// <summary>Sends the platform's own emails through the deployment's email sender (or the workspace's own, once it has one connected).</summary>
public sealed class CommunicationIdentityMailer(ICommunicationSendingProvider sender) : IIdentityMailer
{
    public async Task<bool> SendAsync(InsuranceIntelligence.Domain.Common.TenantScope scope, string recipient, string subject, string body, string correlationId, CancellationToken cancellationToken = default)
    {
        var result = await sender.SendEmailAsync(
            new CommunicationSendRequest(scope, null, CommunicationChannel.Email, recipient, body, "system:identity", Approved: true, LowRisk: true, correlationId, subject),
            cancellationToken);
        return result.DeliveryStatus is "Sent" or "SimulatedSent";
    }
}
