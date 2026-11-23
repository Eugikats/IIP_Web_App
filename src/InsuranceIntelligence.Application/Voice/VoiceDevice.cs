using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using InsuranceIntelligence.Application.Core;

namespace InsuranceIntelligence.Application.Voice;

public sealed record DeviceCheckGrant(string Token, DateTimeOffset ExpiresAt);

/// <summary>
/// What lets a person's phone do two small things while the app is closed: ask whether there is a nudge, and confirm
/// that "Hey Kato" was really said. The portal's own access token lives only in an open tab, so a background job on
/// the phone has nothing to sign in with. This is a separate, narrow credential instead:
/// it is random and opaque, only its hash is kept here, it can do nothing but those two checks (no data is read with
/// it other than the next nudge line), it is revoked on sign-out and expires fourteen days after the app last renewed it.
/// A phone keeps at most three.
/// </summary>
public interface IDeviceChecks
{
    DeviceCheckGrant Issue(TenantContext person);

    /// <summary>The person the token belongs to, renewing it, or null when it is unknown, revoked or out of date.</summary>
    TenantContext? Resolve(string? token);

    void Revoke(string? token);

    /// <summary>Counts one use of the named check against the token's minute, or refuses it.</summary>
    bool TryUse(string token, string check, int perMinute);
}

/// <summary>
/// Kept in memory per API instance: a restart or a second instance does not know a token and the phone's next check
/// is refused, which is safe; the app renews it the next time it is opened.
/// </summary>
public sealed class DeviceChecks(TimeProvider? clock = null) : IDeviceChecks
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(14);
    private const int MaxPerPerson = 3;

    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private readonly object gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, List<DateTimeOffset>> uses = new(StringComparer.Ordinal);

    public DeviceCheckGrant Issue(TenantContext person)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var now = time.GetUtcNow();
        lock (gate)
        {
            Prune(now);
            var mine = entries.Where(pair => Same(pair.Value.Person, person)).OrderBy(pair => pair.Value.IssuedAt).ToList();
            foreach (var old in mine.Take(Math.Max(0, mine.Count - (MaxPerPerson - 1))))
            {
                entries.Remove(old.Key);
            }

            entries[Hash(raw)] = new Entry(person, now, now + Lifetime);
        }

        return new DeviceCheckGrant(raw, now + Lifetime);
    }

    public TenantContext? Resolve(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128)
        {
            return null;
        }

        var key = Hash(token);
        var now = time.GetUtcNow();
        lock (gate)
        {
            if (!entries.TryGetValue(key, out var entry))
            {
                return null;
            }

            if (entry.ExpiresAt <= now)
            {
                entries.Remove(key);
                return null;
            }

            entries[key] = entry with { ExpiresAt = now + Lifetime };
            return entry.Person;
        }
    }

    public void Revoke(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        lock (gate)
        {
            entries.Remove(Hash(token));
        }
    }

    public bool TryUse(string token, string check, int perMinute)
    {
        var key = check + "|" + Hash(token);
        var now = time.GetUtcNow();
        var window = uses.GetOrAdd(key, _ => []);
        lock (window)
        {
            window.RemoveAll(at => now - at > TimeSpan.FromMinutes(1));
            if (window.Count >= perMinute)
            {
                return false;
            }

            window.Add(now);
            return true;
        }
    }

    private void Prune(DateTimeOffset now)
    {
        foreach (var expired in entries.Where(pair => pair.Value.ExpiresAt <= now).Select(pair => pair.Key).ToList())
        {
            entries.Remove(expired);
        }
    }

    private static bool Same(TenantContext a, TenantContext b) =>
        string.Equals(a.TenantId, b.TenantId, StringComparison.Ordinal) && string.Equals(a.UserId, b.UserId, StringComparison.OrdinalIgnoreCase);

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private sealed record Entry(TenantContext Person, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt);
}

public sealed record WakeCheck(bool Wake, string Heard);

/// <summary>
/// The second look at "Hey Kato". The phone's own listener is set loose on purpose, so it also fires on near misses;
/// it sends just those two or three seconds here, they are written down, and the phone only opens if the words really
/// were the wake phrase. Nothing is kept.
/// </summary>
public interface IWakeChecker
{
    bool IsConfigured { get; }

    /// <summary>Null when the speech service could not be reached or refused (the phone then decides alone).</summary>
    Task<WakeCheck?> CheckAsync(byte[] wav, CancellationToken cancellationToken);
}

/// <summary>What counts as the wake phrase once the audio has been written down.</summary>
public static class WakePhrase
{
    // "Hey", "hay", "ay", "hi", "okay", "hello" or "yo", then Kato (or how a speech service tends to write it).
    private const string Name = @"(?:kato|cato|katto|kahto|kado|cado|kat\s?o|cat\s?o)";

    private static readonly Regex Greeting = new(
        @"(?:^|[\s,.!?])(?:\w*(?:ay|ey|ei|eh)|hi|a|ok|okay|hello|yo)\b[\s,.!]*" + Name + @"\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

    // The phone heard the whole phrase but the recording lost or mangled the greeting: a lone "Kato." (or one odd word then
    // Kato) and nothing else said.
    private static readonly Regex Alone = new(
        @"^\W*(?:[a-z']+[\s,.!]+)?" + Name + @"\W*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

    public static bool Matches(string? heard)
    {
        if (string.IsNullOrWhiteSpace(heard))
        {
            return false;
        }

        var text = heard.Trim();
        try
        {
            return Greeting.IsMatch(text) || Alone.IsMatch(text);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
