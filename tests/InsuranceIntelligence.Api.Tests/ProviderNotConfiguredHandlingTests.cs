using System.Text.Json;
using InsuranceIntelligence.Api.Azure;
using InsuranceIntelligence.Api.Local;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// Covers WO-04: a missing provider must produce an honest, structured, capability-identifying
/// response — never a raw provider error and never a silent fallback. Two things are proven:
/// (1) the exception boundary converts ProviderNotConfiguredException into the documented shape;
/// (2) every NotConfigured provider actually throws that typed exception with a real capability
/// name, not a plain untyped exception the boundary would miss.
/// </summary>
public sealed class ProviderNotConfiguredHandlingTests
{
    [Fact]
    public async Task Handler_ReturnsServiceUnavailableWithCapability_ForProviderNotConfiguredException()
    {
        var handler = new ProviderNotConfiguredExceptionHandler(NullLogger<ProviderNotConfiguredExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(
            context,
            new ProviderNotConfiguredException("DocumentStorage", "Document storage provider is not configured."),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        var root = document.RootElement;

        Assert.Equal("DocumentStorage", root.GetProperty("capability").GetString());
        Assert.Equal(503, root.GetProperty("status").GetInt32());
        // The response must never carry the provider class name, a stack trace, or any other
        // internal detail beyond the stable capability identifier and a human-actionable message.
        var raw = root.GetRawText();
        Assert.DoesNotContain("NotConfiguredDocumentStorageProvider", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", raw, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handler_DoesNotHandle_UnrelatedExceptionTypes()
    {
        var handler = new ProviderNotConfiguredExceptionHandler(NullLogger<ProviderNotConfiguredExceptionHandler>.Instance);
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("Some other failure."), CancellationToken.None);

        Assert.False(handled);
        // Falling through to the default handler is the correct behaviour for anything that is
        // not a missing-provider failure — this handler must not swallow other exception types.
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    public static IEnumerable<object[]> NotConfiguredProviderCalls()
    {
        yield return new object[]
        {
            "DocumentStorage",
            (Func<Task>)(() => new NotConfiguredDocumentStorageProvider().StoreAsync(null!, CancellationToken.None)),
        };
        yield return new object[]
        {
            "DocumentProcessing",
            (Func<Task>)(() => new NotConfiguredDocumentProcessingProvider().ProcessAsync(null!, CancellationToken.None)),
        };
        yield return new object[]
        {
            "KnowledgeStorage",
            (Func<Task>)(() => new NotConfiguredKnowledgeStorageProvider().StoreAsync(null!, CancellationToken.None)),
        };
        yield return new object[]
        {
            "KnowledgeIndexing",
            (Func<Task>)(() => new NotConfiguredKnowledgeIndexProvider().IndexAsync(null!, CancellationToken.None)),
        };
        yield return new object[]
        {
            "CustomerCommunication",
            (Func<Task>)(() => new NotConfiguredCommunicationProvider().SendAsync(null!, "user-1", null, CancellationToken.None)),
        };
        yield return new object[]
        {
            "CustomerUpdateDrafting",
            (Func<Task>)(() => new NotConfiguredCustomerUpdateDraftingProvider().DraftAsync(null!, null!, default, default, "user-1", "corr-1", CancellationToken.None)),
        };
        yield return new object[]
        {
            "EvidencePackStorage",
            (Func<Task>)(() => new NotConfiguredEvidencePackStorageProvider().StoreAsync(null!, CancellationToken.None)),
        };
        yield return new object[]
        {
            "ChatCompletion",
            (Func<Task>)(() => new NotConfiguredChatCompletionProvider().CompleteAsync(null!, CancellationToken.None)),
        };
        yield return new object[]
        {
            "MessageQueue",
            (Func<Task>)(() => new NotConfiguredMessageQueueDispatcher().DispatchAsync(null!, CancellationToken.None)),
        };
    }

    [Theory]
    [MemberData(nameof(NotConfiguredProviderCalls))]
    public async Task NotConfiguredProvider_ThrowsTypedExceptionWithCapability(string expectedCapability, Func<Task> call)
    {
        var exception = await Assert.ThrowsAsync<ProviderNotConfiguredException>(call);

        Assert.Equal(expectedCapability, exception.Capability);
    }
}
