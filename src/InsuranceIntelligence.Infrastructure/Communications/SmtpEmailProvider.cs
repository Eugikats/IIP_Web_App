using System.Net;
using System.Net.Mail;
using InsuranceIntelligence.Domain.Communications;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Infrastructure.Communications;

/// <summary>
/// Tenant-supplied SMTP settings — parsed defensively from the flat connection settings
/// dictionary, never throws on a malformed or missing field.
/// </summary>
public sealed record SmtpSettings(string Host, int Port, string Username, string Password, string FromAddress, bool UseSsl, int RequestTimeoutSeconds)
{
    public static bool TryParse(IReadOnlyDictionary<string, string> settings, out SmtpSettings? parsed, out string? error)
    {
        parsed = null;
        if (!settings.TryGetValue("host", out var host) || string.IsNullOrWhiteSpace(host))
        {
            error = "An SMTP host is required.";
            return false;
        }

        if (!settings.TryGetValue("port", out var portRaw) || !int.TryParse(portRaw, out var port) || port is <= 0 or > 65535)
        {
            error = "A valid SMTP port is required.";
            return false;
        }

        if (!settings.TryGetValue("username", out var username) || string.IsNullOrWhiteSpace(username))
        {
            error = "An SMTP username is required.";
            return false;
        }

        if (!settings.TryGetValue("password", out var password) || string.IsNullOrWhiteSpace(password))
        {
            error = "An SMTP password is required.";
            return false;
        }

        if (!settings.TryGetValue("fromAddress", out var fromAddress) || string.IsNullOrWhiteSpace(fromAddress))
        {
            error = "A from-address is required.";
            return false;
        }

        if (!MailAddress.TryCreate(fromAddress, out _))
        {
            error = "The from-address is not a valid email address.";
            return false;
        }

        var useSsl = !settings.TryGetValue("useSsl", out var sslRaw) || !bool.TryParse(sslRaw, out var parsedSsl) || parsedSsl;
        var timeoutSeconds = settings.TryGetValue("requestTimeoutSeconds", out var timeoutRaw) && int.TryParse(timeoutRaw, out var parsedTimeout)
            ? parsedTimeout
            : 20;

        parsed = new SmtpSettings(host.Trim(), port, username.Trim(), password, fromAddress.Trim(), useSsl, timeoutSeconds);
        error = null;
        return true;
    }
}

/// <summary>
/// Real outbound email adapter for any SMTP-speaking mail system — no Azure tenant, no Entra app
/// registration, just host/port/username/password (blueprint §7.2). Uses the built-in
/// System.Net.Mail.SmtpClient rather than adding a new package dependency (MailKit is the natural
/// upgrade path if this needs to scale — see docs/01-strategy-requirements-roadmap/
/// IIP_HACKATHON_READINESS_BLUEPRINT.md §7.2); sufficient for a first working connection-based
/// email provider.
/// </summary>
public sealed class SmtpEmailProvider(SmtpSettings settings, ILogger<SmtpEmailProvider> logger) : IEmailSendingProvider
{
    public async Task<CommunicationSendResult> SendEmailAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.Approved && !request.LowRisk)
        {
            return new CommunicationSendResult("ApprovalRequired", string.Empty, "Customer communication requires approval before sending.");
        }

        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.Username))
        {
            return new CommunicationSendResult("ConfigurationRequired", string.Empty, "Email provider is not configured.");
        }

        if (!MailAddress.TryCreate(request.Recipient, out var recipient))
        {
            return new CommunicationSendResult("InvalidRecipient", string.Empty, "The recipient is not a valid email address.");
        }

        if (string.IsNullOrWhiteSpace(request.MessageBody))
        {
            return new CommunicationSendResult("EmptyMessage", string.Empty, "The email body was empty.");
        }

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.UseSsl,
            Credentials = new NetworkCredential(settings.Username, settings.Password),
            Timeout = Math.Clamp(settings.RequestTimeoutSeconds, 5, 60) * 1000,
        };
        using var message = new MailMessage(settings.FromAddress, recipient.Address, "Insurance claim update", request.MessageBody);

        try
        {
            await client.SendMailAsync(message, cancellationToken);
            logger.LogInformation(
                "SMTP email dispatched. CaseId={CaseId} CorrelationId={CorrelationId}",
                request.CaseId, request.CorrelationId);
            // SmtpClient has no provider message id to report — the send either throws or succeeds.
            return new CommunicationSendResult("Sent", string.Empty, null);
        }
        catch (Exception exception) when (exception is SmtpException or SmtpFailedRecipientException or InvalidOperationException or OperationCanceledException)
        {
            logger.LogWarning(
                "SMTP email send failed safely. CaseId={CaseId} CorrelationId={CorrelationId} ErrorType={ErrorType}",
                request.CaseId, request.CorrelationId, exception.GetType().Name);
            return new CommunicationSendResult("ProviderError", string.Empty, "The mail server could not be reached or rejected the message. No email was sent.");
        }
    }
}

