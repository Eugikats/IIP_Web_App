using System.Text.RegularExpressions;

namespace InsuranceIntelligence.Application.Templates;

/// <summary>What WhatsApp calls the kind of a template. Meta decides which kind it accepts; ours is the request.</summary>
public enum TemplateCategory
{
    Utility,
    Marketing,
    Authentication,
}

/// <summary>
/// Where a template stands. Only <see cref="Approved"/> can be sent. A new template starts as a draft that anyone with the
/// right may edit; once submitted it is locked until WhatsApp decides; an approved template is never edited in place,
/// because WhatsApp approved those exact words.
/// </summary>
public enum TemplateStatus
{
    Draft,
    AwaitingApproval,
    Approved,
    Rejected,
    Paused,
}

/// <summary>
/// One WhatsApp message template: the opening words a business is allowed to send to someone who has not written in
/// the last 24 hours. The body holds numbered placeholders ({{1}}, {{2}}); <see cref="Parameters"/> names what each
/// stands for, in order, so a rule or a person knows what to supply.
/// </summary>
public sealed record MessageTemplate(
    string Name,
    string Language,
    TemplateCategory Category,
    TemplateStatus Status,
    string Purpose,
    string Body,
    IReadOnlyList<string> Parameters,
    IReadOnlyList<string> Buttons,
    DateTimeOffset UpdatedAt,
    string? UpdatedBy = null,
    string? DecisionNote = null,
    DateTimeOffset? DecidedAt = null)
{
    public const int MaxBodyLength = 1024;
    public const int MaxButtons = 3;
    public const int MaxButtonLength = 25;

    private static readonly Regex Placeholder = new(@"\{\{(\d+)\}\}", RegexOptions.Compiled);
    private static readonly Regex NameShape = new("^[a-z][a-z0-9_]{0,59}$", RegexOptions.Compiled);

    /// <summary>The key a template is stored under: the same name can exist in several languages.</summary>
    public string Key => KeyOf(Name, Language);

    public static string KeyOf(string name, string language) => $"{name.Trim().ToLowerInvariant()}:{language.Trim().ToLowerInvariant()}";

    public bool IsSendable => Status == TemplateStatus.Approved;

    public bool IsEditable => Status is TemplateStatus.Draft or TemplateStatus.Rejected;

    /// <summary>How many placeholders the body has: the highest number used.</summary>
    public int PlaceholderCount => Placeholder.Matches(Body).Select(match => int.Parse(match.Groups[1].Value)).DefaultIfEmpty(0).Max();

    /// <summary>What is wrong with the template, or an empty list when it is fit to submit.</summary>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        if (!NameShape.IsMatch(Name ?? string.Empty))
        {
            problems.Add("The name must be lower case letters, numbers and underscores, starting with a letter.");
        }

        if (string.IsNullOrWhiteSpace(Language) || Language.Trim().Length is < 2 or > 10)
        {
            problems.Add("A language code such as en, lg or sw is required.");
        }

        var body = Body?.Trim() ?? string.Empty;
        if (body.Length == 0)
        {
            problems.Add("The body is required.");
        }

        if (body.Length > MaxBodyLength)
        {
            problems.Add($"The body can be at most {MaxBodyLength} characters.");
        }

        var numbers = Placeholder.Matches(body).Select(match => int.Parse(match.Groups[1].Value)).Distinct().Order().ToArray();
        if (numbers.Length > 0 && !numbers.SequenceEqual(Enumerable.Range(1, numbers.Length)))
        {
            problems.Add("Placeholders must be numbered 1, 2, 3 in order, with none missing.");
        }

        if (Parameters is null || Parameters.Count != numbers.Length)
        {
            problems.Add($"Name each placeholder: the body has {numbers.Length} and {Parameters?.Count ?? 0} are named.");
        }

        if (Category != TemplateCategory.Authentication
            && body.Length > 0
            && (body.StartsWith("{{", StringComparison.Ordinal) || body.EndsWith("}}", StringComparison.Ordinal)))
        {
            problems.Add("WhatsApp does not accept a template that starts or ends with a placeholder.");
        }

        if (Buttons is { Count: > MaxButtons })
        {
            problems.Add($"At most {MaxButtons} buttons.");
        }

        if (Buttons is not null && Buttons.Any(label => string.IsNullOrWhiteSpace(label) || label.Trim().Length > MaxButtonLength))
        {
            problems.Add($"Each button needs a label of at most {MaxButtonLength} characters.");
        }

        return problems;
    }

    /// <summary>The words the customer will read, with each placeholder filled in order. Refuses missing values, so a
    /// message can never go out with a hole in it.</summary>
    public string Render(IReadOnlyList<string> values)
    {
        var needed = PlaceholderCount;
        if (values.Count < needed || values.Take(needed).Any(string.IsNullOrWhiteSpace))
        {
            throw new TemplateRenderException($"The template {Name} needs {needed} value(s) and was given {values.Count(value => !string.IsNullOrWhiteSpace(value))}.");
        }

        return Placeholder.Replace(Body, match => OneLine(values[int.Parse(match.Groups[1].Value) - 1]));
    }

    // WhatsApp rejects values with line breaks, tabs or long runs of spaces.
    private static string OneLine(string value) => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}

