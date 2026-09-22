using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Products;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Domain.Cases;

/// <summary>productRepository is optional so every existing caller keeps compiling unchanged;
/// omitting it simply means claims it creates carry no ProductId/ProductVersion pin (outcome C),
/// exactly like a tenant that has not configured that product yet.</summary>
public sealed class CaseIntakeService(ICaseRepository caseRepository, IAuditRepository auditRepository, IProductRepository? productRepository = null)
{
    public async Task<ClaimCase> CreateCaseAsync(CreateCaseCommand command, CancellationToken cancellationToken = default)
    {
        Validate(command);

        var now = DateTimeOffset.UtcNow;
        var motorFnol = command.MotorFnolDetails?.Normalize();
        var medicalFnol = command.MedicalFnolDetails?.Normalize();
        var routing = ClaimRoutingPolicy.Evaluate(command.ClaimType, motorFnol, productLine: command.ProductLine);

        // Name a real owner, not just a team, wherever the routed team's case history has enough
        // information to infer one, see CaseAssignmentPolicy for why this is inferred rather than
        // backed by a staff directory this codebase does not have.
        var existingCases = await caseRepository.ListAsync(command.Scope, cancellationToken);
        var assignee = CaseAssignmentPolicy.SelectAssignee(routing.AssignedTeam, existingCases);
        var status = assignee is not null && routing.InitialStatus == CaseStatus.New
            ? CaseStatus.Assigned
            : routing.InitialStatus;

        var (productId, productVersion) = await ResolveProductPinAsync(command.Scope, command.ProductLine, cancellationToken);

        var claimCase = new ClaimCase
        {
            CaseId = Guid.NewGuid(),
            Scope = command.Scope,
            CustomerName = command.CustomerName.Trim(),
            CustomerEmail = NormalizeOptional(command.CustomerEmail),
            CustomerPhone = NormalizeOptional(command.CustomerPhone),
            PolicyNumber = command.PolicyNumber.Trim(),
            ProductLine = command.ProductLine.Trim(),
            ProductId = productId,
            ProductVersion = productVersion,
            ClaimType = command.ClaimType.Trim(),
            Channel = command.Channel.Trim(),
            IncidentDate = command.IncidentDate,
            IncidentDescription = command.IncidentDescription.Trim(),
            MotorFnolDetails = motorFnol,
            MedicalFnolDetails = medicalFnol,
            Status = status,
            CompletenessScore = 0m,
            RiskLevel = routing.InitialRiskLevel,
            Priority = routing.Priority,
            SlaDueAt = command.RequiresConfirmation ? null : now.AddDays(routing.SlaDays),
            RequiresConfirmation = command.RequiresConfirmation,
            AssignedUserId = assignee,
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

    /// <summary>
    /// A human confirming a claim IIP auto-created from an identified WhatsApp/email sender
    /// (RequiresConfirmation), this is what starts the real SLA clock. Optional corrections let
    /// the confirming officer fix the best-effort product line/claim type/description IIP guessed
    /// from the raw inbound message before the SLA is set, without a second round trip.
    /// </summary>
    public async Task<ClaimCaseConfirmationResult> ConfirmCaseAsync(
        Guid caseId,
        TenantScope scope,
        ConfirmCaseCorrections corrections,
        CancellationToken cancellationToken = default)
    {
        var claimCase = await GetCaseAsync(caseId, scope, cancellationToken);
        if (claimCase is null)
        {
            return ClaimCaseConfirmationResult.NotFound;
        }

        if (!claimCase.RequiresConfirmation)
        {
            return ClaimCaseConfirmationResult.AlreadyConfirmed(claimCase);
        }

        var productLine = string.IsNullOrWhiteSpace(corrections.ProductLine) ? claimCase.ProductLine : corrections.ProductLine.Trim();
        var claimType = string.IsNullOrWhiteSpace(corrections.ClaimType) ? claimCase.ClaimType : corrections.ClaimType.Trim();
        var incidentDescription = string.IsNullOrWhiteSpace(corrections.IncidentDescription) ? claimCase.IncidentDescription : corrections.IncidentDescription.Trim();
        var incidentDate = corrections.IncidentDate ?? claimCase.IncidentDate;

        var routing = ClaimRoutingPolicy.Evaluate(claimType, claimCase.MotorFnolDetails, productLine: productLine);
        var (productId, productVersion) = string.Equals(productLine, claimCase.ProductLine, StringComparison.OrdinalIgnoreCase)
            ? (claimCase.ProductId, claimCase.ProductVersion)
            : await ResolveProductPinAsync(claimCase.Scope, productLine, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var confirmed = claimCase with
        {
            ProductLine = productLine,
            ProductId = productId,
            ProductVersion = productVersion,
            ClaimType = claimType,
            IncidentDescription = incidentDescription,
            IncidentDate = incidentDate,
            RequiresConfirmation = false,
            Status = routing.InitialStatus,
            RiskLevel = routing.InitialRiskLevel,
            Priority = routing.Priority,
            SlaDueAt = now.AddDays(routing.SlaDays),
            AssignedTeam = routing.AssignedTeam,
            UpdatedAt = now
        };
        await caseRepository.SaveAsync(confirmed, cancellationToken);

        return ClaimCaseConfirmationResult.Confirmed(claimCase, confirmed);
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

    /// <summary>Matches a claim's free-text ProductLine to a configured, Published product by
    /// name, and pins the exact version in force right now. Returns (null, null) when no product
    /// repository is wired or no Published product matches, in which case ProductLine alone
    /// continues to describe the claim, exactly as it always has.</summary>
    private async Task<(Guid? ProductId, int? Version)> ResolveProductPinAsync(TenantScope scope, string productLine, CancellationToken cancellationToken)
    {
        if (productRepository is null || string.IsNullOrWhiteSpace(productLine))
        {
            return (null, null);
        }

        var products = await productRepository.ListCurrentForAllProductsAsync(scope, cancellationToken);
        var match = products.FirstOrDefault(product =>
            product.Status == ProductStatus.Published
            && string.Equals(product.Name, productLine.Trim(), StringComparison.OrdinalIgnoreCase));

        return match is null ? (null, null) : (match.ProductId, match.Version);
    }
}

public sealed record ConfirmCaseCorrections(
    string? ProductLine = null,
    string? ClaimType = null,
    DateOnly? IncidentDate = null,
    string? IncidentDescription = null);

public sealed record ClaimCaseConfirmationResult
{
    public required bool Found { get; init; }
    public required bool WasAlreadyConfirmed { get; init; }
    public ClaimCase? Before { get; init; }
    public ClaimCase? After { get; init; }

    public static readonly ClaimCaseConfirmationResult NotFound = new() { Found = false, WasAlreadyConfirmed = false };

    public static ClaimCaseConfirmationResult AlreadyConfirmed(ClaimCase claimCase) =>
        new() { Found = true, WasAlreadyConfirmed = true, Before = claimCase, After = claimCase };

    public static ClaimCaseConfirmationResult Confirmed(ClaimCase before, ClaimCase after) =>
        new() { Found = true, WasAlreadyConfirmed = false, Before = before, After = after };
}
