using System.Net.WebSockets;
using System.Text;
using Azure.Core;
using InsuranceIntelligence.Api.Azure;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Voice;
using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Cases;

namespace InsuranceIntelligence.Api.Voice;

/// <summary>The control link to Voice Live. Signed in as the API's own managed identity: no key is held anywhere.</summary>
public sealed class VoiceLiveWebSocketTransport(VoiceLiveOptions options, TokenCredential credential) : IVoiceLiveTransport
{
    private const string Scope = "https://ai.azure.com/.default";

    private readonly ClientWebSocket socket = new();
    private readonly SemaphoreSlim sendGate = new(1, 1);

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var token = await credential.GetTokenAsync(new TokenRequestContext([Scope]), cancellationToken);
        socket.Options.SetRequestHeader("Authorization", "Bearer " + token.Token);
        var host = new Uri(options.Endpoint!).Host;
        var uri = new Uri($"wss://{host}/voice-live/realtime/calls?api-version={Uri.EscapeDataString(options.ApiVersion)}&model={Uri.EscapeDataString(options.Model)}");
        await socket.ConnectAsync(uri, cancellationToken);
    }

    public async Task SendAsync(string json, CancellationToken cancellationToken)
    {
        // A socket takes one send at a time; a lock message and a tool result can arrive together.
        await sendGate.WaitAsync(cancellationToken);
        try
        {
            await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
        }
        finally
        {
            sendGate.Release();
        }
    }

    public async Task<string?> ReceiveAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();
        while (true)
        {
            if (socket.State is not (WebSocketState.Open or WebSocketState.CloseSent))
            {
                return null;
            }

            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            message.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                return Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            }

            if (message.Length > 4 * 1024 * 1024)
            {
                return null;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (socket.State == WebSocketState.Open)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "ended", timeout.Token);
            }
        }
        catch (Exception)
        {
            // Closing a link that is already gone.
        }

        socket.Dispose();
        sendGate.Dispose();
    }
}

public sealed class VoiceLiveWebSocketTransportFactory(VoiceLiveOptions options) : IVoiceLiveTransportFactory
{
    public IVoiceLiveTransport Create() => new VoiceLiveWebSocketTransport(options, AzureProviderSupport.Credential());
}

/// <summary>Gives a call what it needs for one use, as the person who opened it, in its own scope.</summary>
public sealed class VoiceScopeFactory(IServiceScopeFactory scopes) : IVoiceScopeFactory
{
    public IVoiceScope Create(TenantContext context)
    {
        var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().SetCurrent(context);
        return new Scope(scope);
    }

    private sealed class Scope(IServiceScope inner) : IVoiceScope
    {
        public ICommandCenterConversationService Conversations => inner.ServiceProvider.GetRequiredService<ICommandCenterConversationService>();

        public IAgentBehaviourProfileRepository Profiles => inner.ServiceProvider.GetRequiredService<IAgentBehaviourProfileRepository>();

        public IAuditRepository Audit => inner.ServiceProvider.GetRequiredService<IAuditRepository>();

        public void Dispose() => inner.Dispose();
    }
}
