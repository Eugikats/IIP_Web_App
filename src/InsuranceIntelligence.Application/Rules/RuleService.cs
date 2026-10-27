using System.Text.Json;
using System.Text.Json.Serialization;
using InsuranceIntelligence.Application.BankEvents;
using InsuranceIntelligence.Application.Templates;
using InsuranceIntelligence.Application.Workspaces;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.Rules;

/// <summary>
/// Writes, approves, pauses and lists a workspace's rules. A rule runs only when it is approved and unchanged since: any edit
/// returns it to draft and clears the approval. Who may approve is the workspace's own choice, and the person who approves is
/// named in the record. All of it is scoped to the workspace.
/// </summary>
public sealed class RuleService(IWorkspaceRecordStore store, WorkspaceBrandService brands, TemplateCatalogService templates, TimeProvider clock)
{
    public const string Kind = "rule";
    public const string SettingsKind = "rule-settings";
    public const string SettingsKey = "approval";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public async Task<IReadOnlyList<OutreachRule>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        (await store.ListAsync(scope, Kind, 500, cancellationToken))
            .Select(record => Read<OutreachRule>(record.PayloadJson))
            .Where(rule => rule is not null)
            .Select(rule => rule!)
            .OrderBy(rule => rule.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public async Task<OutreachRule?> GetAsync(TenantScope scope, string ruleId, CancellationToken cancellationToken = default)
    {
        var record = await store.GetAsync(scope, Kind, ruleId, cancellationToken);
        return record is null ? null : Read<OutreachRule>(record.PayloadJson);
    }

    public async Task<IReadOnlyList<OutreachRule>> LiveForEventAsync(TenantScope scope, string eventType, CancellationToken cancellationToken = default) =>
        (await ListAsync(scope, cancellationToken)).Where(rule => rule.IsLive && rule.EventType == eventType).ToArray();

    /// <summary>Creates a rule (no id) or edits one. Either way the result is a draft that has to be approved.</summary>
    public async Task<OutreachRule> SaveAsync(TenantScope scope, OutreachRule input, string actorId, CancellationToken cancellationToken = default)
    {
        var existing = string.IsNullOrWhiteSpace(input.RuleId) ? null : await GetAsync(scope, input.RuleId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(input.RuleId) && existing is null)
        {
            throw RuleException.NotFound("That rule does not exist.");
        }

        var rule = input with
        {
            RuleId = existing?.RuleId ?? $"rule-{Guid.NewGuid():N}"[..17],
            Name = (input.Name ?? string.Empty).Trim(),
            Language = (input.Language ?? DefaultTemplates.Language).Trim().ToLowerInvariant(),
            TemplateName = (input.TemplateName ?? string.Empty).Trim().ToLowerInvariant(),
            FollowUpTemplate = string.IsNullOrWhiteSpace(input.FollowUpTemplate) ? null : input.FollowUpTemplate.Trim().ToLowerInvariant(),
            AllowedProductLines = (input.AllowedProductLines ?? []).Select(line => line.Trim()).Where(line => line.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Version = (existing?.Version ?? 0) + 1,
            Status = RuleStatus.Draft,
            CreatedBy = existing?.CreatedBy ?? actorId,
            UpdatedAt = clock.GetUtcNow(),
            ApprovedBy = null,
            ApprovedByRole = null,
            ApprovedAt = null,
            ApprovedFingerprint = null,
        };
        await ValidateAsync(scope, rule, cancellationToken);
        await Write(scope, rule, cancellationToken);
        return rule;
    }

    public async Task<OutreachRule> ApproveAsync(
        TenantScope scope, string ruleId, string actorId, IReadOnlyList<string> actorRoles, CancellationToken cancellationToken = default)
    {
        var rule = await RequireAsync(scope, ruleId, cancellationToken);
        var settings = await GetSettingsAsync(scope, cancellationToken);
        var role = actorRoles.FirstOrDefault(candidate => settings.AllowedRoles.Contains(candidate, StringComparer.OrdinalIgnoreCase));
        if (role is null)
        {
            throw RuleException.Forbidden($"Only these roles may approve a rule here: {string.Join(", ", settings.AllowedRoles)}.");
        }

        if (settings.RequireDifferentApprover && string.Equals(rule.CreatedBy, actorId, StringComparison.Ordinal))
        {
            throw RuleException.Forbidden("The person who wrote a rule cannot be the one to approve it here. Ask a colleague.");
        }

        await ValidateAsync(scope, rule, cancellationToken);
        var approved = rule with
        {
            Status = RuleStatus.Approved,
            ApprovedBy = actorId,
            ApprovedByRole = role,
            ApprovedAt = clock.GetUtcNow(),
            ApprovedFingerprint = rule.Fingerprint(),
            UpdatedAt = clock.GetUtcNow(),
        };
        await Write(scope, approved, cancellationToken);
        return approved;
    }

    public async Task<OutreachRule> PauseAsync(TenantScope scope, string ruleId, CancellationToken cancellationToken = default)
    {
        var rule = await RequireAsync(scope, ruleId, cancellationToken);
        var paused = rule with { Status = RuleStatus.Paused, UpdatedAt = clock.GetUtcNow() };
        await Write(scope, paused, cancellationToken);
        return paused;
    }

    /// <summary>Switches a paused rule back on. It must still be exactly what was approved; if it was changed it needs approving again.</summary>
    public async Task<OutreachRule> ResumeAsync(TenantScope scope, string ruleId, CancellationToken cancellationToken = default)
    {
        var rule = await RequireAsync(scope, ruleId, cancellationToken);
        if (rule.Status != RuleStatus.Paused)
        {
            throw RuleException.Conflict("Only a paused rule can be resumed.");
        }

        if (rule.ApprovedFingerprint != rule.Fingerprint())
        {
            throw RuleException.Conflict("This rule was changed after it was approved. Approve it again to switch it on.");
        }

        var resumed = rule with { Status = RuleStatus.Approved, UpdatedAt = clock.GetUtcNow() };
        await Write(scope, resumed, cancellationToken);
        return resumed;
    }

    public async Task<RuleApprovalSettings> GetSettingsAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await store.GetAsync(scope, SettingsKind, SettingsKey, cancellationToken);
        return (record is null ? null : Read<RuleApprovalSettings>(record.PayloadJson)) is { AllowedRoles.Count: > 0 } settings
            ? settings
            : RuleApprovalSettings.Default;
    }

    public async Task<RuleApprovalSettings> SaveSettingsAsync(TenantScope scope, RuleApprovalSettings settings, CancellationToken cancellationToken = default)
    {
        var roles = (settings.AllowedRoles ?? []).Select(role => role.Trim()).Where(role => role.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (roles.Length == 0)
        {
            throw RuleException.Invalid("Choose at least one role that may approve rules.");
        }

        var clean = settings with { AllowedRoles = roles };
        await store.SaveAsync(scope, SettingsKind, SettingsKey, JsonSerializer.Serialize(clean, Json), cancellationToken);
        return clean;
    }

    private async Task ValidateAsync(TenantScope scope, OutreachRule rule, CancellationToken cancellationToken)
    {
        if (rule.Name.Length is 0 or > 100)
        {
            throw RuleException.Invalid("Give the rule a name of up to 100 characters.");
        }

        if (BankEventCatalog.Find(rule.EventType) is null)
        {
            throw RuleException.Invalid("Choose what has to happen for the rule to apply.");
        }

        if (rule.WaitDays is < 0 or > 60)
        {
            throw RuleException.Invalid("Wait between 0 and 60 days.");
        }

        if (rule.Hours.FromHour is < 0 or > 23 || rule.Hours.ToHour is < 1 or > 24 || rule.Hours.FromHour >= rule.Hours.ToHour)
        {
            throw RuleException.Invalid("Sending hours must start before they end, between 00:00 and 24:00.");
        }

        if (rule.Limits.OnePerPersonDays < 1 || rule.Limits.SameCoverDays < 1 || rule.Limits.DailyLimit is < 1 or > 5000)
        {
            throw RuleException.Invalid("Limits must be at least 1, and at most 5,000 people a day.");
        }

        if (rule.Conditions.MinAmount is < 0 || rule.Conditions.MinTermMonths is < 0)
        {
            throw RuleException.Invalid("Conditions cannot be negative.");
        }

        var brand = (await brands.ResolveAsync(scope, cancellationToken));
        var sold = brand.Products.Select(product => product.ProductLine).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (rule.AllowedProductLines.Count == 0)
        {
            throw RuleException.Invalid("Choose at least one cover the rule may offer.");
        }

        var foreign = rule.AllowedProductLines.FirstOrDefault(line => !sold.Contains(line));
        if (foreign is not null)
        {
            throw RuleException.Invalid($"This organisation does not sell '{foreign}'.");
        }

        await RequireUsableTemplateAsync(scope, rule.TemplateName, rule.Language, "opening message", cancellationToken);
        if (rule.FollowUpAfterDays is not null)
        {
            if (rule.FollowUpAfterDays is < 1 or > 30 || string.IsNullOrWhiteSpace(rule.FollowUpTemplate))
            {
                throw RuleException.Invalid("A follow-up needs a number of days (1 to 30) and a template.");
            }

            await RequireUsableTemplateAsync(scope, rule.FollowUpTemplate!, rule.Language, "follow-up", cancellationToken);
        }
    }

    private async Task RequireUsableTemplateAsync(TenantScope scope, string name, string language, string role, CancellationToken cancellationToken)
    {
        var template = await templates.GetAsync(scope, name, language, cancellationToken)
            ?? await templates.GetAsync(scope, name, DefaultTemplates.Language, cancellationToken)
            ?? throw RuleException.Invalid($"There is no template called {name} for the {role}.");
        var unsupported = template.Parameters.FirstOrDefault(parameter => !RuleValues.ForEvents.Contains(parameter));
        if (unsupported is not null)
        {
            throw RuleException.Invalid($"The template {name} needs '{unsupported}', which a rule triggered by an event cannot supply. Choose another template.");
        }
    }

    private async Task<OutreachRule> RequireAsync(TenantScope scope, string ruleId, CancellationToken cancellationToken) =>
        await GetAsync(scope, ruleId, cancellationToken) ?? throw RuleException.NotFound("That rule does not exist.");

    private Task Write(TenantScope scope, OutreachRule rule, CancellationToken cancellationToken) =>
        store.SaveAsync(scope, Kind, rule.RuleId, JsonSerializer.Serialize(rule, Json), cancellationToken);

    private static T? Read<T>(string json) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
