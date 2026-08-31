using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Domain.Cases;

public sealed class CaseIntakeService(ICaseRepository caseRepository, IAuditRepository auditRepository)
{
    public async Task<ClaimCase> CreateCaseAsync(CreateCaseCommand command, CancellationToken cancellationToken = default)
    {
        Validate(command);

        var now = DateTimeOffset.UtcNow;
        var motorFnol = command.MotorFnolDetails?.Normalize();
        var routing = ClaimRoutingPolicy.Evaluate(command.ClaimType, motorFnol);
        var claimCase = new ClaimCase
        {
            CaseId = Guid.NewGuid(),
            Scope = command.Scope,
            CustomerName = command.CustomerName.Trim(),
            CustomerEmail = NormalizeOptional(command.CustomerEmail),
            CustomerPhone = NormalizeOptional(command.CustomerPhone),
            PolicyNumber = command.PolicyNumber.Trim(),
            ProductLine = command.ProductLine.Trim(),
            ClaimType = command.ClaimType.Trim(),
            Channel = command.Channel.Trim(),
            IncidentDate = command.IncidentDate,
            IncidentDescription = command.IncidentDescription.Trim(),
            MotorFnolDetails = motorFnol,
            Status = routing.InitialStatus,
            CompletenessScore = 0m,
            RiskLevel = routing.InitialRiskLevel,
            Priority = routing.Priority,
            SlaDueAt = now.AddDays(routing.SlaDays),
            AssignedUserId = null,
            AssignedTeam = routing.AssignedTeam,
            CreatedByUserId = command.CreatedByUserId.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            CorrelationId = command.CorrelationId.Trim()
        };

        await caseRepository.SaveAsync(claimCase, cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.CaseCreated(claimCase.ToAuditSnapshot(), claimCase.CreatedByUserId, claimCase.CorrelationId),
            cancellationToken);

        return claimCase;
    }

    public async Task<ClaimCase?> GetCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        return claimCase is not null && claimCase.Scope.Matches(scope) ? claimCase : null;
    }

    private static void Validate(CreateCaseCommand command)
    {
        _ = TenantScope.Create(command.Scope.TenantId, command.Scope.WorkspaceId);

        Require(command.CustomerName, nameof(command.CustomerName));
        Require(command.PolicyNumber, nameof(command.PolicyNumber));
        Require(command.ProductLine, nameof(command.ProductLine));
        Require(command.ClaimType, nameof(command.ClaimType));
        Require(command.Channel, nameof(command.Channel));
        Require(command.IncidentDescription, nameof(command.IncidentDescription));
        Require(command.CreatedByUserId, nameof(command.CreatedByUserId));
        Require(command.CorrelationId, nameof(command.CorrelationId));

        if (command.IncidentDate == default)
        {
            throw new ArgumentException("Incident date is required.", nameof(command.IncidentDate));
        }
    }

    private static void Require(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.", name);
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