public sealed class TemplateRenderException(string message) : Exception(message);

public sealed class TemplateException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;

    public static TemplateException Invalid(string message) => new("invalid", message);
    public static TemplateException NotFound(string message) => new("not_found", message);
    public static TemplateException Conflict(string message) => new("conflict", message);
}

/// <summary>The templates every workspace starts with, as drafts. None is approved: only WhatsApp can approve a
/// template, and nothing here pretends it has.</summary>
public static class DefaultTemplates
{
    public const string Language = "en";

    private static MessageTemplate Draft(
        string name, TemplateCategory category, string purpose, string body, string[] parameters, params string[] buttons) =>
        new(name, Language, category, TemplateStatus.Draft, purpose, body, parameters, buttons, DateTimeOffset.UnixEpoch);

    public static readonly IReadOnlyList<MessageTemplate> All =
    [
        Draft("renewal_30_days", TemplateCategory.Utility, "Renewal reminder, 30 days before",
            "Hello {{1}}, your {{2}} cover ({{3}}) ends on {{4}}. Would you like to renew it?",
            ["customer_name", "product", "policy_number", "end_date"], "Yes, renew", "Not now", "Talk to a person"),
        Draft("renewal_7_days", TemplateCategory.Utility, "Renewal reminder, 7 days before",
            "Hello {{1}}, a reminder: your {{2}} cover ends on {{3}}. Reply to renew or ask a question.",
            ["customer_name", "product", "end_date"], "Renew", "Not now"),
        Draft("premium_due", TemplateCategory.Utility, "Premium due",
            "Hello {{1}}, your {{2}} premium of {{3}} is due on {{4}}. Reply if you need help paying.",
            ["customer_name", "product", "amount", "due_date"], "I paid", "Need help"),
        Draft("policy_lapsed", TemplateCategory.Utility, "Win-back after a policy lapsed",
            "Hello {{1}}, your {{2}} cover ended on {{3}}. You can restart it. Would you like details?",
            ["customer_name", "product", "end_date"], "Yes", "No thanks"),
        Draft("claim_documents_needed", TemplateCategory.Utility, "Documents still needed for a claim",
            "Hello {{1}}, for claim {{2}} we still need: {{3}}. You can send photos here in this chat.",
            ["customer_name", "claim_reference", "documents_needed"], "Send now", "Need help"),
        Draft("claim_received", TemplateCategory.Utility, "Claim received, outside the 24 hour window",
            "Hello {{1}}, we received your claim {{2}}. A claims officer will review it. We will update you here.",
            ["customer_name", "claim_reference"]),
        Draft("claim_status_update", TemplateCategory.Utility, "Claim status changed",
            "Hello {{1}}, an update on claim {{2}}: {{3}}. Reply with any question.",
            ["customer_name", "claim_reference", "status"], "Talk to a person"),
        Draft("welcome_and_consent", TemplateCategory.Utility, "First contact from a list: ask to be allowed to message",
            "Hello {{1}}, this is Kato from {{2}}. May we message you here about your insurance?",
            ["customer_name", "organisation"], "Yes", "No"),
        Draft("quote_follow_up", TemplateCategory.Marketing, "Quiet after a quote",
            "Hello {{1}}, you asked about {{2}} cover. Would you like to go on?",
            ["customer_name", "product"], "Yes", "Not now"),
        Draft("satisfaction_check", TemplateCategory.Utility, "After a claim is settled",
            "Hello {{1}}, how was your claim {{2}} experience? Reply 1 to 5.",
            ["customer_name", "claim_reference"], "1", "3", "5"),
        Draft("verify_code", TemplateCategory.Authentication, "Identity check code",
            "{{1}} is your verification code.",
            ["code"]),
        Draft("bank_cover_intro", TemplateCategory.Marketing, "A bank customer is told they may qualify for a cover",
            "Hello {{1}}, this is Kato from {{2}}. As you bank with us ({{3}}), you may qualify for {{4}}. Would you like to see what it covers and what it costs?",
            ["customer_name", "organisation", "bank_relationship", "cover_name"], "Yes, show me", "Not now", "Stop messages"),
    ];
}
