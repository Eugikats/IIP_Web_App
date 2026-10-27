namespace InsuranceIntelligence.Application.Organisations;

public enum SecurityStatus
{
    /// <summary>True of the product today.</summary>
    InPlace,

    /// <summary>Partly true: what exists, and what does not, are both said.</summary>
    Partly,

    /// <summary>Not offered today. Saying so is the point.</summary>
    NotOffered,
}

public sealed record SecurityStatement(string Key, string Title, string Statement, SecurityStatus Status);

/// <summary>
/// What IIP says about how an organisation's data is protected, in one place, for the set-up screen and the security pack. Every
/// line must stay true of the product, and a thing IIP does not do is listed as not offered rather than left out, because a bank's
/// technical team will ask. Change the product and this text together, and raise <see cref="Version"/> so organisations read it again.
/// It has not been reviewed by counsel.
/// </summary>
public static class SecurityStatements
{
    public const string Version = "2026-10-01";

    public static readonly IReadOnlyList<SecurityStatement> All =
    [
        new("separation", "Your data is kept apart from other organisations",
            "Every record carries the organisation and workspace that own it, and every read and write is limited to them. Automated tests fail if a new table could mix organisations. The separation is enforced by the application; the database does not yet add a second, independent lock (row-level security).",
            SecurityStatus.Partly),
        new("transit", "Encrypted in transit",
            "Browsers, the API and the services behind it talk over HTTPS with TLS.",
            SecurityStatus.InPlace),
        new("rest", "Encrypted at rest",
            "The Azure services that hold your data (database, storage, search) encrypt it at rest with Microsoft-managed keys. You cannot bring your own keys today.",
            SecurityStatus.Partly),
        new("secrets", "Connection credentials are protected",
            "WhatsApp and other provider credentials are encrypted before they are saved and are never shown back, even to an administrator. Application keys are moving to Azure Key Vault; some still sit in the application's own configuration today.",
            SecurityStatus.Partly),
        new("access", "People see only what their role allows",
            "Sign-in is through Microsoft Entra, with two-factor sign-in and passkeys available. What a person may do is checked on the server on every request, never only in the screen.",
            SecurityStatus.InPlace),
        new("humans", "The AI prepares and people decide",
            "The AI summarises, drafts and recommends. It never approves or rejects a claim, binds a policy, sets a premium or accuses anyone. Answers about cover come only from wordings you have approved, with the source shown.",
            SecurityStatus.InPlace),
        new("outreach", "Nothing is sent to a customer without your say-so",
            "A message to someone who has not written in needs a rule a person approved, the customer's recorded agreement, an approved WhatsApp template and sending hours. A STOP reply ends it at once, and every decision, sent or not, is logged with its reasons.",
            SecurityStatus.InPlace),
        new("audit", "Important actions are recorded",
            "Approvals, changes to rules, templates, connections and settings, and customer data actions are written to an audit trail that records what happened and who did it, without copying message text or documents. You cannot yet send it to your own monitoring system.",
            SecurityStatus.Partly),
        new("whatsapp", "WhatsApp messages pass through Meta",
            "Messages travel through Meta's business platform, which receives and processes them. They are not end-to-end encrypted to IIP. For sensitive papers, customers are sent a secure link instead of being asked to send them in the chat.",
            SecurityStatus.Partly),
        new("location", "Where your data is kept",
            "Data is held in Microsoft Azure regions outside Uganda today. A guarantee that data stays in one country is not offered; agree it with us before sending personal data if you need it.",
            SecurityStatus.NotOffered),
        new("leaving", "You can take your data and leave",
            "An administrator can export everything the organisation owns, without stored credentials, and can close the organisation, which deletes everything it owns. Closing cannot be undone.",
            SecurityStatus.InPlace),
        new("assurance", "Independent assurance",
            "IIP holds no security certification such as ISO 27001 or SOC 2 and has not yet had an independent penetration test. Microsoft's certifications cover Microsoft's platform, not our application.",
            SecurityStatus.NotOffered),
        new("availability", "Availability and recovery",
            "The environment is sized for demonstration and early pilots. There is no uptime commitment, no recovery time or recovery point commitment and no tested disaster recovery plan yet.",
            SecurityStatus.NotOffered),
        new("support", "Access by our staff",
            "Otic staff access to your data is not yet governed by a written, customer-approved support access procedure. Until it is, ask us before you send anything you would not want a support engineer to be able to see.",
            SecurityStatus.NotOffered),
    ];
}
