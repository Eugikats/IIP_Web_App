namespace InsuranceIntelligence.Domain.Common;

/// <summary>
/// Domain-rule violation. Free of ASP.NET / EF / Azure SDK dependencies.
/// </summary>
public sealed class DomainException(string message) : Exception(message);
