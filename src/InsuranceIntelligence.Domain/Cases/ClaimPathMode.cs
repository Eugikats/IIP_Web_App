namespace InsuranceIntelligence.Domain.Cases;

public enum ClaimPathMode
{
    /// <summary>Standard FNOL → intake → assignment → review → approval → communication → export.</summary>
    HappyPath,

    /// <summary>Same path with mandatory designated-human escalation before consequential progression.</summary>
    ExceptionRoute
}
