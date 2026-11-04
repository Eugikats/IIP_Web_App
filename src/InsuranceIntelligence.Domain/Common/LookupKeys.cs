namespace InsuranceIntelligence.Domain.Common;

/// <summary>
/// The short, comparable forms of a policy number, a phone number and an email that are stored beside a record so it can be found with an
/// index instead of by reading every record in the workspace (build-loop item 35). Each is deliberately a little coarser than the real match
/// rule, so an indexed lookup returns every record that could match and the caller's own exact rule then decides.
/// </summary>
public static class LookupKeys
{
    public static string? PolicyKey(string? policyNumber)
    {
        var text = policyNumber?.Trim();
        return string.IsNullOrEmpty(text) ? null : (text.Length > 120 ? text[..120] : text).ToUpperInvariant();
    }

    /// <summary>The last seven digits. Two numbers the platform treats as the same (with or without the country code or leading zero) always share these.</summary>
    public static string? PhoneKey(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length < 7 ? null : digits[^7..];
    }

    public static string? EmailKey(string? email)
    {
        var text = email?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(text) ? null : text.Length > 200 ? text[..200] : text;
    }

    public static string? TextKey(string? value, int max = 60)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) ? null : text.Length > max ? text[..max] : text;
    }
}
