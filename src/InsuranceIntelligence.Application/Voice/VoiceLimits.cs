using System.Collections.Concurrent;

namespace InsuranceIntelligence.Application.Voice;

/// <summary>What a person may do with voice: how often, for how long in a day, and how many calls at once.</summary>
public sealed class VoiceLimitOptions
{
    public int TranscriptionsPerMinute { get; init; } = 20;
    public int AudioSecondsPerDay { get; init; } = 30 * 60;
    public int MaxConcurrentSessions { get; init; } = 2;
    public int MaxSessionMinutes { get; init; } = 15;
}

public sealed record VoiceLimitDecision(bool Allowed, string Code, string Message)
{
    public static readonly VoiceLimitDecision Ok = new(true, "ok", string.Empty);
}

public interface IVoiceLimits
{
    /// <summary>Counts one transcription of the given length against the person's minute and day, or refuses it.</summary>
    VoiceLimitDecision TryUseTranscription(string tenantId, string userId, double audioSeconds);

    /// <summary>Counts the length of a live call against the day when it ends.</summary>
    void AddCallSeconds(string tenantId, string userId, double seconds);

    /// <summary>A place for one live call, or null (with the reason) when the person already has the most allowed.</summary>
    VoiceSessionLease? TryOpenSession(string tenantId, string userId, out VoiceLimitDecision decision);

    TimeSpan MaxSessionLength { get; }
}

/// <summary>Held for the life of a live call. Disposing it frees the place.</summary>
public sealed class VoiceSessionLease(Action release) : IDisposable
{
    private int released;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref released, 1) == 0)
        {
            release();
        }
    }
}

/// <summary>
/// In-memory limits, kept per API instance. They protect a tenant's speech spend and the Voice Live
/// resource from a loop or a stuck client; they are not a billing record (usage is recorded separately).
/// With more than one API instance each instance counts for itself, so the real ceiling is the
/// limit times the instance count.
/// </summary>
public sealed class VoiceLimits(VoiceLimitOptions options, TimeProvider? clock = null) : IVoiceLimits
{
    private const string DailyMessage = "You have used today's voice minutes. Type instead, or ask an administrator to raise the limit.";

    private readonly ConcurrentDictionary<string, Person> people = new();

    public TimeSpan MaxSessionLength => TimeSpan.FromMinutes(options.MaxSessionMinutes);

    public VoiceLimitDecision TryUseTranscription(string tenantId, string userId, double audioSeconds)
    {
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var person = For(tenantId, userId);
        lock (person)
        {
            person.RollDay(now);
            person.Recent.RemoveAll(at => now - at > TimeSpan.FromMinutes(1));
            if (person.Recent.Count >= options.TranscriptionsPerMinute)
            {
                return new VoiceLimitDecision(false, "rate_limited", "That is a lot of voice in a short time. Wait a minute, then try again.");
            }

            if (person.SecondsToday + audioSeconds > options.AudioSecondsPerDay)
            {
                return new VoiceLimitDecision(false, "daily_limit", DailyMessage);
            }

            person.Recent.Add(now);
            person.SecondsToday += audioSeconds;
            return VoiceLimitDecision.Ok;
        }
    }

    public void AddCallSeconds(string tenantId, string userId, double seconds)
    {
        var person = For(tenantId, userId);
        lock (person)
        {
            person.RollDay((clock ?? TimeProvider.System).GetUtcNow());
            person.SecondsToday += Math.Max(0, seconds);
        }
    }

    public VoiceSessionLease? TryOpenSession(string tenantId, string userId, out VoiceLimitDecision decision)
    {
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var person = For(tenantId, userId);
        lock (person)
        {
            person.RollDay(now);
            if (person.SecondsToday >= options.AudioSecondsPerDay)
            {
                decision = new VoiceLimitDecision(false, "daily_limit", DailyMessage);
                return null;
            }

            if (person.OpenSessions >= options.MaxConcurrentSessions)
            {
                decision = new VoiceLimitDecision(false, "too_many_calls", "You already have live conversations open. End one before starting another.");
                return null;
            }

            person.OpenSessions++;
        }

        decision = VoiceLimitDecision.Ok;
        return new VoiceSessionLease(() =>
        {
            lock (person)
            {
                person.OpenSessions = Math.Max(0, person.OpenSessions - 1);
            }
        });
    }

    /// <summary>Length of a recording in seconds: exact for 16-bit mono 16 kHz WAV, a cautious estimate for anything else.</summary>
    public static double EstimateSeconds(long bytes, string? contentType) =>
        contentType?.Contains("wav", StringComparison.OrdinalIgnoreCase) == true
            ? Math.Max(0, bytes - 44) / 32_000.0
            : bytes / 4_000.0;

    private Person For(string tenantId, string userId) => people.GetOrAdd($"{tenantId}|{userId}", _ => new Person());

    private sealed class Person
    {
        private DateOnly day;

        public List<DateTimeOffset> Recent { get; } = [];

        public double SecondsToday { get; set; }

        public int OpenSessions { get; set; }

        public void RollDay(DateTimeOffset now)
        {
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            if (today == day)
            {
                return;
            }

            day = today;
            SecondsToday = 0;
        }
    }
}
