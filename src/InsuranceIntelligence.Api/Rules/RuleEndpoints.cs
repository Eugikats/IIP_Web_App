using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.BankEvents;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Rules;
using InsuranceIntelligence.Application.Templates;
using InsuranceIntelligence.Application.Workspaces;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Rules;

/// <summary>
/// Outreach rules: write one, approve it once, pause it, run the rules now, and read the decision log. Who may approve is a
/// workspace setting the administrator controls; the check is made on the server from the signed-in person's roles, never
/// from what the browser says. Every change is audited by rule id and action.
/// </summary>
public static class RuleEndpoints
{
    public static async Task<IResult> Overview(
        RuleService rules, WorkspaceBrandService brands, TemplateCatalogService templates, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (Resolve(httpContext, options, Permission.ManageSalesLeads, out var scope, out _, out var roles) is { } failed)
        {
            return failed;
        }

        var settings = await rules.GetSettingsAsync(scope, cancellationToken);
        var brand = await brands.ResolveAsync(scope, cancellationToken);
        var all = await templates.ListAsync(scope, cancellationToken);
        return Results.Ok(new
        {
            rules = (await rules.ListAsync(scope, cancellationToken)).Select(Describe),
            approval = new
            {
                allowedRoles = settings.AllowedRoles,
                requireDifferentApprover = settings.RequireDifferentApprover,
                youMayApprove = roles.Any(role => settings.AllowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase)),
            },
            events = BankEventCatalog.All.Select(type => new { code = type.Code, label = type.Label, suggestedOffer = type.SuggestedOffer }),
            covers = brand.Products.Select(product => new { line = product.ProductLine, name = product.Name, insurer = product.Insurer, insuranceClass = product.InsuranceClass.ToString() }),
            templates = all.Where(template => template.Parameters.All(RuleValues.ForEvents.Contains)).Select(template => new
            {
                name = template.Name, language = template.Language, category = template.Category.ToString(), status = template.Status.ToString(), template.Purpose,
            }),
        });
    }

    public static Task<IResult> Save(
        string? ruleId, RuleRequest request, RuleService rules, IAuditWriter audit, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Change(httpContext, options, audit, "saved", Permission.ManageSalesLeads, async (scope, userId, _) =>
        {
            var saved = await rules.SaveAsync(scope, FromRequest(ruleId, request), userId, cancellationToken);
            return (saved.RuleId, Describe(saved));
        });

    public static Task<IResult> Approve(
        string ruleId, RuleService rules, IAuditWriter audit, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Change(httpContext, options, audit, "approved", Permission.ManageSalesLeads, async (scope, userId, roles) =>
            (ruleId, Describe(await rules.ApproveAsync(scope, ruleId, userId, roles, cancellationToken))));

    public static Task<IResult> Pause(
        string ruleId, RuleService rules, IAuditWriter audit, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Change(httpContext, options, audit, "paused", Permission.ManageSalesLeads, async (scope, _, _) =>
            (ruleId, Describe(await rules.PauseAsync(scope, ruleId, cancellationToken))));

    public static Task<IResult> Resume(
        string ruleId, RuleService rules, IAuditWriter audit, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Change(httpContext, options, audit, "resumed", Permission.ManageSalesLeads, async (scope, _, _) =>
            (ruleId, Describe(await rules.ResumeAsync(scope, ruleId, cancellationToken))));

    public static Task<IResult> SaveApprovalSettings(
        ApprovalSettingsRequest request, RuleService rules, IAuditWriter audit, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Change(httpContext, options, audit, "approvers_changed", Permission.ConfigureIntegrations, async (scope, _, _) =>
        {
            var saved = await rules.SaveSettingsAsync(scope, new RuleApprovalSettings(request.AllowedRoles ?? [], request.RequireDifferentApprover), cancellationToken);
            return ("approval", (object)new { allowedRoles = saved.AllowedRoles, requireDifferentApprover = saved.RequireDifferentApprover });
        });

    public static async Task<IResult> Run(RuleEngine engine, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (Resolve(httpContext, options, Permission.ManageSalesLeads, out var scope, out var userId, out _) is { } failed)
        {
            return failed;
        }

        return Results.Ok(await engine.RunAsync(scope, userId, cancellationToken));
    }

    public static async Task<IResult> Outcomes(bool? needsPerson, OutcomeService outcomes, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (Resolve(httpContext, options, Permission.ManageSalesLeads, out var scope, out _, out _) is { } failed)
        {
            return failed;
        }

        var items = await outcomes.ListAsync(scope, needsPerson, 300, cancellationToken);
        return Results.Ok(items.Select(item => new
        {
            item.OutcomeId, phone = Mask(item.Phone), kind = item.Kind.ToString(), item.RuleName, item.EventType, item.Cover, item.Insurer, item.OpeningText,
            item.OfferedAt, item.At, item.Reply, item.NeedsPerson, item.LeadId, item.CustomerName, item.Done, item.DoneAt, item.DoneBy,
        }));
    }

    public static Task<IResult> MarkDone(
        string outcomeId, OutcomeService outcomes, IAuditWriter audit, TimeProvider clock, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Change(httpContext, options, audit, "followed_up", Permission.ManageSalesLeads, async (scope, userId, _) =>
        {
            var done = await outcomes.MarkDoneAsync(scope, outcomeId, userId, clock.GetUtcNow(), cancellationToken);
            return (outcomeId, (object)new { done.OutcomeId, done.Done, done.DoneAt, done.DoneBy });
        });

    public static async Task<IResult> Decisions(
        string? ruleId, string? phone, string? outcome, DecisionLog log, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (Resolve(httpContext, options, Permission.ManageSalesLeads, out var scope, out _, out _) is { } failed)
        {
            return failed;
        }

        DecisionOutcome? parsed = Enum.TryParse<DecisionOutcome>(outcome, ignoreCase: true, out var value) ? value : null;
        var wanted = string.IsNullOrWhiteSpace(phone) ? null : EventService.NormalisePhoneOrNull(phone);
        var items = await log.ListAsync(scope, ruleId, wanted, parsed, 300, cancellationToken);
        return Results.Ok(items.Select(item => new
        {
            item.DecisionId, item.RuleId, item.RuleName, item.RuleVersion, item.EventType,
            phone = Mask(item.Phone), item.CustomerName, item.LeadId, outcome = item.Outcome.ToString(), item.Reason, item.DecidedAt, item.NotBefore,
            item.Cover, item.Insurer, item.TemplateName, item.Kind, gates = item.Gates,
        }));
    }

    private static async Task<IResult> Change(
        HttpContext httpContext, InfrastructureOptions options, IAuditWriter audit, string action, Permission permission,
        Func<TenantScope, string, IReadOnlyList<string>, Task<(string Id, object Result)>> work)
    {
        if (Resolve(httpContext, options, permission, out var scope, out var userId, out var roles) is { } failed)
        {
            return failed;
        }

        try
        {
            var (id, result) = await work(scope, userId, roles);
            await audit.WriteAsync(
                AuditEvent.WorkspaceSettingChanged(scope, userId, "rule", action, id, $"Rule {id} {action}.", httpContext.TraceIdentifier),
                httpContext.RequestAborted);
            return Results.Ok(result);
        }
        catch (RuleException failure)
        {
            var status = failure.Code switch
            {
                "not_found" => StatusCodes.Status404NotFound,
                "forbidden" => StatusCodes.Status403Forbidden,
                "conflict" => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(failure.Message, statusCode: status);
        }
    }

    private static IResult? Resolve(
        HttpContext httpContext, InfrastructureOptions options, Permission permission, out TenantScope scope, out string userId, out IReadOnlyList<string> roles)
    {
        scope = default!;
        userId = string.Empty;
        roles = [];
        if (DemoAuthorization.Require(httpContext, options, permission) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out scope, out userId, out var scopeError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [scopeError] });
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["userId"] = ["A signed-in user is required."] });
        }

        roles = httpContext.RequestServices.GetService<ITenantContextAccessor>() is { HasCurrent: true } accessor ? accessor.Current.RoleCodes.ToArray() : [];
        return null;
    }

    private static OutreachRule FromRequest(string? ruleId, RuleRequest request) => new(
        ruleId ?? string.Empty,
        request.Name ?? string.Empty,
        0,
        request.EventType ?? string.Empty,
        new RuleConditions(request.MinAmount, request.MinTermMonths),
        request.WaitDays ?? 0,
        request.AllowedProductLines ?? [],
        request.TemplateName ?? string.Empty,
        request.Language ?? DefaultTemplates.Language,
        new RuleHours(request.FromHour ?? 9, request.ToHour ?? 18),
        new RuleLimits(request.OnePerPersonDays ?? 30, request.SameCoverDays ?? 90, request.DailyLimit ?? 50),
        request.FollowUpAfterDays,
        request.FollowUpTemplate,
        RuleStatus.Draft,
        string.Empty,
        DateTimeOffset.UtcNow);

    private static string Mask(string phone) => phone.Length <= 6 ? phone : $"+{phone[..3]} {new string('*', Math.Max(phone.Length - 6, 1))} {phone[^3..]}";

    private static object Describe(OutreachRule rule) => new
    {
        rule.RuleId, rule.Name, rule.Version, rule.EventType,
        eventLabel = BankEventCatalog.Find(rule.EventType)?.Label ?? rule.EventType,
        minAmount = rule.Conditions.MinAmount, minTermMonths = rule.Conditions.MinTermMonths,
        rule.WaitDays, rule.AllowedProductLines, rule.TemplateName, rule.Language,
        fromHour = rule.Hours.FromHour, toHour = rule.Hours.ToHour,
        onePerPersonDays = rule.Limits.OnePerPersonDays, sameCoverDays = rule.Limits.SameCoverDays, dailyLimit = rule.Limits.DailyLimit,
        rule.FollowUpAfterDays, rule.FollowUpTemplate,
        status = rule.Status.ToString(), isLive = rule.IsLive, changedSinceApproval = rule.Status != RuleStatus.Draft && rule.ApprovedFingerprint != rule.Fingerprint(),
        rule.CreatedBy, rule.UpdatedAt, rule.ApprovedBy, rule.ApprovedByRole, rule.ApprovedAt,
    };
}

public sealed record RuleRequest(
    string? Name, string? EventType, decimal? MinAmount, int? MinTermMonths, int? WaitDays, IReadOnlyList<string>? AllowedProductLines,
    string? TemplateName, string? Language, int? FromHour, int? ToHour, int? OnePerPersonDays, int? SameCoverDays, int? DailyLimit,
    int? FollowUpAfterDays, string? FollowUpTemplate);

public sealed record ApprovalSettingsRequest(IReadOnlyList<string>? AllowedRoles, bool RequireDifferentApprover);