/// <summary>
/// Validates SMTP credentials by opening a real connection and completing AUTH LOGIN — never MAIL
/// FROM/RCPT TO/DATA — so nothing is ever sent or queued for delivery (blueprint §7.2). Hand-rolled
/// against the raw protocol rather than SmtpClient.Send(), because SmtpClient has no
/// "authenticate only" call: sending to a placeholder recipient risks a real mail server actually
/// accepting and queuing that message, which is not what "validate without sending" means.
/// </summary>
public sealed class SmtpConnectionValidator : InsuranceIntelligence.Domain.Integrations.IConnectionValidator
{
    public InsuranceIntelligence.Domain.Integrations.ConnectionType ConnectionType => InsuranceIntelligence.Domain.Integrations.ConnectionType.Email;
    public string ProviderKey => "Smtp";

    public async Task<InsuranceIntelligence.Domain.Integrations.ConnectionValidationResult> ValidateAsync(
        IReadOnlyDictionary<string, string> settings,
        CancellationToken cancellationToken = default)
    {
        if (!SmtpSettings.TryParse(settings, out var parsed, out var parseError) || parsed is null)
        {
            return new InsuranceIntelligence.Domain.Integrations.ConnectionValidationResult(false, parseError);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(parsed.RequestTimeoutSeconds, 5, 60)));

