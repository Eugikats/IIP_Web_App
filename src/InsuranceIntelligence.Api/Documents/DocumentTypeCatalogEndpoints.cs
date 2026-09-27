using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Api.Documents;

/// <summary>
/// The document type catalog an insurer owns (claim intake build prompt outcome A): the eight
/// built-in types every tenant starts with, plus any type an admin adds that the classifier does
/// not recognise. Versioned exactly like CommunicationTemplate: every save is a new row, nothing
/// is overwritten in place.
/// </summary>
public static class DocumentTypeCatalogEndpoints
{
    public static async Task<IResult> ListCurrent(
        IDocumentTypeCatalogRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        var current = await repository.ListCurrentAsync(scope, cancellationToken);
        return Results.Ok(current.OrderBy(d => d.CustomerLabel, StringComparer.OrdinalIgnoreCase).Select(ToResponse).ToArray());
    }

    public static async Task<IResult> ListVersions(
        string key,
        IDocumentTypeCatalogRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        var versions = await repository.ListVersionsAsync(scope, key, cancellationToken);
        return Results.Ok(versions.OrderByDescending(d => d.Version).Select(ToResponse).ToArray());
    }

    public static async Task<IResult> SaveVersion(
        string key,
        SaveDocumentTypeDefinitionRequest request,
        IDocumentTypeCatalogRepository repository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            return WorkflowEndpoints.Validation("key", "A stable key is required.");
        }

        if (string.IsNullOrWhiteSpace(request.CustomerLabel))
        {
            return WorkflowEndpoints.Validation("customerLabel", "The words a customer sees for this document are required.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return WorkflowEndpoints.Validation("description", "A plain-words reason this document is needed is required.");
        }

        var actor = WorkflowEndpoints.GetActor(httpContext, null);
        if (string.IsNullOrWhiteSpace(actor))
        {
            return WorkflowEndpoints.Validation("userId", "Authenticated user context is required to change the document type catalog.");
        }

        var auditReason = AuditReasonReader.Read(httpContext);
        if (string.IsNullOrWhiteSpace(auditReason))
        {
            return WorkflowEndpoints.Validation("auditReason", "A reason is required to change the document type catalog.");
        }

        var current = await repository.GetCurrentAsync(scope, key, cancellationToken);
        var builtInType = Enum.TryParse<DocumentType>(key, ignoreCase: true, out var parsed) ? parsed : (DocumentType?)null;
        var version = new DocumentTypeDefinition
        {
            DefinitionId = current?.DefinitionId ?? Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            Scope = scope,
            Version = (current?.Version ?? 0) + 1,
            Key = key.Trim(),
            CustomerLabel = request.CustomerLabel.Trim(),
            Description = request.Description.Trim(),
            BuiltInType = builtInType,
            Active = request.Active,
            UpdatedByUserId = actor,
            UpdatedAt = DateTimeOffset.UtcNow,
            ChangeReason = auditReason,
        };

        await repository.SaveAsync(version, cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.DocumentTypeDefinitionSaved(scope, actor, version.Key, version.Version, version.Active, auditReason, httpContext.TraceIdentifier),
            cancellationToken);

        return Results.Ok(ToResponse(version));
    }

    /// <summary>Seeds the tenant's catalog with the eight built-in types. Idempotent: a key that
    /// already exists for this tenant (built-in or custom) is left alone, so running this again
    /// after an admin has customised a label never overwrites their edit.</summary>
    public static async Task<IResult> SeedDefaults(
        IDocumentTypeCatalogRepository repository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        var actor = WorkflowEndpoints.GetActor(httpContext, null);
        if (string.IsNullOrWhiteSpace(actor))
        {
            return WorkflowEndpoints.Validation("userId", "Authenticated user context is required to seed the document type catalog.");
        }

        var existing = await repository.ListCurrentAsync(scope, cancellationToken);
        var existingKeys = existing.Select(d => d.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var created = new List<DocumentTypeDefinitionResponse>();
        foreach (var definition in DefaultDocumentTypeCatalog.BuildFor(scope, actor, DateTimeOffset.UtcNow))
        {
            if (existingKeys.Contains(definition.Key))
            {
                continue;
            }

            await repository.SaveAsync(definition, cancellationToken);
            await auditRepository.WriteAsync(
                AuditEvent.DocumentTypeDefinitionSaved(scope, actor, definition.Key, definition.Version, definition.Active, definition.ChangeReason, httpContext.TraceIdentifier),
                cancellationToken);
            created.Add(ToResponse(definition));
        }

        return Results.Ok(new { seeded = created.Count, types = created });
    }

    /// <summary>Claim intake build prompt outcome A's other acceptance test: an admin view listing
    /// any existing product whose document matrix already holds a key the tenant's active catalog
    /// does not recognise, so a silent drop that already happened (a typo saved before this
    /// validation existed) becomes visible instead of staying invisible. Nothing here is rewritten
    /// automatically.</summary>
    public static async Task<IResult> ListUnresolvedProductDocumentKeys(
        IProductRepository productRepository,
        IDocumentTypeCatalogRepository catalogRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        var activeCatalogKeys = (await catalogRepository.ListCurrentAsync(scope, cancellationToken))
            .Where(d => d.Active)
            .Select(d => d.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var products = await productRepository.ListCurrentForAllProductsAsync(scope, cancellationToken);
        var flagged = products
            .Select(product => new
            {
                product,
                unresolvedKeys = product.DocumentMatrix
                    .Select(entry => entry.DocumentKey)
                    .Where(entryKey => !Enum.TryParse<DocumentType>(entryKey, ignoreCase: true, out _) && !activeCatalogKeys.Contains(entryKey))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
            })
            .Where(entry => entry.unresolvedKeys.Length > 0)
            .Select(entry => new UnresolvedProductDocumentKeysResponse(
                entry.product.ProductId,
                entry.product.Name,
                entry.product.Version,
                entry.unresolvedKeys))
            .ToArray();

        return Results.Ok(flagged);
    }

    private static DocumentTypeDefinitionResponse ToResponse(DocumentTypeDefinition definition) =>
        new(
            definition.DefinitionId,
            definition.Key,
            definition.Version,
            definition.CustomerLabel,
            definition.Description,
            definition.BuiltInType?.ToString(),
            definition.Active,
            definition.UpdatedByUserId,
            definition.UpdatedAt,
            definition.ChangeReason);
}

public sealed record SaveDocumentTypeDefinitionRequest(
    string? CustomerLabel,
    string? Description,
    bool Active);

public sealed record DocumentTypeDefinitionResponse(
    Guid DefinitionId,
    string Key,
    int Version,
    string CustomerLabel,
    string Description,
    string? BuiltInType,
    bool Active,
    string UpdatedByUserId,
    DateTimeOffset UpdatedAt,
    string ChangeReason);

public sealed record UnresolvedProductDocumentKeysResponse(
    Guid ProductId,
    string ProductName,
    int Version,
    IReadOnlyList<string> UnresolvedDocumentKeys);
