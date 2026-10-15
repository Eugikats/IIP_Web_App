using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Academy;

/// <summary>
/// The Academy is run by the platform's own workspace, not by any insurer. That workspace's administrators are the
/// Academy's operators: they set prices, enrol people, mark final tasks, run the community and set the Discord link.
/// Configured by <c>Academy:OperatorTenantId</c> and <c>Academy:OperatorWorkspaceId</c>, defaulting to the
/// deployment's own tenant and workspace.
/// </summary>
public sealed class AcademyOperator(InfrastructureOptions options, IConfiguration configuration)
{
    public TenantScope Scope { get; } = Resolve(options, configuration);

    private static TenantScope Resolve(InfrastructureOptions options, IConfiguration configuration)
    {
        var tenant = FirstNonBlank(configuration["Academy:OperatorTenantId"], options.TenantId, "tenant-demo");
        var workspace = FirstNonBlank(configuration["Academy:OperatorWorkspaceId"], options.WorkspaceId, "workspace-operations");
        return TenantScope.Create(tenant, workspace);
    }

    private static string FirstNonBlank(params string?[] values) => values.First(value => !string.IsNullOrWhiteSpace(value))!;
}
