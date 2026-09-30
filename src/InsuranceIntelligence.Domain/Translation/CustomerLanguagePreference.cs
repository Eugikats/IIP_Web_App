using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Translation;

/// <summary>Step 1 of the language-resolution order (build plan §6.B): what a contact's messages
/// are answered in, once it has been established, so the assistant does not re-detect language on
/// every single message. Append-only, the same shape as ConsentRecord and CustomerConversationTurn
/// in this subsystem, every save is a new row and the most recent one within the freshness window
/// wins.</summary>
public sealed record CustomerLanguagePreference
{
    public required Guid PreferenceId { get; init; }
    public required TenantScope Scope { get; init; }
    public required string ContactWaId { get; init; }
    public required SupportedLanguageCode Language { get; init; }
    public required DateTimeOffset SetAt { get; init; }
}

public interface ICustomerLanguagePreferenceRepository
{
    /// <summary>The most recent preference on file, regardless of age; whether it is still fresh
    /// enough to use is <see cref="LanguageResolutionOrder"/>'s decision, not the repository's.</summary>
    Task<CustomerLanguagePreference?> GetCurrentAsync(TenantScope scope, string contactWaId, CancellationToken cancellationToken = default);

    Task SaveAsync(CustomerLanguagePreference preference, CancellationToken cancellationToken = default);

    /// <summary>Forgets every preference on file for the number, so the next message is resolved
    /// as if from someone new. Used by the demo reset on a shared phone.</summary>
    Task ClearAsync(TenantScope scope, string contactWaId, CancellationToken cancellationToken = default);
}
