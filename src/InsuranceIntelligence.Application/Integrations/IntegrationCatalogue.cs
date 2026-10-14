namespace InsuranceIntelligence.Application.Integrations;

/// <summary>
/// Where an integration really stands. Each is earned by a check, never by "a setting exists":
/// <list type="bullet">
/// <item><c>NotConfigured</c>: the platform side is missing (no provider, no app registration), so nobody can connect yet.</item>
/// <item><c>NotConnected</c>: it can be connected, and this organisation (or person) has not.</item>
/// <item><c>NeedsAttention</c>: connected, but a check that matters is failing.</item>
/// <item><c>Connected</c>: connected and every check that matters passes.</item>
/// </list>
/// </summary>
public enum IntegrationCardState
{
    NotConfigured,
    NotConnected,
    NeedsAttention,
    Connected,
}

/// <summary>One thing that was looked at. A check that is not <c>Blocking</c> is shown but does not by itself raise "needs attention".</summary>
public sealed record IntegrationCheck(string Name, bool Passed, string Note, bool Blocking = true);

public sealed record IntegrationCard(
    string Key,
    string Name,
    string Group,
    IntegrationCardState State,
    string Summary,
    IReadOnlyList<IntegrationCheck> Checks,
    string? SetupHint);

public static class IntegrationCardRules
{
    public static IntegrationCardState Decide(bool platformAvailable, bool connected, IReadOnlyList<IntegrationCheck> checks)
    {
        if (!platformAvailable) return IntegrationCardState.NotConfigured;
        if (!connected) return IntegrationCardState.NotConnected;
        return checks.Any(check => check.Blocking && !check.Passed) ? IntegrationCardState.NeedsAttention : IntegrationCardState.Connected;
    }

    public static string Label(IntegrationCardState state) => state switch
    {
        IntegrationCardState.Connected => "Connected",
        IntegrationCardState.NeedsAttention => "Needs attention",
        IntegrationCardState.NotConnected => "Not connected",
        _ => "Not configured",
    };
}

/// <summary>What was found when the catalogue was read. Plain values, so the cards can be derived and tested without infrastructure.</summary>
public sealed record WhatsAppFacts(
    bool Configured,
    string? ConfigurationSource,
    DateTimeOffset? LastReceiptAt,
    DateTimeOffset? LastAcceptedAt,
    int RefusedInLastDay,
    bool PublicAddressKnown);

/// <summary>
/// An organisation can connect its own provider (<c>Saved</c>), or the platform may have one wired for everybody
/// (<c>PlatformProviderLive</c>). A card is connected through either; its checks are only about a saved connection.
/// </summary>
public sealed record ConnectionFacts(bool PlatformProviderLive, bool Saved, bool Active, string? LastValidationStatus, DateTimeOffset? LastValidatedAt, string? ProviderKey);

public sealed record FileProviderFacts(bool Configured, bool Connected, string? AccountEmail, string? Reason);

public sealed record IntegrationFacts(
    DateTimeOffset Now,
    WhatsAppFacts WhatsApp,
    ConnectionFacts Email,
    ConnectionFacts Sms,
    bool TeamsProviderAvailable,
    FileProviderFacts GoogleDrive,
    FileProviderFacts Microsoft);

/// <summary>Builds the Integration Center cards. Only integrations that exist are listed; an unbuilt one is not shown.</summary>
public static class IntegrationCatalogueBuilder
{
    public static IReadOnlyList<IntegrationCard> Build(IntegrationFacts facts) =>
    [
        WhatsApp(facts),
        FromConnection("email", "Email", "Channels", facts.Email, "Settings, Connections, Email"),
        FromConnection("sms", "SMS", "Channels", facts.Sms, "Settings, Connections, SMS"),
        Teams(facts),
        File("google-drive", "Google Drive", facts.GoogleDrive),
        File("microsoft-files", "OneDrive and SharePoint", facts.Microsoft),
    ];

