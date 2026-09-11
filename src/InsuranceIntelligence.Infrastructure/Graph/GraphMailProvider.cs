using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Infrastructure.Graph;

public sealed class GraphMailProvider(InfrastructureOptions options, IGraphMailClient client) : IGraphMailProvider
{
    public async Task<IReadOnlyCollection<InboundEmailMessage>> PullMessagesAsync(MailboxConnection mailbox, CancellationToken cancellationToken = default)
    {
        if (!options.EmailProvider.Equals("Graph", StringComparison.OrdinalIgnoreCase) || !mailbox.IngestionEnabled)
        {
            return Array.Empty<InboundEmailMessage>();
        }

        var messages = await client.ReadMessagesAsync(mailbox, cancellationToken);
        return messages
            .Select(message => ToInbound(mailbox, message))
            .ToArray();
    }

    public Task CompleteMessageAsync(MailboxConnection mailbox, InboundEmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!options.EmailProvider.Equals("Graph", StringComparison.OrdinalIgnoreCase)
            || !options.GraphMail.MoveProcessedMessages
            || string.IsNullOrWhiteSpace(message.ExternalMessageId))
        {
            return Task.CompletedTask;
        }

        var folderName = string.IsNullOrWhiteSpace(mailbox.ProcessedFolderName)
            ? options.GraphMail.ProcessedFolderName
            : mailbox.ProcessedFolderName;
        return client.MoveMessageAsync(mailbox.MailboxAddress, message.ExternalMessageId, folderName, cancellationToken);
    }

    private static InboundEmailMessage ToInbound(MailboxConnection mailbox, GraphMailboxMessage message)
    {
        var messageId = StableGuid($"{mailbox.Scope.TenantId}:{mailbox.MailboxAddress}:{message.ExternalMessageId}");
        return new InboundEmailMessage
        {
            MessageId = messageId,
            Scope = mailbox.Scope,
            MailboxConnectionId = mailbox.MailboxConnectionId,
            ExternalMessageId = message.ExternalMessageId,
            InternetMessageId = message.InternetMessageId,
            Sender = message.Sender,
            Subject = message.Subject,
            BodyPreview = SafePreview(message.BodyPreview),
            ReceivedAt = message.ReceivedAt,
            ProcessingStatus = EmailProcessingStatus.Received,
            Attachments = message.Attachments
                .Where(attachment => !attachment.IsInline)
                .Select(attachment => new EmailAttachment(
                    StableGuid($"{message.ExternalMessageId}:{attachment.ExternalAttachmentId}"),
                    messageId,
                    attachment.FileName,
                    attachment.ContentType,
                    attachment.SizeBytes,
                    attachment.ExternalAttachmentId))
                .ToArray()
        };
    }

    private static Guid StableGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        return new Guid(bytes);
    }

    private static string SafePreview(string value)
    {
        var sanitized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return sanitized.Length <= 280 ? sanitized : sanitized[..280];
    }
}

// Both ICommunicationSendingProvider implementations now purely delegate SendEmailAsync and
// SendSmsAsync to injected, tenant-aware providers (TenantAwareEmailSendingProvider /
// TenantAwareSmsProvider) — the real email/SMS mode selection lives entirely in those resolvers
// and their factories now. Kept as two distinct classes (rather than collapsed into one) only to
// avoid an unrelated, wider test-file rewrite in this change; they are behaviorally identical.
// See docs/01-strategy-requirements-roadmap/IIP_HACKATHON_READINESS_BLUEPRINT.md §14 Phase 1.
public sealed class GraphCommunicationSendingProvider(
    IEmailSendingProvider emailProvider,
    ISmsProvider smsProvider) : ICommunicationSendingProvider
{
    // Previously inline Graph-only logic, hardcoded to this class — extracted to
    // GraphEmailSendingProvider so email, like SMS, resolves through the Connection model (a
    // tenant-configured SMTP connection first, then this Graph fallback, then Not configured).
    public Task<CommunicationSendResult> SendEmailAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default) =>
        emailProvider.SendEmailAsync(request, cancellationToken);

    // Previously hardcoded to ConfigurationRequired regardless of SmsProvider/IntegrationConnection
    // state — a real bug: CommunicationProvider ("GraphEmail"/"Disabled") only ever governed email
    // mode, but SMS has its own independent SmsProvider/Connection resolution (TenantAwareSmsProvider)
    // that this method never reached. Delegating fixes it without touching that resolution at all.
    public Task<CommunicationSendResult> SendSmsAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default) =>
        smsProvider.SendAsync(request, cancellationToken);
}

