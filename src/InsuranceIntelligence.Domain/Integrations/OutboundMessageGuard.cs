using System.Text.RegularExpressions;

namespace InsuranceIntelligence.Domain.Integrations;

public sealed record OutboundGuardResult(bool Allowed, string? Code, string? SafeReason)
{
    public static readonly OutboundGuardResult Pass = new(true, null, null);
}

/// <summary>
/// The last check before a message leaves the platform for a customer. It refuses only what is never right to send:
/// a storage address or key that would let a stranger open a file, a bearer token, or a request for a PIN, password or
/// one-time code. Ported from the outbound guard in CIP's WhatsApp relay (<c>ContentGuard</c>), keeping its secret and
/// credential rules.
///
/// CIP's guard also refuses approval and score wording, because its assistant never states a credit decision. IIP's
/// assistant replies from fixed wording and a person's approved decisions (a claim a person approved is correctly told
/// as approved), so a blanket wording block would stop true status messages. The human-decision boundary is enforced
/// where decisions are made, not by matching words here.
/// </summary>
public static class OutboundMessageGuard
{
    private static readonly Regex SecretOrStorageReference = new(
        @"([?&]sig=|\bAccountKey\b|\bSharedAccessSignature\b|\bBearer\s+[A-Za-z0-9\-_\.]{12,}|\bapi[_-]?key\b|\bBlobPath\b|\bContentHash\b|[a-z0-9]+\.blob\.core\.windows\.net|\bsk-[A-Za-z0-9]{16,})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Asking for a credential. Warning a customer never to share one ("never share your PIN") is allowed, so the
    // credential word has to follow a request verb addressed to the customer.
    private static readonly Regex CredentialRequest = new(
        @"(?<!(?:\bnever|\bnot|n't)\s+)\b(send|share|give|provide|enter|type|tell|reply with|confirm)\b[^.?!\n]{0,40}\b(your |the )?(PIN|password|passcode|CVV|full card number|one[- ]time (password|code|pin)|OTP)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static OutboundGuardResult Check(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return OutboundGuardResult.Pass;

        if (SecretOrStorageReference.IsMatch(text))
        {
            return new OutboundGuardResult(false, "secret_or_storage_reference", "The message contained an internal address or key and was not sent.");
        }

        if (CredentialRequest.IsMatch(text))
        {
            return new OutboundGuardResult(false, "credential_request", "The message asked for a PIN, password or one-time code and was not sent.");
        }

        return OutboundGuardResult.Pass;
    }
}