    private static IntegrationCard WhatsApp(IntegrationFacts facts)
    {
        var w = facts.WhatsApp;
        var checks = new List<IntegrationCheck>();
        if (w.Configured)
        {
            checks.Add(new IntegrationCheck("Messages from customers", w.LastAcceptedAt is not null,
                w.LastAcceptedAt is { } accepted ? $"The last message arrived {Ago(facts.Now, accepted)}." : "No customer message has arrived yet.", Blocking: false));
            checks.Add(new IntegrationCheck("Meta's calls are accepted", w.RefusedInLastDay == 0 || (w.LastAcceptedAt is { } a && w.LastReceiptAt is { } r && a >= r),
                w.RefusedInLastDay == 0 ? "No call from Meta was refused in the last day." : $"{w.RefusedInLastDay} call(s) from Meta were refused in the last day and none was accepted since. Check the app secret.", Blocking: true));
            checks.Add(new IntegrationCheck("IIP's public address is known", w.PublicAddressKnown,
                w.PublicAddressKnown ? "IIP knows the address Meta should send messages to." : "IIP has no public address set, so it cannot tell whether Meta is pointed at it.", Blocking: false));
        }

        var state = IntegrationCardRules.Decide(platformAvailable: true, connected: w.Configured, checks);
        var summary = state switch
        {
            IntegrationCardState.Connected => w.LastAcceptedAt is { } last ? $"Connected. The last customer message arrived {Ago(facts.Now, last)}." : "Connected. No customer message has arrived yet.",
            IntegrationCardState.NeedsAttention => "Connected, but a check is failing. The checks below say what to fix.",
            _ => "No WhatsApp number is connected for this organisation.",
        };
        return new IntegrationCard("whatsapp", "WhatsApp", "Channels", state, summary, checks, "Settings, Connections, WhatsApp");
    }

    private static IntegrationCard FromConnection(string key, string name, string group, ConnectionFacts c, string hint)
    {
        var checks = new List<IntegrationCheck>();
        if (c.Saved)
        {
            var passed = string.Equals(c.LastValidationStatus, "Success", StringComparison.OrdinalIgnoreCase);
            checks.Add(new IntegrationCheck("Last test with the provider", passed,
                passed ? "The provider accepted the saved settings." : "The provider did not accept the saved settings. Test the connection again."));
            checks.Add(new IntegrationCheck("Connection is switched on", c.Active, c.Active ? "In use." : "Saved but switched off."));
        }

        // Any organisation can connect its own provider, so the platform side is always available for these.
        var state = IntegrationCardRules.Decide(platformAvailable: true, connected: c.Saved || c.PlatformProviderLive, checks);
        var summary = state switch
        {
            IntegrationCardState.Connected => c.Saved ? $"Connected through {c.ProviderKey ?? "the saved provider"}." : "Connected through the provider the platform supplies.",
            IntegrationCardState.NeedsAttention => "Connected, but a check is failing. The checks below say what to fix.",
            IntegrationCardState.NotConnected => $"Not connected for this organisation. {hint}.",
            _ => $"{name} is not set up on this platform yet.",
        };
        return new IntegrationCard(key, name, group, state, summary, checks, hint);
    }

    private static IntegrationCard Teams(IntegrationFacts facts)
    {
        var state = IntegrationCardRules.Decide(facts.TeamsProviderAvailable, facts.TeamsProviderAvailable, []);
        return new IntegrationCard("teams", "Microsoft Teams", "Channels", state,
            facts.TeamsProviderAvailable ? "Alerts post to the Teams channel set for this platform." : "Teams alerts are not set up on this platform yet.",
            [], null);
    }

    private static IntegrationCard File(string key, string name, FileProviderFacts f)
    {
        var checks = new List<IntegrationCheck>();
        var state = IntegrationCardRules.Decide(f.Configured, f.Connected, checks);
        var summary = state switch
        {
            IntegrationCardState.Connected => $"Your account{(f.AccountEmail is null ? string.Empty : $" ({f.AccountEmail})")} is connected. This is your own connection, not the organisation's.",
            IntegrationCardState.NotConnected => "You have not connected your account. Connections belong to each person.",
            _ => f.Reason ?? $"{name} is not set up on this platform yet.",
        };
        return new IntegrationCard(key, name, "Files", state, summary, checks, "Settings, Integrations");
    }

    public static string Ago(DateTimeOffset now, DateTimeOffset then)
    {
        var span = now - then;
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        if (span.TotalMinutes < 2) return "just now";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} minutes ago";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours} hour{((int)span.TotalHours == 1 ? string.Empty : "s")} ago";
        return $"{(int)span.TotalDays} day{((int)span.TotalDays == 1 ? string.Empty : "s")} ago";
    }
}
