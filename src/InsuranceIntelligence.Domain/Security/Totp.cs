using System.Security.Cryptography;
using System.Text;

namespace InsuranceIntelligence.Domain.Security;

/// <summary>
/// Time-based one-time passwords (RFC 6238) and the base32 text they travel in (RFC 4648). Microsoft Authenticator
/// and Google Authenticator both implement exactly this, so one QR code works in either. Small enough to keep in
/// the domain with no package: HMAC-SHA1 over a 30 second counter, six digits.
/// </summary>
public static class Totp
{
    public const int StepSeconds = 30;
    public const int Digits = 6;

    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string NewSecret() => Base32Encode(RandomNumberGenerator.GetBytes(20));

    public static long StepAt(DateTimeOffset moment) => moment.ToUnixTimeSeconds() / StepSeconds;

    public static string Code(string base32Secret, long step)
    {
        var key = Base32Decode(base32Secret);
        var counter = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian) Array.Reverse(counter);
        var hash = HMACSHA1.HashData(key, counter);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % (int)Math.Pow(10, Digits)).ToString().PadLeft(Digits, '0');
    }

    /// <summary>
    /// The step the code belongs to when it is valid now, allowing one step either side for clock drift, or null.
    /// Steps at or before <paramref name="lastUsedStep"/> are refused, so a code works once.
    /// </summary>
    public static long? Verify(string base32Secret, string? submitted, DateTimeOffset now, long lastUsedStep)
    {
        var code = new string((submitted ?? string.Empty).Where(char.IsDigit).ToArray());
        if (code.Length != Digits) return null;
        var current = StepAt(now);
        for (var offset = -1L; offset <= 1; offset++)
        {
            var step = current + offset;
            if (step <= lastUsedStep) continue;
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Code(base32Secret, step)), Encoding.ASCII.GetBytes(code)))
            {
                return step;
            }
        }

        return null;
    }

    /// <summary>The address an authenticator app reads from the QR code.</summary>
    public static string ProvisioningUri(string issuer, string account, string base32Secret) =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}" +
        $"?secret={base32Secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";

    public static string Base32Encode(byte[] data)
    {
        var output = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bits = 0;
        foreach (var value in data)
        {
            buffer = (buffer << 8) | value;
            bits += 8;
            while (bits >= 5)
            {
                output.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0) output.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return output.ToString();
    }

    public static byte[] Base32Decode(string text)
    {
        var clean = new string(text.Where(c => c != '=' && !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        var output = new List<byte>(clean.Length * 5 / 8);
        var buffer = 0;
        var bits = 0;
        foreach (var c in clean)
        {
            var value = Alphabet.IndexOf(c);
            if (value < 0) throw new FormatException("Not a valid base32 secret.");
            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        return output.ToArray();
    }
}
