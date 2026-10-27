using System.Text.Json;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.Workspaces;

/// <summary>What a workspace is called to its customers, and what it sells. Stored per workspace so that one
/// deployment can speak as an insurer in one workspace and as a bank selling partner covers in another.</summary>
public sealed record WorkspaceBrandSettings(
    string Organisation,
    string ShortName,
    IReadOnlyList<string> PartnerInsurers,
    LineFocus LineFocus,
    string? ScenarioId = null,
    bool IsDemonstration = false)
{
    public KatoBrand ToBrand() => new(Organisation, ShortName, PartnerInsurers, LineFocus);

    public static WorkspaceBrandSettings From(KatoBrand brand) =>
        new(brand.Organisation, brand.ShortName, brand.PartnerInsurers, brand.LineFocus);
}

/// <summary>Works out whose assistant Kato is for a workspace.</summary>
public interface IWorkspaceBrandResolver
{
    /// <summary>The workspace's own brand, or the start-up default when it has not set one.</summary>
    Task<KatoBrand> ResolveAsync(TenantScope scope, CancellationToken cancellationToken = default);
}

public sealed class WorkspaceBrandException(string message) : Exception(message);

public sealed class WorkspaceBrandService(IWorkspaceRecordStore store) : IWorkspaceBrandResolver
{
    public const string Kind = "brand";
    public const string Key = "current";
    public const int MaxNameLength = 80;
    public const int MaxPartners = 8;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public async Task<WorkspaceBrandSettings?> GetAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await store.GetAsync(scope, Kind, Key, cancellationToken);
        if (record is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<WorkspaceBrandSettings>(record.PayloadJson, Json);
        }
        catch (JsonException)
        {
            // A damaged record must not silence the assistant: fall back to the default brand.
            return null;
        }
    }

    public async Task<KatoBrand> ResolveAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        (await GetAsync(scope, cancellationToken))?.ToBrand() ?? KatoBrand.Current;

    public async Task<WorkspaceBrandSettings> SaveAsync(TenantScope scope, WorkspaceBrandSettings settings, CancellationToken cancellationToken = default)
    {
        var clean = Validate(settings);
        await store.SaveAsync(scope, Kind, Key, JsonSerializer.Serialize(clean, Json), cancellationToken);
        return clean;
    }

    public static WorkspaceBrandSettings Validate(WorkspaceBrandSettings settings)
    {
        var organisation = (settings.Organisation ?? string.Empty).Trim();
        var shortName = string.IsNullOrWhiteSpace(settings.ShortName) ? organisation : settings.ShortName.Trim();
        if (organisation.Length == 0)
        {
            throw new WorkspaceBrandException("The organisation name is required.");
        }

        if (organisation.Length > MaxNameLength || shortName.Length > MaxNameLength)
        {
            throw new WorkspaceBrandException($"Names can be at most {MaxNameLength} characters.");
        }

        var partners = (settings.PartnerInsurers ?? [])
            .Select(name => name?.Trim() ?? string.Empty)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (partners.Length > MaxPartners || partners.Any(name => name.Length > MaxNameLength))
        {
            throw new WorkspaceBrandException($"At most {MaxPartners} partner insurers, each at most {MaxNameLength} characters.");
        }

        if (!Enum.IsDefined(settings.LineFocus))
        {
            throw new WorkspaceBrandException("The line of business must be life, general or both.");
        }

        return settings with { Organisation = organisation, ShortName = shortName, PartnerInsurers = partners };
    }
}