/// <summary>
/// The real Microsoft Graph mail sender, extracted unchanged from GraphCommunicationSendingProvider
/// so it can be TenantAwareEmailSendingProvider's env-var fallback (blueprint §7.2) exactly the way
/// AfricasTalkingSmsProvider is TenantAwareSmsProvider's. Requires Entra app registration and a
/// configured default mailbox — that requirement, and the CommunicationProvider="GraphEmail" gate,
/// are unchanged from before this extraction.
/// </summary>
public sealed class GraphEmailSendingProvider(InfrastructureOptions options, IGraphMailClient client) : IEmailSendingProvider
{
    public async Task<CommunicationSendResult> SendEmailAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.Approved && !request.LowRisk)
        {
            return new CommunicationSendResult("ApprovalRequired", string.Empty, "Customer communication requires approval before sending.");
        }

        if (!options.CommunicationProvider.Equals("GraphEmail", StringComparison.OrdinalIgnoreCase))
        {
            return new CommunicationSendResult("ConfigurationRequired", string.Empty, "Selected communication provider is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.GraphMail.DefaultMailboxAddress))
        {
            return new CommunicationSendResult("ConfigurationRequired", string.Empty, "GraphEmail mode requires a configured sender mailbox.");
        }

        try
        {
            var result = await client.SendMailAsync(
                new GraphMailSendRequest(
                    options.GraphMail.DefaultMailboxAddress,
                    request.Recipient,
                    "Insurance claim update",
                    request.MessageBody,
                    SaveToSentItems: true),
                cancellationToken);
            return new CommunicationSendResult("Sent", result.ProviderMessageId, null);
        }
        catch (InvalidOperationException)
        {
            return new CommunicationSendResult("ConfigurationRequired", string.Empty, SafeError());
        }
        catch (HttpRequestException)
        {
            return new CommunicationSendResult("Failed", string.Empty, "Microsoft Graph mail send failed. Verify mailbox access and Graph app permissions.");
        }
    }

    private static string SafeError() =>
        "Microsoft Graph mail provider configuration is incomplete. Check mailbox access, identity, and Graph app permissions.";
}

/// <summary>Honest not-configured fallback for email — mirrors DisabledSmsProvider.</summary>
public sealed class DisabledEmailSendingProvider : IEmailSendingProvider
{
    public Task<CommunicationSendResult> SendEmailAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CommunicationSendResult("ConfigurationRequired", string.Empty, "Email sending provider is not configured."));
}

public sealed class DisabledEmailIngestionProvider : IEmailIngestionProvider
{
    public Task<InboundEmailMessage> CreateTestMessageAsync(MailboxConnection mailbox, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Synthetic email ingestion is disabled when a live email provider is selected.");
}

public sealed class DisabledWebhookSubscriptionProvider : IWebhookSubscriptionProvider
{
    public Task<string> ValidateAsync(string payload, CancellationToken cancellationToken = default) =>
        Task.FromResult("WebhookSubscriptionProviderNotConfigured");
}

public sealed class DisabledSmsProvider : ISmsProvider
{
    public Task<CommunicationSendResult> SendAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CommunicationSendResult("ConfigurationRequired", string.Empty, "SMS provider is not configured."));
}

public sealed class DisabledCommunicationSendingProvider(IEmailSendingProvider emailProvider, ISmsProvider smsProvider) : ICommunicationSendingProvider
{
    // "Disabled" describes the deployment's CommunicationProvider env var only — email and SMS now
    // each follow their own independent Connection-model resolution regardless of it. See
    // GraphCommunicationSendingProvider for the same delegation and its rationale.
    public Task<CommunicationSendResult> SendEmailAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default) =>
        emailProvider.SendEmailAsync(request, cancellationToken);

    public Task<CommunicationSendResult> SendSmsAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default) =>
        smsProvider.SendAsync(request, cancellationToken);
}

public sealed class DisabledTeamsNotificationProvider : ITeamsNotificationProvider
{
    public Task<string> SendAsync(TeamsNotification notification, CancellationToken cancellationToken = default) =>
        Task.FromResult("ConfigurationRequired");
}

