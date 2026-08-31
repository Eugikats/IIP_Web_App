using System.Text.Json.Serialization;

namespace InsuranceIntelligence.Application.Search;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GlobalSearchResultType
{
    Case,
    Document,
    WorkflowTask,
    Approval
}

public sealed record GlobalSearchResultDto(
    GlobalSearchResultType Type,
    string Id,
    string? CaseId,
    string Title,
    string Subtitle,
    string MatchReason,
    string Href,
    DateTimeOffset UpdatedAt);

public sealed record GlobalSearchResponse(
    string Query,
    int ReturnedCount,
    bool HasMore,
    IReadOnlyList<GlobalSearchResultDto> Results);

public interface IGlobalSearchService
{
    Task<GlobalSearchResponse> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default);
}
