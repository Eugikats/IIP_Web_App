using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Domain.Knowledge;

namespace InsuranceIntelligence.Api.Local.Mock;

/// <summary>
/// Deterministic chat completion for local runs and tests. It never calls a model. Every answer is
/// plainly labelled as mock output, is built only from lines already present in the prompt, and
/// contains no decision language, so the human-review guardrails see exactly what they would see
/// from a cautious real model.
/// </summary>
public sealed class MockChatCompletionProvider : IChatCompletionProvider
{
    public const string Label = "MOCK AI OUTPUT, not produced by a real model.";

    public Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
    {
        var facts = new List<string>();
        foreach (var line in request.Prompt.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("CLAIM:", StringComparison.Ordinal)
                || trimmed.StartsWith("Customer:", StringComparison.Ordinal)
                || trimmed.StartsWith("Product line:", StringComparison.Ordinal)
                || trimmed.StartsWith("Incident date:", StringComparison.Ordinal)
                || trimmed.StartsWith("Completeness:", StringComparison.Ordinal))
            {
                facts.Add(trimmed);
            }
        }

        var body = new StringBuilder();
        body.AppendLine(Label);
        if (facts.Count > 0)
        {
            body.AppendLine(string.Join(" ", facts));
        }

        body.AppendLine("Summary for the reviewer: the claim facts above were read from the case record and its documents. Nothing has been verified by this mock. A person must review the documents and decide.");
        var content = InsuranceIntelligence.Domain.Common.DisplayText.WithoutEmDashes(body.ToString().Trim());

        return Task.FromResult(new ChatCompletionResult(
            content,
            "Mock",
            "mock-chat",
            DateTimeOffset.UtcNow,
            "mock-chat-v1",
            PromptTokens: Math.Max(1, request.Prompt.Length / 4),
            CompletionTokens: Math.Max(1, content.Length / 4)));
    }
}

/// <summary>Deterministic embedding: a fixed-size vector derived from a SHA-256 of the text, so the
/// same text always yields the same vector. Not semantically meaningful.</summary>
public sealed class MockEmbeddingProvider : IEmbeddingProvider
{
    public Task<IReadOnlyCollection<float>> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty));
        var vector = new float[32];
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (hash[i] - 128) / 128f;
        }

        return Task.FromResult<IReadOnlyCollection<float>>(vector);
    }
}

/// <summary>
/// A scripted stand-in for the WhatsApp assistant's tool-calling model. It is stateless: it decides
/// the next step only from the message list it is given, so the same conversation always produces
/// the same tool calls and the same reply. The script: call get_my_account first; a message that
/// reports a loss is read back and the customer is asked to reply YES; a YES after that read-back
/// calls file_claim; a question about covers calls list_products. Replies are labelled as mock.
/// </summary>
public sealed partial class MockToolCallingChatProvider : IToolCallingChatProvider
{
    private const string ReadBackMarker = "reply YES";

    public Task<AgentChatStep> NextAsync(
        IReadOnlyList<AgentChatMessage> messages,
        IReadOnlyList<AgentToolSpec> tools,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var lastUserIndex = -1;
        for (var i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i].Role == "user")
            {
                lastUserIndex = i;
                break;
            }
        }

        var userText = lastUserIndex >= 0 ? messages[lastUserIndex].Content ?? string.Empty : string.Empty;
        var toolResultsThisTurn = messages.Skip(lastUserIndex + 1).Where(message => message.Role == "tool").ToArray();
        var toolNamesThisTurn = messages
            .Skip(lastUserIndex + 1)
            .Where(message => message.ToolCalls is { Count: > 0 })
            .SelectMany(message => message.ToolCalls!)
            .Select(call => call.Name)
            .ToHashSet(StringComparer.Ordinal);

        var previousAssistant = messages.Take(Math.Max(0, lastUserIndex)).LastOrDefault(message => message.Role == "assistant")?.Content ?? string.Empty;
        var isYes = YesPattern().IsMatch(userText) && previousAssistant.Contains(ReadBackMarker, StringComparison.OrdinalIgnoreCase);
        var reportsLoss = LossPattern().IsMatch(userText);
        var asksAboutCovers = CoverPattern().IsMatch(userText);

        if (toolResultsThisTurn.Length == 0)
        {
            if (isYes)
            {
                var story = messages
                    .Take(lastUserIndex)
                    .Where(message => message.Role == "user" && LossPattern().IsMatch(message.Content ?? string.Empty))
                    .Select(message => message.Content!)
                    .LastOrDefault() ?? "A loss was reported on WhatsApp.";
                return Step(null, Call("mock-file-claim", "file_claim", new { what_happened = story, when_it_happened = "recently" }));
            }

            if (reportsLoss)
            {
                return Step(null, Call("mock-account", "get_my_account", new { }));
            }

            if (asksAboutCovers)
            {
                return Step(null, Call("mock-products", "list_products", new { }));
            }

            return Step(Labelled("Hello, I can explain our covers or help you report a claim. What would you like to do?"));
        }

        if (toolNamesThisTurn.Contains("file_claim"))
        {
            var link = LinkPattern().Match(toolResultsThisTurn[^1].Content ?? string.Empty);
            return Step(Labelled(link.Success
                ? $"Your claim has been filed. Send your papers through this secure link: {link.Value}"
                : "Your claim has been filed. A person will contact you about the papers needed."));
        }

        if (toolNamesThisTurn.Contains("get_my_account") && reportsLoss)
        {
            return Step(Labelled($"I understand: {userText.Trim()} If this is right, {ReadBackMarker} and I will file the claim."));
        }

        if (toolNamesThisTurn.Contains("list_products"))
        {
            return Step(Labelled("We offer several covers. Tell me about your work and I will suggest the one that fits."));
        }

        return Step(Labelled("Thank you. A person on the team will follow up if anything more is needed."));
    }

    private static string Labelled(string text) => $"(mock assistant) {text}";

    private static Task<AgentChatStep> Step(string? content, params AgentToolCall[] calls) =>
        Task.FromResult(new AgentChatStep(content, calls));

    private static AgentToolCall Call(string id, string name, object arguments) =>
        new(id, name, System.Text.Json.JsonSerializer.Serialize(arguments));

    [GeneratedRegex("^\\s*(yes|yeah|yep|ok|okay|confirm|correct)\\b", RegexOptions.IgnoreCase)]
    private static partial Regex YesPattern();

    [GeneratedRegex("\\b(accident|crash|stolen|theft|fire|damage|hospital|injur|died|death|lost|claim)", RegexOptions.IgnoreCase)]
    private static partial Regex LossPattern();

    [GeneratedRegex("\\b(cover|covers|insurance|product|products|price|cost|join|buy)\\b", RegexOptions.IgnoreCase)]
    private static partial Regex CoverPattern();

    [GeneratedRegex("https?://[^\\s\"]+")]
    private static partial Regex LinkPattern();
}
