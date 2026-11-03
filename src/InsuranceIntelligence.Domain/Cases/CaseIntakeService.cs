using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.Domain.Products;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Domain.Cases;

/// <summary>productRepository and policyRepository are optional so every existing caller keeps
/// compiling unchanged; omitting productRepository simply means claims it creates carry no
/// ProductId/ProductVersion pin (outcome C), and omitting policyRepository means no advisory
/// policy-in-force/sum-insured signal is computed (outcome E), exactly like a tenant that has not
/// configured that capability yet.</summary>
public sealed class CaseIntakeService(
    ICaseRepository caseRepository,
    IAuditRepository auditRepository,
    IProductRepository? productRepository = null,
    IPolicyRepository? policyRepository = null)
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

        var pinnedProduct = await ResolveProductAsync(command.Scope, command.ProductLine, cancellationToken);
        var (productId, productVersion) = (pinnedProduct?.ProductId, pinnedProduct?.Version);
        var claimTypeResolution = ResolveClaimType(pinnedProduct, command.ClaimType, command.IncidentDescription, command.RequireConfiguredClaimType);
        var fieldValues = ResolveFieldValues(command, pinnedProduct, claimTypeResolution.Key, ref motorFnol, ref medicalFnol);
        var advisoryRiskSignals = await ResolveAdvisoryRiskSignalsAsync(command.Scope, command.PolicyNumber, command.IncidentDate, command.ClaimedAmount, cancellationToken);

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
            ClaimedAmount = command.ClaimedAmount,
            AdvisoryRiskSignals = advisoryRiskSignals,
            ClaimType = claimTypeResolution.Name,
            ClaimTypeKey = claimTypeResolution.Key,
            FieldValues = fieldValues,
            Channel = command.Channel.Trim(),
            IncidentDate = command.IncidentDate,
            IncidentDescription = command.IncidentDescription.Trim(),
            OriginalIncidentText = string.IsNullOrWhiteSpace(command.OriginalIncidentText) ? null : command.OriginalIncidentText.Trim(),
            OriginalIncidentLanguage = string.IsNullOrWhiteSpace(command.OriginalIncidentText) ? null : command.OriginalIncidentLanguage,
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
        var productChanged = !string.Equals(productLine, claimCase.ProductLine, StringComparison.OrdinalIgnoreCase);
        var confirmedProduct = productChanged
            ? await ResolveProductAsync(claimCase.Scope, productLine, cancellationToken)
            : await ResolvePinnedProductAsync(claimCase, cancellationToken);
        var (productId, productVersion) = productChanged
            ? (confirmedProduct?.ProductId, confirmedProduct?.Version)
            : (claimCase.ProductId, claimCase.ProductVersion);
        // Confirming is where a person fixes the best guess an automatic channel made, so a typed
        // claim type is matched against the configured types; an unmatched one is kept as text.
        var confirmedType = ResolveClaimType(confirmedProduct, claimType, incidentDescription, requireConfigured: false);
        var now = DateTimeOffset.UtcNow;
        var confirmed = claimCase with
        {
            ProductLine = productLine,
            ProductId = productId,
            ProductVersion = productVersion,
            ClaimType = confirmedType.Name,
            ClaimTypeKey = confirmedType.Key,
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

    /// <summary>Matches a claim's free-text ProductLine to a configured, Published product, and
    /// returns it so its exact version can be pinned. Null when no product repository is wired or no
    /// Published product matches, in which case ProductLine alone continues to describe the claim,
    /// exactly as it always has.</summary>
    private async Task<Product?> ResolveProductAsync(TenantScope scope, string productLine, CancellationToken cancellationToken)
    {
        if (productRepository is null || string.IsNullOrWhiteSpace(productLine))
        {
            return null;
        }

        var products = await productRepository.ListCurrentForAllProductsAsync(scope, cancellationToken);
        // ProductClass is the line ("Motor", "Medical"), not Name (an admin-chosen display name
        // like "Motor Comprehensive" or the demo seed's "Motor Cover"). Matching on Name meant a
        // freshly filed claim never pinned to a real product even when one existed for its line,
        // silently falling back to the generic built-in checklist every time.
        return products.FirstOrDefault(product =>
            product.Status == ProductStatus.Published
            && string.Equals(product.ProductClass, productLine.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The exact product version a claim was pinned to (not the latest).</summary>
    private async Task<Product?> ResolvePinnedProductAsync(ClaimCase claimCase, CancellationToken cancellationToken)
    {
        if (productRepository is null || claimCase.ProductId is not { } productId || claimCase.ProductVersion is not { } version)
        {
            return null;
        }

        return await productRepository.GetVersionAsync(claimCase.Scope, productId, version, cancellationToken);
    }

    /// <summary>Checks the submitted answers against the claim's field definitions. A person filling
    /// a form must satisfy required fields; the original Motor/Medical FNOL details count as answers
    /// so existing callers keep working, and answers for the built-in keys also fill the FNOL records
    /// that injury detection and routing read.</summary>
    private static IReadOnlyDictionary<string, string>? ResolveFieldValues(
        CreateCaseCommand command,
        Product? product,
        string? claimTypeKey,
        ref MotorFnolDetails? motorFnol,
        ref MedicalFnolDetails? medicalFnol)
    {
        var definitions = ClaimFieldResolver.ForProduct(product, command.ProductLine, claimTypeKey);

        // Type-check only what was submitted as field values. The original FNOL records are not
        // re-validated (older clients send free wording there), but they do count as answers for
        // the purpose of "is this required field present".
        var result = ClaimFieldValidator.Validate(
            definitions,
            command.FieldValues ?? new Dictionary<string, string>(),
            requireRequired: false,
            customerAudience: command.FieldsFromCustomer);
        var errors = new Dictionary<string, string>(result.Errors, StringComparer.OrdinalIgnoreCase);
        if (command.RequireConfiguredClaimType && product is { ClaimFields.Count: > 0 })
        {
            var present = new HashSet<string>(result.Values.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (var (key, _) in ClaimFieldValues.FromFnol(motorFnol, medicalFnol))
            {
                present.Add(key);
            }

            foreach (var definition in definitions.Where(candidate => candidate.Required && (!command.FieldsFromCustomer || candidate.CustomerVisible)))
            {
                if (!present.Contains(definition.Key) && !errors.ContainsKey(definition.Key))
                {
                    errors[definition.Key] = $"{definition.Label} is needed.";
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new ClaimFieldsInvalidException(errors);
        }

        if (command.FieldValues is null)
        {
            return null;
        }

        motorFnol = ClaimFieldValues.ToMotorFnol(result.Values, motorFnol);
        medicalFnol = ClaimFieldValues.ToMedicalFnol(result.Values, medicalFnol);
        return result.Values;
    }

    private static (string Name, string? Key) ResolveClaimType(Product? product, string typed, string? description, bool requireConfigured)
    {
        var trimmed = typed.Trim();
        var configured = product?.ActiveClaimTypes ?? [];
        if (product is null || configured.Count == 0)
        {
            return (trimmed, null);
        }

        // Only active types: a retired type still resolves for history (Product.FindClaimType) but
        // can no longer be chosen for a new claim.
        var match = ClaimTypeMatching.Find(configured, trimmed) ?? (requireConfigured ? null : product.SuggestClaimType($"{trimmed} {description}"));
        if (match is not null)
        {
            return (match.Name, match.Key);
        }

        if (requireConfigured)
        {
            throw new ClaimTypeNotConfiguredException(trimmed, configured.Select(type => type.Name).ToArray());
        }

        return (trimmed, null);
    }

    /// <summary>Deterministic, rule-based advisory only (PolicyRiskSignalEvaluator), never a
    /// decision and never blocks intake. Empty when no policy repository is wired or no policy
    /// matches, exactly as honest as a tenant with no core-system connection at all.</summary>
    private async Task<IReadOnlyList<string>> ResolveAdvisoryRiskSignalsAsync(
        TenantScope scope, string policyNumber, DateOnly incidentDate, decimal? claimedAmount, CancellationToken cancellationToken)
    {
        if (policyRepository is null || string.IsNullOrWhiteSpace(policyNumber))
        {
            return [];
        }

        var policy = await policyRepository.GetByPolicyNumberAsync(policyNumber.Trim(), scope, cancellationToken);
        return PolicyRiskSignalEvaluator.Evaluate(policy, incidentDate, claimedAmount);
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

/// <summary>A person chose a claim type that the claim's product does not offer.</summary>
public sealed class ClaimTypeNotConfiguredException(string chosen, IReadOnlyList<string> allowed)
    : ArgumentException($"'{chosen}' is not a claim type for this product. Choose one of: {string.Join(", ", allowed)}.", "ClaimType")
{
    public string Chosen { get; } = chosen;
    public IReadOnlyList<string> Allowed { get; } = allowed;
}

/// <summary>The submitted answers do not satisfy the claim's fields. Errors are keyed by field key.</summary>
public sealed class ClaimFieldsInvalidException(IReadOnlyDictionary<string, string> errors)
    : ArgumentException(errors.Values.First(), "fieldValues")
{
    public IReadOnlyDictionary<string, string> Errors { get; } = errors;
}
