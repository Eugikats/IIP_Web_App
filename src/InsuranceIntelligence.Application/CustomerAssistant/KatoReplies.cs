using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Application.CustomerAssistant;

/// <summary>
/// Everything Kato, Nile Shield's helper on WhatsApp, says in his own voice. Fixed text, never
/// rephrased by a model: short sentences that read well aloud to someone with limited literacy,
/// outcome words rather than process words, no lists, and a route to a person on every opening.
///
/// Kato never claims to be a person and never performs being one; asked directly, he says plainly
/// that he is not. Papers with personal details (IDs, permits, police and medical reports) are only
/// ever asked for through the secure link, never in the chat.
/// </summary>
public static class KatoReplies
{
    public const string PersonWord = "PERSON";

    private static string Comma(string? firstName) => string.IsNullOrWhiteSpace(firstName) ? "" : $", {firstName}";

    public static string Introduction(string? firstName, bool voiceNotes) =>
        $"Hello{Comma(firstName)}. I'm Kato, {KatoBrand.Current.ShortName}'s helper on WhatsApp. " +
        "If you have cover with us and something has happened, I can help you get your money. " +
        "I can also tell you what your cover pays for, or find cover that fits your work. " +
        (voiceNotes
            ? "Just tell me what you need in your own words. You can write or send a voice note, in the language you like best."
            : "Just write to me in your own words, in the language you like best.");

    /// <summary>Opens the first reply to an unknown number when that reply is not the introduction itself.</summary>
    public static string FirstContactPrefix(string? firstName) =>
        $"Hello{Comma(firstName)}, I'm Kato, {KatoBrand.Current.ShortName}'s helper on WhatsApp.";

    public const string AskName = "What should I call you?";

    public static string NiceToMeetYou(string firstName) =>
        $"Nice to meet you, {firstName}. How can I help you today? If something has happened, just tell me what happened.";

    public static string WelcomeBack(string firstName) =>
        $"Hello {firstName}, it's Kato from {KatoBrand.Current.ShortName}. If something has happened, tell me and I will help you get your money. " +
        $"I can also tell you where your claim is, or what your cover pays for.";

    public static string Clarify() =>
        "Sorry, I didn't understand that. Has something happened that you want help with? " +
        "Or do you want to know about your cover, or check on a claim?";

    public static string HandingToPerson(string? firstName) =>
        $"Thank you{Comma(firstName)}. We will get back to you shortly, right here.";

    // Warm and plain, and still true: Kato never claims to be human. The people behind him are real,
    // and one of them takes over whenever the customer wants.
    public static string NotAPerson(string? firstName) =>
        $"I'm Kato{Comma(firstName)}, {KatoBrand.Current.ShortName}'s assistant here on WhatsApp. I'm not a person myself, but I work with our team, " +
        "and they see what you tell me. I'm here day and night to help you get things done quickly.";

    public static string YoureWelcome(string? firstName) =>
        $"You're welcome{Comma(firstName)}. I'm here any time you need me.";

    public static string PersonRequested(string? firstName) =>
        $"Of course{Comma(firstName)}. We will get back to you shortly, right here.";

    public static string CheckingAnswer(string? firstName) =>
        $"Thank you for your question{Comma(firstName)}. I want to be sure I give you the right answer, so we are checking it. We will get back to you shortly, right here.";

    public static string NoTrustedAnswer() =>
        "I don't have an answer I can trust for that yet. We will get back to you shortly, right here.";

    public static string CannotHelpDirectly() =>
        "I'm not able to help with that myself. We will get back to you shortly, right here.";

    // ---- Claims ----

    public static string NoCoverOnThisNumber(string? firstName) =>
        $"I'm sorry to hear that{Comma(firstName)}. I can't find cover with {KatoBrand.Current.ShortName} for this phone number, " +
        "and I can only start a claim from the phone number on the policy. " +
        "If you don't have cover yet, tell me what work you do, and I'll tell you about cover that could help you.";

    public static string PolicyNotOnThisNumber(string? firstName) =>
        $"Thank you{Comma(firstName)}. That policy is not on this phone number, so I can't use it here. Please check the policy number on your documents and tell me again.";

    public static string WhichCover(string firstName, IReadOnlyList<Policy> policies)
    {
        var choices = policies.Select((policy, index) => $"{index + 1} for your {policy.ProductLine} cover").ToArray();
        var joined = choices.Length <= 1
            ? string.Join("", choices)
            : $"{string.Join(", ", choices[..^1])}, or {choices[^1]}";
        return $"I'm sorry to hear that, {firstName}. Which cover is this about? Reply {joined}.";
    }

    public static string AskWhatHappened(string firstName, string productLine) =>
        $"I'm sorry to hear that, {firstName}. I hope you are safe. " +
        $"Tell me in your own words what happened, and when and where it happened. I will use your {productLine} cover.";

