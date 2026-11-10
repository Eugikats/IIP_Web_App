using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Fraud;

/// <summary>A fact about the claim that the fraud agent was shown, with the id it must cite to rely on it.</summary>
public sealed record FraudAgentFact(string Id, string Text);

/// <summary>
/// One thing the fraud agent noticed. It always cites the facts it rests on, always says what a person
/// should check, and never says anyone did anything wrong. Advisory only.
/// </summary>
public sealed record FraudAgentFinding(
    string IndicatorCode,
    string Title,
    string Observation,
    string WhatToCheck,
    FraudSignalSeverity Severity,
    IReadOnlyList<string> FactIds);

/// <summary>
/// The fraud agent's review of one claim. The findings are kept with the facts it was shown, so a
/// person can open every citation, and with the playbook version it was briefed under.
/// </summary>
public sealed record FraudAgentReview
{
    public required Guid ReviewId { get; init; }
    public required TenantScope Scope { get; init; }
    public required Guid CaseId { get; init; }
    public required IReadOnlyList<FraudAgentFinding> Findings { get; init; }
    public required IReadOnlyList<FraudAgentFact> Facts { get; init; }

    /// <summary>Hash of the facts the review was written from. When the claim changes, the hash changes and the review shows as out of date.</summary>
    public required string FactsHash { get; init; }
    public required string PlaybookVersion { get; init; }
    public required string Provider { get; init; }
    public string? Deployment { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required string GeneratedBy { get; init; }
    public required string CorrelationId { get; init; }

    /// <summary>How many things the model wrote that were left out because they cited nothing real or used wording that is not allowed.</summary>
    public int LeftOut { get; init; }

    public static string HashFacts(IReadOnlyList<FraudAgentFact> facts)
    {
        var builder = new StringBuilder();
        foreach (var fact in facts)
        {
            builder.Append(fact.Id).Append('=').Append(fact.Text).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))[..32];
    }
}

public interface IFraudAgentReviewRepository
{
    Task SaveAsync(FraudAgentReview review, CancellationToken cancellationToken = default);

    Task<FraudAgentReview?> GetLatestAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default);
}

public sealed record FraudAgentParseResult(IReadOnlyList<FraudAgentFinding> Findings, int LeftOut, bool Readable);

/// <summary>
/// Turns what the model wrote into findings, and keeps only the ones that hold up. A finding is kept
/// when it cites at least one fact the agent was really shown, uses a code from the playbook, says what
/// to check, and stays inside advisory wording. A single cited fact can raise at most a potential
/// anomaly; "requires investigation" needs two different facts behind it. Nothing here can make a
/// finding stronger than the facts allow.
/// </summary>
public static class FraudAgentOutput
{
    public const int MaxFindings = 6;
    private const int MaxTitle = 120;
    private const int MaxObservation = 500;
    private const int MaxCheck = 300;

    public static FraudAgentParseResult Parse(string? content, IReadOnlySet<string> validFactIds)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new FraudAgentParseResult([], 0, false);
        }

        var json = ExtractJson(content);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return new FraudAgentParseResult([], 0, false);
        }

        using (document)
        {
            var root = document.RootElement;
            JsonElement items;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("findings", out var found) && found.ValueKind == JsonValueKind.Array)
            {
                items = found;
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                items = root;
            }
            else
            {
                return new FraudAgentParseResult([], 0, false);
            }

            var kept = new List<FraudAgentFinding>();
            var leftOut = 0;
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    leftOut++;
                    continue;
                }

                var finding = ReadFinding(item, validFactIds);
                if (finding is null)
                {
                    leftOut++;
                    continue;
                }

                if (kept.Count >= MaxFindings || kept.Any(existing => existing.IndicatorCode == finding.IndicatorCode && existing.FactIds.SequenceEqual(finding.FactIds)))
                {
                    leftOut++;
                    continue;
                }

                kept.Add(finding);
            }

            return new FraudAgentParseResult(kept, leftOut, true);
        }
    }

    private static FraudAgentFinding? ReadFinding(JsonElement item, IReadOnlySet<string> validFactIds)
    {
        var code = Text(item, "indicator")?.Trim().ToUpperInvariant();
        var title = Clean(Text(item, "title"), MaxTitle);
        var observation = Clean(Text(item, "observation"), MaxObservation);
        var check = Clean(Text(item, "check"), MaxCheck);
        if (code is null || !FraudPlaybook.IsKnownCode(code) || title is null || observation is null || check is null)
        {
            return null;
        }

        if (!AdvisoryLanguage.IsAcceptable(title) || !AdvisoryLanguage.IsAcceptable(observation) || !AdvisoryLanguage.IsAcceptable(check))
        {
            return null;
        }

        var factIds = new List<string>();
        if (item.TryGetProperty("facts", out var facts) && facts.ValueKind == JsonValueKind.Array)
        {
            foreach (var fact in facts.EnumerateArray())
            {
                var id = fact.ValueKind == JsonValueKind.String ? fact.GetString()?.Trim().ToUpperInvariant() : null;
                if (id is not null && validFactIds.Contains(id) && !factIds.Contains(id))
                {
                    factIds.Add(id);
                }
            }
        }

        // No citation, no finding.
        if (factIds.Count == 0)
        {
            return null;
        }

        var severity = (Text(item, "severity")?.Trim().ToLowerInvariant()) switch
        {
            "investigate" or "requires_investigation" => FraudSignalSeverity.RequiresInvestigation,
            "potential" or "potential_anomaly" => FraudSignalSeverity.PotentialAnomaly,
            _ => FraudSignalSeverity.AdvisorySignal,
        };
        if (severity == FraudSignalSeverity.RequiresInvestigation && factIds.Count < 2)
        {
            severity = FraudSignalSeverity.PotentialAnomaly;
        }

        return new FraudAgentFinding(code, title, observation, check, severity, factIds);
    }

    private static string? Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? Clean(string? value, int max)
    {
        var text = DisplayText.WithoutEmDashes((value ?? string.Empty).Trim());
        return text.Length is > 0 && text.Length <= max ? text : null;
    }

    /// <summary>The JSON in a reply, whether or not the model wrapped it in a code fence or added a line before it.</summary>
    private static string ExtractJson(string content)
    {
        var text = content.Trim();
        var start = text.IndexOfAny(['{', '[']);
        var end = text.LastIndexOfAny(['}', ']']);
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }
}