        try
        {
            using var tcpClient = new System.Net.Sockets.TcpClient();
            await tcpClient.ConnectAsync(parsed.Host, parsed.Port, timeout.Token);

            System.IO.Stream stream = tcpClient.GetStream();
            System.Net.Security.SslStream? sslStream = null;
            var implicitTls = parsed.Port == 465; // the one well-known implicit-TLS SMTP port; 587/25 use STARTTLS

            if (implicitTls && parsed.UseSsl)
            {
                sslStream = new System.Net.Security.SslStream(stream, leaveInnerStreamOpen: false);
                await sslStream.AuthenticateAsClientAsync(parsed.Host);
                stream = sslStream;
            }

            using var reader = new System.IO.StreamReader(stream, leaveOpen: true);
            using var writer = new System.IO.StreamWriter(stream, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };

            var greeting = await ReadResponseAsync(reader, timeout.Token);
            if (!greeting.StartsWith("220", StringComparison.Ordinal))
            {
                return new InsuranceIntelligence.Domain.Integrations.ConnectionValidationResult(false, "The mail server did not respond with a valid SMTP greeting.");
            }

            await writer.WriteLineAsync($"EHLO iip-connection-check");
            var ehloResponse = await ReadResponseAsync(reader, timeout.Token);
            if (!ehloResponse.StartsWith("250", StringComparison.Ordinal))
            {
                return new InsuranceIntelligence.Domain.Integrations.ConnectionValidationResult(false, "The mail server rejected the EHLO handshake.");
            }

            if (!implicitTls && parsed.UseSsl)
            {
                await writer.WriteLineAsync("STARTTLS");
                var startTlsResponse = await ReadResponseAsync(reader, timeout.Token);
                if (!startTlsResponse.StartsWith("220", StringComparison.Ordinal))
                {
                    return new InsuranceIntelligence.Domain.Integrations.ConnectionValidationResult(false, "The mail server does not support STARTTLS on this port.");
                }

                sslStream = new System.Net.Security.SslStream(stream, leaveInnerStreamOpen: false);
                await sslStream.AuthenticateAsClientAsync(parsed.Host);
                stream = sslStream;
                reader.Dispose();
                writer.Dispose();
                return await AuthenticateAsync(stream, parsed, timeout.Token, reEhlo: true);
            }

            return await AuthenticateOverExistingStreamAsync(reader, writer, parsed.Username, parsed.Password);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new InsuranceIntelligence.Domain.Integrations.ConnectionValidationResult(false, "The mail server did not respond in time.");
        }
        catch (Exception exception) when (exception is System.Net.Sockets.SocketException or System.Security.Authentication.AuthenticationException or System.IO.IOException)
        {
            return new InsuranceIntelligence.Domain.Integrations.ConnectionValidationResult(false, "Could not reach the mail server. Verify the host and port.");
        }
    }

    private static async Task<InsuranceIntelligence.Domain.Integrations.ConnectionValidationResult> AuthenticateAsync(
        System.IO.Stream stream, SmtpSettings parsed, CancellationToken cancellationToken, bool reEhlo)
    {
        using var reader = new System.IO.StreamReader(stream, leaveOpen: true);
        using var writer = new System.IO.StreamWriter(stream, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
        if (reEhlo)
        {
            await writer.WriteLineAsync("EHLO iip-connection-check");
            var ehloResponse = await ReadResponseAsync(reader, cancellationToken);
            if (!ehloResponse.StartsWith("250", StringComparison.Ordinal))
            {
                return new InsuranceIntelligence.Domain.Integrations.ConnectionValidationResult(false, "The mail server rejected the EHLO handshake after STARTTLS.");
            }
        }

        return await AuthenticateOverExistingStreamAsync(reader, writer, parsed.Username, parsed.Password);
    }

    private static async Task<InsuranceIntelligence.Domain.Integrations.ConnectionValidationResult> AuthenticateOverExistingStreamAsync(
        System.IO.StreamReader reader, System.IO.StreamWriter writer, string username, string password)
    {
        await writer.WriteLineAsync("AUTH LOGIN");
        var authResponse = await ReadResponseAsync(reader, CancellationToken.None);
        if (!authResponse.StartsWith("334", StringComparison.Ordinal))
        {
            return new InsuranceIntelligence.Domain.Integrations.ConnectionValidationResult(false, "The mail server does not support AUTH LOGIN.");
        }

        await writer.WriteLineAsync(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(username)));
        var userResponse = await ReadResponseAsync(reader, CancellationToken.None);
        if (!userResponse.StartsWith("334", StringComparison.Ordinal))
        {
            return new InsuranceIntelligence.Domain.Integrations.ConnectionValidationResult(false, "The mail server rejected the username.");
        }

        await writer.WriteLineAsync(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password)));
        var passResponse = await ReadResponseAsync(reader, CancellationToken.None);

        await writer.WriteLineAsync("QUIT");

        return passResponse.StartsWith("235", StringComparison.Ordinal)
            ? new InsuranceIntelligence.Domain.Integrations.ConnectionValidationResult(true, null)
            : new InsuranceIntelligence.Domain.Integrations.ConnectionValidationResult(false, "The mail server rejected these credentials. Verify the username and password.");
    }

    // A multi-line SMTP response ("250-..." continuation lines ending in "250 ...") only matters
    // for EHLO's capability list, which this validator never parses — reading one line at a time
    // and using only the final line's status code is sufficient for every check performed here.
    private static async Task<string> ReadResponseAsync(System.IO.StreamReader reader, CancellationToken cancellationToken)
    {
        string? line;
        string last = string.Empty;
        do
        {
            line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) break;
            last = line;
        } while (line.Length > 3 && line[3] == '-');
        return last;
    }
}
