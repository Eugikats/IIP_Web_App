using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;

namespace InsuranceIntelligence.Application.Channels;

public sealed class ChannelQueryService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    IChannelReadRepository repository) : IChannelQueryService
{
    public async Task<IReadOnlyList<ChannelConversationRowDto>> GetOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewChannels, cancellationToken);

        var context = tenantContextAccessor.Current;
        var rows = await repository.GetOverviewAsync(context.TenantId, context.WorkspaceId, cancellationToken);

        return rows
            .Select(row => new ChannelConversationRowDto(
                row.Id,
                row.CustomerName,
                NormalizeChannel(row.Channel),
                NormalizeIntent(row.DetectedIntent),
                row.SuggestedRouting,
                row.AiConfidence,
                row.Status,
                row.AssignedTo,
                row.LastMessageSnippet,
                row.LastActivityAt.ToString("O")))
            .ToArray();
    }

    private static string NormalizeChannel(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            ChannelValues.WhatsApp => ChannelValues.WhatsApp,
            ChannelValues.WebChat => ChannelValues.WebChat,
            ChannelValues.Sms => ChannelValues.Sms,
            ChannelValues.Email => ChannelValues.Email,
            ChannelValues.CallCenter => ChannelValues.CallCenter,
            ChannelValues.Broker => ChannelValues.Broker,
            ChannelValues.WebsiteForm => ChannelValues.WebsiteForm,
            _ => ChannelValues.Email
        };

    private static string NormalizeIntent(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            ChannelDetectedIntentValues.ClaimFnol => ChannelDetectedIntentValues.ClaimFnol,
            ChannelDetectedIntentValues.QuoteRequest => ChannelDetectedIntentValues.QuoteRequest,
            ChannelDetectedIntentValues.Renewal => ChannelDetectedIntentValues.Renewal,
            ChannelDetectedIntentValues.Complaint => ChannelDetectedIntentValues.Complaint,
            ChannelDetectedIntentValues.PolicyInquiry => ChannelDetectedIntentValues.PolicyInquiry,
            ChannelDetectedIntentValues.DocumentUpload => ChannelDetectedIntentValues.DocumentUpload,
            ChannelDetectedIntentValues.PaymentInquiry => ChannelDetectedIntentValues.PaymentInquiry,
            ChannelDetectedIntentValues.GeneralService => ChannelDetectedIntentValues.GeneralService,
            _ => ChannelDetectedIntentValues.GeneralService
        };
}
