using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Domain.Administration;

namespace InsuranceIntelligence.Api.People;

/// <summary>
/// The people in this workspace, by name, for any staff screen that has to show who did something
/// or who owns something, and for the claim assignment picker.
///
/// Records carry actors as their directory object id, which is correct for audit but meaningless on
/// screen. The full admin user list (GET /api/v1/admin/users) needs ManageTenantSettings, so a
/// claims officer could never have resolved a colleague's name through it. This returns only what
/// is safe to show a colleague: the id, the name and the role. No email, no phone, no status history.
///
/// The directory is filled by sign-in (RuntimeContextEndpoints.GetMe records each person the first
/// time they use the app), so a name appears here once that person has signed in at least once.
/// </summary>
public static class PeopleEndpoints
{
    public sealed record PersonResponse(string UserId, string Name, string Role);

    public static async Task<IResult> ListPeople(
        IUserAccountRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        var users = await repository.ListAsync(scope, cancellationToken);
        var people = users
            .Where(user => string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
            .Where(user => !string.IsNullOrWhiteSpace(user.Name))
            .OrderBy(user => user.Name, StringComparer.OrdinalIgnoreCase)
            .Select(user => new PersonResponse(user.UserId, user.Name, user.Role.ToString()))
            .ToArray();

        return Results.Ok(people);
    }
}