    public static string ReadBack(string? firstName, string productLine, string description) =>
        $"Let me check I have this right{Comma(firstName)}. This is for your {productLine} cover. " +
        $"You told me: \"{string.Join(" ", description.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))}\". " +
        "Is that right? Reply YES and I will send it to our claims team. Or tell me what to change.";

    public static string TellMeAgain(string? firstName) =>
        $"No problem{Comma(firstName)}. Tell me again what happened, in your own words, and I will read it back to you.";

    public static string AlreadyReported(string? firstName, string reference, DateTimeOffset reportedAt) =>
        $"You already told me about this on {reportedAt:d MMMM}{Comma(firstName)}. Your reference is {reference}. " +
        "I have added what you just said to it, so you don't need to start again. " +
        "If this is a different accident, reply NEW and I will start a new report.";

    public static string ClaimSent(string? firstName, string reference, string documentsSentence, string? link, IReadOnlyList<string>? papersOnFile = null) =>
        $"Thank you{Comma(firstName)}. I have sent your report to our claims team. Your reference is {reference}. " +
        "A person will check it and tell you here what happens next. " +
        $"To pay you, they will need {documentsSentence}. " +
        (link is null
            ? "Someone from our team will tell you how to share those papers safely."
            : $"Please share those papers only through this secure link, not in this chat. It works for 24 hours: {link}") +
        (papersOnFile is { Count: > 0 } && link is not null
            ? $" You do not have to send everything again: we already have {string.Join(", ", papersOnFile)}. On the page, choose \"Use the one on file\" for it."
            : string.Empty);

    // ---- Claim status and links ----

    public static string ClaimStatusList(string firstName, IEnumerable<string> claimSentences) =>
        $"Hello {firstName}. Here is where your claims are. {string.Join(" ", claimSentences)} " +
        "Someone from our team will message you here if they need anything. Reply LINK if you want to see your claim or send papers.";

    public static string ClaimStatusSentence(string reference, string productLine, DateTimeOffset reportedAt, string status) =>
        $"Claim {reference}, for your {productLine} cover, reported on {reportedAt:d MMMM}: {status}.";

    public static string NoOpenClaims(string firstName, string reference, string productLine, string status) =>
        $"Hello {firstName}. You have no open claims right now. Your last claim, {reference}, for your {productLine} cover: {status}. " +
        "If something new has happened, tell me and I'll help you.";

    public static string NoClaimsYet(string firstName) =>
        $"Hello {firstName}. I can't see any claims for you yet. If something has happened, tell me and I'll help you.";

    public static string OnlyForThePolicyNumber() =>
        "I can only share claim news with the phone number on the policy, and this number is not on any of our policies.";

    public static string SecureLink(string reference, string link) =>
        $"Here is your secure link for claim {reference}. It works for 24 hours: {link} " +
        "Please share papers like police reports or medical notes only through this link, not in this chat.";

    public static string NoOpenClaimForLink(string? firstName) =>
        $"You don't have an open claim right now{Comma(firstName)}. If something has happened, tell me and I'll help you.";

    public static string LinkUnavailable(string reference) =>
        $"Someone from our team will tell you how to send papers for claim {reference} safely.";

    // ---- Photos and voice ----

    public static string PhotoAdded(string? firstName, string reference) =>
        $"Thank you{Comma(firstName)}. I've added your photo to claim {reference}.";

    public static string PhotoHeld(string? firstName) =>
        $"Thank you{Comma(firstName)}. I have your photo. Someone from our team will look at it and reply to you here.";

    public static string CannotListenYet(string? firstName) =>
        $"Thank you{Comma(firstName)}. I can't listen to voice notes yet. Please write your message to me instead, in any language you like.";

    /// <summary>Voice notes work, but this one could not be made out (a bad connection, silence, a very short
    /// clip). The customer is told that, not that voice notes are unsupported.</summary>
    public static string CouldNotHearVoiceNote(string? firstName) =>
        $"Sorry{Comma(firstName)}, I could not make out that voice note. Please send it again, or write your message and I will answer straight away.";

    // ---- No policy: cover that fits their work ----

    public static string AskOccupation(string? firstName) =>
        $"I can help you find cover that fits your life{Comma(firstName)}. What work do you do? " +
        "For example, do you ride a boda boda, sell in a market or a shop, or something else?";

    /// <summary>The workspace has no cover on offer here yet, so nothing is explained and a person follows up.</summary>
    public static string NoCoversYet(string? firstName) =>
        $"Thank you{Comma(firstName)}. We do not have a cover to offer in this chat yet. Someone from {KatoBrand.Current.ShortName} will get back to you shortly.";

    public static string CoverSuggestion(string? firstName, string explanation) =>
        $"Thank you{Comma(firstName)}. {explanation} " +
        "These are microinsurance covers we are trying out, so nothing is sold in this chat. " +
        "We will get back to you shortly with more detail.";
}
