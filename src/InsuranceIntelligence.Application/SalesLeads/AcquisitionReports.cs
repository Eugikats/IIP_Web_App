using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Acquisition;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.CustomerAssistant;

namespace InsuranceIntelligence.Application.SalesLeads;

/// <summary>Every conversation turn in the workspace, read only for the acquisition reports.</summary>
public interface IConversationActivityReader
{
    Task<IReadOnlyList<CustomerConversationTurn>> ListTurnsAsync(TenantScope scope, CancellationToken cancellationToken = default);
}

public interface IAcquisitionReportService
{
    Task<EscalationReportDto> EscalationsAsync(CancellationToken cancellationToken = default);

    Task<AgentOperationsDto> AgentOperationsAsync(CancellationToken cancellationToken = default);

    Task<ChannelIntelligenceDto> ChannelIntelligenceAsync(CancellationToken cancellationToken = default);
}

public sealed record EscalationRowDto(
    string EscalationId,
    string Customer,
    string Contact,
    string Type,
    string Status,
    string Priority,
    string? AssigneeName,
    DateTimeOffset RaisedAt,
    DateTimeOffset UpdatedAt,
    string? ConversationId,
    string? LeadId,
    bool? BankCustomer);

public sealed record EscalationReportDto(
    int Open,
    int HighPriority,
    int PendingCustomer,
    int NewToBank,
    int Unassigned,
    IReadOnlyList<EscalationRowDto> Rows);

public sealed record AgentOperationRowDto(
    string Key,
    string Name,
    string Purpose,
    string Kind,
    bool Enabled,
    string Health,
    int Runs,
    int Errors,
    DateTimeOffset? LastRunAt,
    DateTimeOffset? LastOkAt,
    decimal ErrorRatePercent,
    bool CanRunNow);

public sealed record AgentOperationsDto(int Total, int Live, int Errors, int NoRuns, IReadOnlyList<AgentOperationRowDto> Rows);

public sealed record ChannelRowDto(string Channel, int Interactions, int NewLeads, int Qualified, decimal? ConversionPercent);

public sealed record ChannelIntelligenceDto(
    int TotalInteractions,
    int NewLeads,
    int Qualified,
    decimal? ConversionPercent,
    IReadOnlyList<ChannelRowDto> Channels,
    IReadOnlyList<AgentOperationRowDto> Agents);