public sealed class MicrosoftGraphMailClient(HttpClient httpClient, InfrastructureOptions options) : IGraphMailClient
{
    private readonly string graphBaseUrl = options.GraphMail.GraphBaseUrl.TrimEnd('/');

    public async Task<IReadOnlyCollection<GraphMailboxMessage>> ReadMessagesAsync(MailboxConnection mailbox, CancellationToken cancellationToken = default)
    {
        EnsureClientCredentialConfigured();
        var token = await GetTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildReadMessagesUri(mailbox.MailboxAddress));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<GraphMessagesResponse>(cancellationToken: cancellationToken)
            ?? new GraphMessagesResponse([]);

        return payload.Value.Select(ToGraphMessage).ToArray();
    }

    public async Task<GraphAttachmentContent> DownloadAttachmentAsync(
        string mailboxAddress,
        string externalMessageId,
        string externalAttachmentId,
        CancellationToken cancellationToken = default)
    {
        EnsureClientCredentialConfigured();
        var token = await GetTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{graphBaseUrl}/users/{Escape(mailboxAddress)}/messages/{Escape(externalMessageId)}/attachments/{Escape(externalAttachmentId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var attachment = await response.Content.ReadFromJsonAsync<GraphAttachmentContentResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Graph attachment response was empty.");
        if (string.IsNullOrWhiteSpace(attachment.ContentBytes))
        {
            throw new InvalidOperationException("Graph attachment response did not include file content.");
        }

        byte[] contentBytes;
        try
        {
            contentBytes = Convert.FromBase64String(attachment.ContentBytes);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Graph attachment content was not a valid file payload.", ex);
        }

        return new GraphAttachmentContent(
            string.IsNullOrWhiteSpace(attachment.Name) ? "attachment" : attachment.Name,
            string.IsNullOrWhiteSpace(attachment.ContentType) ? "application/octet-stream" : attachment.ContentType,
            attachment.Size > 0 ? attachment.Size : contentBytes.LongLength,
            contentBytes);
    }

    public async Task MoveMessageAsync(string mailboxAddress, string externalMessageId, string folderName, CancellationToken cancellationToken = default)
    {
        EnsureClientCredentialConfigured();
        var token = await GetTokenAsync(cancellationToken);
        var folderId = await GetOrCreateFolderAsync(mailboxAddress, folderName, token, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{graphBaseUrl}/users/{Escape(mailboxAddress)}/messages/{Escape(externalMessageId)}/move")
        {
            Content = JsonContent.Create(new { destinationId = folderId })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<GraphMailSendResult> SendMailAsync(GraphMailSendRequest sendRequest, CancellationToken cancellationToken = default)
    {
        EnsureClientCredentialConfigured();
        var token = await GetTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{graphBaseUrl}/users/{Escape(sendRequest.MailboxAddress)}/sendMail")
        {
            Content = JsonContent.Create(new
            {
                message = new
                {
                    subject = sendRequest.Subject,
                    body = new { contentType = "Text", content = sendRequest.Body },
                    toRecipients = new[]
                    {
                        new { emailAddress = new { address = sendRequest.Recipient } }
                    }
                },
                saveToSentItems = sendRequest.SaveToSentItems
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return new GraphMailSendResult($"graph-send-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}");
    }

    private Uri BuildReadMessagesUri(string mailboxAddress)
    {
        var query = new List<string>
        {
            "$top=" + Math.Clamp(options.GraphMail.MaxMessagesPerPull, 1, 50),
            "$select=id,internetMessageId,subject,from,receivedDateTime,bodyPreview,hasAttachments,isRead",
            "$expand=attachments($select=id,name,contentType,size,isInline)",
            "$orderby=receivedDateTime desc"
        };
        if (options.GraphMail.ProcessUnreadOnly)
        {
            query.Add("$filter=isRead eq false");
        }

        return new Uri($"{graphBaseUrl}/users/{Escape(mailboxAddress)}/mailFolders/inbox/messages?{string.Join('&', query)}");
    }

    private async Task<string> GetOrCreateFolderAsync(string mailboxAddress, string folderName, string token, CancellationToken cancellationToken)
    {
        var filter = Uri.EscapeDataString($"displayName eq '{folderName.Replace("'", "''")}'");
        using var listRequest = new HttpRequestMessage(HttpMethod.Get, $"{graphBaseUrl}/users/{Escape(mailboxAddress)}/mailFolders?$filter={filter}&$select=id,displayName");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var listResponse = await httpClient.SendAsync(listRequest, cancellationToken);
        listResponse.EnsureSuccessStatusCode();
        var folders = await listResponse.Content.ReadFromJsonAsync<GraphFoldersResponse>(cancellationToken: cancellationToken)
            ?? new GraphFoldersResponse([]);
        var existing = folders.Value.FirstOrDefault();
        if (existing is not null)
        {
            return existing.Id;
        }

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, $"{graphBaseUrl}/users/{Escape(mailboxAddress)}/mailFolders")
        {
            Content = JsonContent.Create(new { displayName = folderName })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var createResponse = await httpClient.SendAsync(createRequest, cancellationToken);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<GraphFolder>(cancellationToken: cancellationToken);
        return created?.Id ?? throw new InvalidOperationException("Graph mail processed folder could not be created.");
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = options.GraphMail.ClientId!,
            ["client_secret"] = options.GraphMail.ClientSecret!,
            ["scope"] = "https://graph.microsoft.com/.default",
            ["grant_type"] = "client_credentials"
        };
        using var response = await httpClient.PostAsync(
            $"https://login.microsoftonline.com/{Uri.EscapeDataString(options.GraphMail.TenantId!)}/oauth2/v2.0/token",
            new FormUrlEncodedContent(form),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken);
        return token?.AccessToken ?? throw new InvalidOperationException("Graph token response did not include an access token.");
    }

    private void EnsureClientCredentialConfigured()
    {
        if (options.GraphMail.UseManagedIdentity)
        {
            throw new InvalidOperationException("Graph managed identity authentication is not enabled for this provider. Configure GraphMail client credentials.");
        }

        if (string.IsNullOrWhiteSpace(options.GraphMail.TenantId)
            || string.IsNullOrWhiteSpace(options.GraphMail.ClientId)
            || string.IsNullOrWhiteSpace(options.GraphMail.ClientSecret))
        {
            throw new InvalidOperationException("Graph mail provider requires tenant ID, client ID, and a secret or certificate configured outside source code.");
        }
    }

    private static GraphMailboxMessage ToGraphMessage(GraphMessage message) =>
        new(
            message.Id,
            message.InternetMessageId,
            message.From?.EmailAddress?.Address ?? "unknown@example.invalid",
            string.IsNullOrWhiteSpace(message.Subject) ? "(no subject)" : message.Subject,
            message.BodyPreview ?? string.Empty,
            message.ReceivedDateTime ?? DateTimeOffset.UtcNow,
            (message.Attachments ?? []).Select(attachment => new GraphMailboxAttachment(
                attachment.Id,
                attachment.Name ?? "attachment",
                attachment.ContentType ?? "application/octet-stream",
                attachment.Size,
                attachment.IsInline)).ToArray());

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private sealed record TokenResponse([property: JsonPropertyName("access_token")] string AccessToken);
    private sealed record GraphMessagesResponse([property: JsonPropertyName("value")] IReadOnlyCollection<GraphMessage> Value);
    private sealed record GraphMessage(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("internetMessageId")] string? InternetMessageId,
        [property: JsonPropertyName("subject")] string? Subject,
        [property: JsonPropertyName("from")] GraphRecipient? From,
        [property: JsonPropertyName("receivedDateTime")] DateTimeOffset? ReceivedDateTime,
        [property: JsonPropertyName("bodyPreview")] string? BodyPreview,
        [property: JsonPropertyName("attachments")] IReadOnlyCollection<GraphAttachment>? Attachments);
    private sealed record GraphRecipient([property: JsonPropertyName("emailAddress")] GraphEmailAddress? EmailAddress);
    private sealed record GraphEmailAddress([property: JsonPropertyName("address")] string? Address);
    private sealed record GraphAttachment(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("contentType")] string? ContentType,
        [property: JsonPropertyName("size")] long Size,
        [property: JsonPropertyName("isInline")] bool IsInline);
    private sealed record GraphAttachmentContentResponse(
        [property: JsonPropertyName("@odata.type")] string? ODataType,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("contentType")] string? ContentType,
        [property: JsonPropertyName("size")] long Size,
        [property: JsonPropertyName("contentBytes")] string? ContentBytes);
    private sealed record GraphFoldersResponse([property: JsonPropertyName("value")] IReadOnlyCollection<GraphFolder> Value);
    private sealed record GraphFolder([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("displayName")] string DisplayName);
}
