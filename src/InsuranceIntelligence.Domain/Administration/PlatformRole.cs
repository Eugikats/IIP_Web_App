namespace InsuranceIntelligence.Domain.Administration;

public enum PlatformRole
{
    Admin,
    ClaimsOfficer,
    ClaimsSupervisor,
    CustomerService,
    Broker,
    Compliance,
    Client,
    KnowledgeManager,
    ITAdmin,
    PublisherSupport,

    /// <summary>A system acting for an insurer through an API key, never a person.</summary>
    Integration
}