/// <summary>
/// The Escalations, Agents and Channel Intelligence views. Everything is counted from records that exist:
/// conversation turns, prospects and agent runs. Anything the platform does not measure (cost per lead,
/// response time) is not computed here, and the screen says so rather than showing a number.
/// </summary>
public sealed class AcquisitionReportService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    ISalesLeadReadRepository leadRepository,
    IConversationActivityReader activityReader,
    IAgentRunRepository agentRunRepository,
    IAgentGate agentGate,
    IUserAccountRepository? userAccountRepository = null) : IAcquisitionReportService
{
    private static readonly string[] AcquisitionAgents = ["customer-assistant", "conversation-insights", ProspectTriageService.AgentKey];

    public async Task<EscalationReportDto> EscalationsAsync(CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewSalesLeads, cancellationToken);
        var scope = tenantContextAccessor.Current.Scope;
        var leads = await leadRepository.ListLeadsAsync(scope.TenantId, scope.WorkspaceId, cancellationToken);
        var turns = await activityReader.ListTurnsAsync(scope, cancellationToken);
        var users = userAccountRepository is null
            ? new Dictionary<string, string>()
            : (await userAccountRepository.ListAsync(scope, cancellationToken)).ToDictionary(user => user.UserId, user => user.Name, StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;

        var rows = new List<EscalationRowDto>();
        foreach (var thread in turns.GroupBy(turn => turn.ThreadId))
        {
            var ordered = thread.OrderBy(turn => turn.OccurredAt).ToArray();
            var raised = ordered.LastOrDefault(turn =>
                turn.Outcome is CustomerAssistantOutcome.Escalated or CustomerAssistantOutcome.QueuedForHumanApproval);
            if (raised is null)
            {
                continue;
            }

            var lead = leads
                .Where(item => item.ThreadId == thread.Key
                    || (item.Phone is not null && InsuranceIntelligence.Domain.Customers.PhoneNumbers.SameNumber(item.Phone, raised.ContactWaId)))
                .OrderByDescending(item => item.LastInteractionAt ?? item.CreatedAt ?? DateTimeOffset.MinValue)
                .FirstOrDefault();

            var after = ordered.Where(turn => turn.OccurredAt > raised.OccurredAt).ToArray();
            var control = ConversationControl.LastControlTurn(after);
            var handedBack = control?.Outcome == CustomerAssistantOutcome.HandedBackToKato;
            var personReplied = control is not null && !handedBack;
            var customerWroteAfter = control is not null && after.Any(turn => turn.OccurredAt > control.OccurredAt && !string.IsNullOrWhiteSpace(turn.InboundMessageText)
                && turn.Outcome is not (CustomerAssistantOutcome.HumanTookOver or CustomerAssistantOutcome.HumanReplied or CustomerAssistantOutcome.HandedBackToKato));

            var status = handedBack ? "Closed"
                : personReplied && !customerWroteAfter ? "Pending customer"
                : personReplied ? "With a person"
                : "Open";

            var band = lead?.Triage?.Band;
            var priority = band is null ? "Not scored"
                : band is ReadinessBand.HighPriority ? "High"
                : band is ReadinessBand.Qualified or ReadinessBand.Warm ? "Medium"
                : "Low";

            var assigneeId = control?.HumanUserId ?? lead?.AssignedSalesOfficer;
            var name = lead is not null && !string.IsNullOrWhiteSpace(lead.CustomerName) ? lead.CustomerName : "WhatsApp contact";
            rows.Add(new EscalationRowDto(
                raised.TurnId.ToString("N"),
                name,
                ContactMask.Mask(lead?.Phone ?? raised.ContactWaId),
                raised.Outcome == CustomerAssistantOutcome.Escalated ? "Asked for a person" : "Reply needs approval",
                status,
                priority,
                assigneeId is null ? null : users.GetValueOrDefault(assigneeId) ?? assigneeId,
                raised.OccurredAt,
                (after.LastOrDefault() ?? raised).OccurredAt,
                thread.Key,
                lead?.LeadId,
                lead?.BankCustomer));
        }

        var open = rows.Where(row => row.Status != "Closed").ToArray();
        return new EscalationReportDto(
            open.Count(row => row.Status == "Open"),
            open.Count(row => row.Priority == "High"),
            open.Count(row => row.Status == "Pending customer"),
            open.Count(row => row.BankCustomer == false),
            open.Count(row => row.AssigneeName is null),
            rows.OrderByDescending(row => row.UpdatedAt).ToArray());
    }

    public async Task<AgentOperationsDto> AgentOperationsAsync(CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewSalesLeads, cancellationToken);
        var rows = await AgentRowsAsync(cancellationToken);
        return new AgentOperationsDto(
            rows.Count,
            rows.Count(row => row.Enabled && row.Health == "Healthy"),
            rows.Count(row => row.Health == "Errors"),
            rows.Count(row => row.Runs == 0),
            rows);
    }

    public async Task<ChannelIntelligenceDto> ChannelIntelligenceAsync(CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewSalesLeads, cancellationToken);
        var scope = tenantContextAccessor.Current.Scope;
        var leads = await leadRepository.ListLeadsAsync(scope.TenantId, scope.WorkspaceId, cancellationToken);
        var turns = await activityReader.ListTurnsAsync(scope, cancellationToken);

        var customerTurns = turns.Count(turn => !string.IsNullOrWhiteSpace(turn.InboundMessageText)
            && turn.Outcome is not (CustomerAssistantOutcome.HumanTookOver or CustomerAssistantOutcome.HumanReplied or CustomerAssistantOutcome.HandedBackToKato));

        var channels = leads
            .GroupBy(lead => lead.PrimaryChannel ?? (lead.LeadSource.StartsWith("WhatsApp", StringComparison.OrdinalIgnoreCase) ? "WhatsApp" : "Manual"))
            .Select(group =>
            {
                var interactions = group.Key == "WhatsApp" ? customerTurns : 0;
                var qualified = group.Count(lead => lead.Triage is { Band: ReadinessBand.Qualified or ReadinessBand.HighPriority });
                var applied = group.Count(lead => lead.AppliedFor is not null);
                return new ChannelRowDto(group.Key, interactions, group.Count(), qualified, group.Any() ? Math.Round(applied * 100m / group.Count(), 1) : null);
            })
            .ToList();
        if (customerTurns > 0 && channels.All(channel => channel.Channel != "WhatsApp"))
        {
            channels.Add(new ChannelRowDto("WhatsApp", customerTurns, 0, 0, null));
        }

        var totalLeads = leads.Count;
        var qualifiedTotal = channels.Sum(channel => channel.Qualified);
        var appliedTotal = leads.Count(lead => lead.AppliedFor is not null);
        var agents = (await AgentRowsAsync(cancellationToken)).Where(row => AcquisitionAgents.Contains(row.Key)).ToArray();

        return new ChannelIntelligenceDto(
            customerTurns,
            totalLeads,
            qualifiedTotal,
            totalLeads == 0 ? null : Math.Round(appliedTotal * 100m / totalLeads, 1),
            channels.OrderByDescending(channel => channel.Interactions + channel.NewLeads).ToArray(),
            agents);
    }

    private async Task<IReadOnlyList<AgentOperationRowDto>> AgentRowsAsync(CancellationToken cancellationToken)
    {
        var scope = tenantContextAccessor.Current.Scope;
        var runs = await agentRunRepository.ListAsync(scope, cancellationToken);
        var rows = new List<AgentOperationRowDto>();
        foreach (var agent in AgentCatalog.Agents.Where(agent => AcquisitionAgents.Contains(agent.Key)))
        {
            var mine = runs.Where(run => string.Equals(run.AgentKey, agent.Key, StringComparison.OrdinalIgnoreCase)).ToArray();
            var gate = await agentGate.CheckAsync(scope, agent.Key, cancellationToken);
            var errors = mine.Count(run => run.Status == AgentRunStatus.Failed);
            var lastRun = mine.OrderByDescending(run => run.StartedAt).FirstOrDefault();
            var lastOk = mine.Where(run => run.Status == AgentRunStatus.Completed).OrderByDescending(run => run.StartedAt).FirstOrDefault();
            var health = mine.Length == 0 ? "No runs yet"
                : lastRun!.Status == AgentRunStatus.Failed ? "Errors"
                : errors * 5 > mine.Length ? "Degraded"
                : "Healthy";
            rows.Add(new AgentOperationRowDto(
                agent.Key,
                agent.Name,
                agent.Purpose,
                agent.ImplementationKind.ToString(),
                gate.Enabled,
                gate.Enabled ? health : "Switched off",
                mine.Length,
                errors,
                lastRun?.StartedAt,
                lastOk?.StartedAt,
                mine.Length == 0 ? 0 : Math.Round(errors * 100m / mine.Length, 1),
                gate.Enabled && agent.Key == ProspectTriageService.AgentKey));
        }

        return rows;
    }
}
