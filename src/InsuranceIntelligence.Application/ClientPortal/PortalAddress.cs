using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.ClientPortal;

/// <summary>
/// Where a workspace's customers are sent (build-loop item 29). A workspace with a verified web address of its own is reached
/// there; every other workspace is reached on the deployment's shared customer portal, with its short name in the link so the
/// page knows whose it is. Null when no address can be made, so a link is never built from a guess.
/// </summary>
public interface IPortalAddress
{
    string? BaseUrlFor(TenantScope scope);

    /// <summary>What to add to the end of a link that already has a query: "&amp;t=short-name" on the shared portal, nothing on the
    /// workspace's own address.</summary>
    string LinkQueryFor(TenantScope scope);
}
