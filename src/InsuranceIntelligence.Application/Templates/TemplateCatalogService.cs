using System.Text.Json;
using System.Text.Json.Serialization;
using InsuranceIntelligence.Application.Workspaces;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.Templates;

/// <summary>
/// A workspace's catalogue of message templates. A workspace starts with the standard drafts; its people edit a draft,
/// submit it, and record WhatsApp's decision. Until real provider sync exists the decision is recorded by a person, and
/// the record says so. Every write names who made it, and a template that WhatsApp approved is never edited in place.
/// </summary>
public sealed class TemplateCatalogService(IWorkspaceRecordStore store)
{
    public const string Kind = "template";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<IReadOnlyList<MessageTemplate>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var stored = (await store.ListAsync(scope, Kind, 1000, cancellationToken))
            .Select(record => Read(record.PayloadJson))
            .Where(template => template is not null)
            .Select(template => template!)
            .ToDictionary(template => template.Key, StringComparer.Ordinal);

        // The standard drafts are shown until the workspace has its own of the same name and language, so a fresh
        // workspace is never empty and nothing has to be seeded before it can be used.
        foreach (var standard in DefaultTemplates.All)
        {
            stored.TryAdd(standard.Key, standard);
        }

        return stored.Values.OrderBy(template => template.Name, StringComparer.Ordinal).ThenBy(template => template.Language, StringComparer.Ordinal).ToArray();
    }

    public async Task<MessageTemplate?> GetAsync(TenantScope scope, string name, string language, CancellationToken cancellationToken = default)
    {
        var key = MessageTemplate.KeyOf(name, language);
        var record = await store.GetAsync(scope, Kind, key, cancellationToken);
        if (record is not null && Read(record.PayloadJson) is { } template)
        {
            return template;
        }

        return DefaultTemplates.All.FirstOrDefault(standard => standard.Key == key);
    }

    /// <summary>The approved template for this name, preferring the customer's language and falling back to English.
    /// Null when none is approved, which is the honest answer for a new workspace.</summary>
    public async Task<MessageTemplate?> FindSendableAsync(TenantScope scope, string name, string language, CancellationToken cancellationToken = default)
    {
        foreach (var code in new[] { language, DefaultTemplates.Language }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var template = await GetAsync(scope, name, code, cancellationToken);
            if (template is { IsSendable: true })
            {
                return template;
            }
        }

        return null;
    }

    /// <summary>Creates or edits a draft. Refuses to change a template that is awaiting a decision or approved.</summary>
    public async Task<MessageTemplate> SaveDraftAsync(
        TenantScope scope, MessageTemplate edit, string actorId, CancellationToken cancellationToken = default)
    {
        var clean = Clean(edit) with { Status = TemplateStatus.Draft, UpdatedAt = DateTimeOffset.UtcNow, UpdatedBy = actorId, DecisionNote = null, DecidedAt = null };
        RequireValid(clean);
        var existing = await GetAsync(scope, clean.Name, clean.Language, cancellationToken);
        if (existing is not null && !existing.IsEditable && existing.UpdatedAt != DateTimeOffset.UnixEpoch)
        {
            throw TemplateException.Conflict(
                existing.Status == TemplateStatus.Approved
                    ? "An approved template cannot be edited, because WhatsApp approved those exact words. Make a new template with a new name."
                    : "This template is waiting for WhatsApp's decision and cannot be edited now.");
        }

        await Save(scope, clean, cancellationToken);
        return clean;
    }

    /// <summary>Marks a draft as submitted to WhatsApp for approval. Locks it until a decision is recorded.</summary>
    public async Task<MessageTemplate> SubmitAsync(TenantScope scope, string name, string language, string actorId, CancellationToken cancellationToken = default)
    {
        var template = await RequireAsync(scope, name, language, cancellationToken);
        if (!template.IsEditable)
        {
            throw TemplateException.Conflict("Only a draft or a rejected template can be submitted.");
        }

        RequireValid(template);
        var submitted = template with { Status = TemplateStatus.AwaitingApproval, UpdatedAt = DateTimeOffset.UtcNow, UpdatedBy = actorId, DecisionNote = null, DecidedAt = null };
        await Save(scope, submitted, cancellationToken);
        return submitted;
    }

    /// <summary>Records what WhatsApp decided. Until the provider reports it automatically, a person enters it, and the
    /// record carries who and when.</summary>
    public async Task<MessageTemplate> RecordDecisionAsync(
        TenantScope scope, string name, string language, TemplateStatus decision, string? note, string actorId, CancellationToken cancellationToken = default)
    {
        if (decision is not (TemplateStatus.Approved or TemplateStatus.Rejected or TemplateStatus.Paused))
        {
            throw TemplateException.Invalid("A decision is approved, rejected or paused.");
        }

        var template = await RequireAsync(scope, name, language, cancellationToken);
        if (template.Status is TemplateStatus.Draft)
        {
            throw TemplateException.Conflict("Submit the template to WhatsApp before recording its decision.");
        }

        var cleanNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (decision == TemplateStatus.Rejected && cleanNote is null)
        {
            throw TemplateException.Invalid("Say why WhatsApp rejected it, so the next draft can fix that.");
        }

        var now = DateTimeOffset.UtcNow;
        var decided = template with { Status = decision, UpdatedAt = now, UpdatedBy = actorId, DecisionNote = cleanNote, DecidedAt = now };
        await Save(scope, decided, cancellationToken);
        return decided;
    }

    /// <summary>Loads templates the owner supplies (for example ones already approved by WhatsApp). The status in the file
    /// is kept, because the file is the record of what WhatsApp decided. Returns how many were stored and what was refused.</summary>
    public async Task<TemplateImportResult> ImportAsync(
        TenantScope scope, IReadOnlyList<MessageTemplate> templates, string actorId, CancellationToken cancellationToken = default)
    {
        var stored = 0;
        var refused = new List<string>();
        foreach (var incoming in templates)
        {
            var clean = Clean(incoming) with { UpdatedAt = DateTimeOffset.UtcNow, UpdatedBy = actorId };
            var problems = clean.Problems();
            if (problems.Count > 0)
            {
                refused.Add($"{clean.Name} ({clean.Language}): {problems[0]}");
                continue;
            }

            await Save(scope, clean, cancellationToken);
            stored++;
        }

        return new TemplateImportResult(stored, refused);
    }

    private async Task<MessageTemplate> RequireAsync(TenantScope scope, string name, string language, CancellationToken cancellationToken) =>
        await GetAsync(scope, name, language, cancellationToken) ?? throw TemplateException.NotFound("That template does not exist.");

    private static MessageTemplate Clean(MessageTemplate template) => template with
    {
        Name = (template.Name ?? string.Empty).Trim().ToLowerInvariant(),
        Language = (template.Language ?? string.Empty).Trim().ToLowerInvariant(),
        Purpose = (template.Purpose ?? string.Empty).Trim(),
        Body = (template.Body ?? string.Empty).Trim(),
        Parameters = (template.Parameters ?? []).Select(parameter => parameter.Trim()).ToArray(),
        Buttons = (template.Buttons ?? []).Select(label => label.Trim()).ToArray(),
    };

    private static void RequireValid(MessageTemplate template)
    {
        var problems = template.Problems();
        if (problems.Count > 0)
        {
            throw TemplateException.Invalid(problems[0]);
        }
    }

    private Task Save(TenantScope scope, MessageTemplate template, CancellationToken cancellationToken) =>
        store.SaveAsync(scope, Kind, template.Key, JsonSerializer.Serialize(template, Json), cancellationToken);

    private static MessageTemplate? Read(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<MessageTemplate>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed record TemplateImportResult(int Stored, IReadOnlyList<string> Refused);
