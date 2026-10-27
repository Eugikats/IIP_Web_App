using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Azure;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.BankEvents;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.DemoScenarios;
using InsuranceIntelligence.Application.Rules;
using InsuranceIntelligence.Application.Workspaces;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.DemoScenarios;

/// <summary>
/// The rehearsal tools: whether this workspace is a demonstration, adding a demonstration customer, and the timeline that answers
/// "why did this person get this message". The first two exist only in a demonstration workspace and never under a protected
/// runtime profile. The timeline is available to anyone who manages leads, because it is what a compliance officer asks for.
/// </summary>
public static class DemoCustomerEndpoints
{
    public static async Task<IResult> Status(
        WorkspaceBrandService brands, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageSalesLeads) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var scopeError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [scopeError] });
        }

        var saved = await brands.GetAsync(scope, cancellationToken);
        return Results.Ok(new
        {
            isDemonstration = saved?.IsDemonstration == true,
            scenarioId = saved?.ScenarioId,
            brandName = saved?.Organisation,
            isBank = saved?.PartnerInsurers.Count > 0,
        });
    }

    public static async Task<IResult> Add(
        DemoCustomerBody body, DemoCustomerService service, IAuditWriter audit, InfrastructureOptions options, IWebHostEnvironment environment,
        HttpContext httpContext, CancellationToken cancellationToken)
    {
        var profile = AzureProviderStartupValidator.ResolveRuntimeProfile(options, environment.EnvironmentName);
        if (AzureProviderStartupValidator.IsProtectedRuntime(profile))
        {
            return Results.Problem($"Demonstration customers are not available under the '{profile}' runtime profile.", statusCode: StatusCodes.Status403Forbidden);
        }

        if (DemoAuthorization.Require(httpContext, options, Permission.ManageSalesLeads) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out var scopeError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [scopeError] });
        }

        if (!Enum.TryParse<DemoConsent>(body.Consent ?? "Agreed", ignoreCase: true, out var consent))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["consent"] = ["Choose agreed, not asked or declined."] });
        }

        try
        {
            var result = await service.AddAsync(
                scope,
                new DemoCustomerRequest(body.Name, body.Phone, body.Branch, body.BankProducts, body.IncomeBand, body.Dependants, body.LoanAmount, body.LoanTermMonths, body.LoanDaysAgo, consent),
                userId, cancellationToken);
            await audit.WriteAsync(
                AuditEvent.WorkspaceSettingChanged(scope, userId, "demo_customer", "added", result.LeadId, "A demonstration customer was added to a demonstration workspace.", httpContext.TraceIdentifier),
                cancellationToken);
            return Results.Ok(result);
        }
        catch (DemoCustomerException problem)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["customer"] = [problem.Message] });
        }
    }

    public static async Task<IResult> Timeline(
        string? phone, string? eventId, string? decisionId, PersonTimelineService timeline, EventService events, DecisionLog decisions,
        InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageSalesLeads) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var scopeError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [scopeError] });
        }

        // The lists show a masked number, so a row asks by the id of the event or decision it is about, and the server finds the person.
        if (!string.IsNullOrWhiteSpace(eventId))
        {
            phone = (await events.GetAsync(scope, eventId, cancellationToken))?.Phone;
        }
        else if (!string.IsNullOrWhiteSpace(decisionId))
        {
            phone = (await decisions.GetAsync(scope, decisionId, cancellationToken))?.Phone;
        }

        var normalised = EventService.NormalisePhoneOrNull(phone);
        if (normalised is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["phone"] = ["That is not a usable phone number."] });
        }

        var result = await timeline.GetAsync(scope, normalised, cancellationToken);
        return Results.Ok(new
        {
            phone = Mask(result.Phone),
            result.CustomerName,
            result.ConsentSummary,
            entries = result.Entries.Select(entry => new { entry.At, entry.Kind, entry.Title, entry.Detail, entry.Gates, entry.Outcome }),
        });
    }

    private static string Mask(string phone) => phone.Length <= 6 ? phone : $"+{phone[..3]} {new string('*', Math.Max(phone.Length - 6, 1))} {phone[^3..]}";
}

public sealed record DemoCustomerBody(
    string? Name, string? Phone, string? Branch, IReadOnlyList<string>? BankProducts, string? IncomeBand, string? Dependants,
    decimal? LoanAmount, int? LoanTermMonths, int? LoanDaysAgo, string? Consent);
