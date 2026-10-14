namespace InsuranceIntelligence.Domain.Customers;

/// <summary>
/// Compares phone numbers as people and systems actually write them. WhatsApp delivers the
/// sender as bare international digits ("256776678989"); an insurer's records hold the same
/// number as "+256 776 678989", "256776678989" or the local form "0776678989". All of those are
/// one number.
///
/// The local form is only accepted against the international form's national part (the digits
/// after the country code), never as a loose "same last nine digits" rule between two
/// international numbers, which would treat numbers in different countries as the same person.
/// </summary>
public static class PhoneNumbers
{
    public static bool SameNumber(string? first, string? second)
    {
        var a = Digits(first);
        var b = Digits(second);
        if (a.Length < 7 || b.Length < 7)
        {
            return false;
        }

        if (a == b)
        {
            return true;
        }

        // "0776678989" against "256776678989": the local trunk prefix 0 stands in for the
        // country code, so the national parts must match exactly.
        return LocalMatchesInternational(a, b) || LocalMatchesInternational(b, a);
    }

    // An international number carries at least a one-digit country code in front of the national
    // part, so it is always longer than the local form minus its trunk 0.
    private static bool LocalMatchesInternational(string local, string international) =>
        IsLocalForm(local)
        && !IsLocalForm(international)
        && international.Length >= 11
        && international.EndsWith(local[1..], StringComparison.Ordinal);

    /// <summary>
    /// The last nine digits, which is where a local number and its international form agree. Two numbers that
    /// <see cref="SameNumber"/> always share a tail, so numbers can be indexed by it and only candidates compared.
    /// </summary>
    public static string Tail(string digits) => digits.Length <= 9 ? digits : digits[^9..];

    public static string Digits(string? value) =>
        value is null ? string.Empty : new string(value.Where(char.IsDigit).ToArray());

    private static bool IsLocalForm(string digits) => digits.Length is 10 && digits[0] == '0';
}
