using InsuranceIntelligence.Application.Documents;

namespace InsuranceIntelligence.Api.Local;

/// <summary>The API's name for the shared completeness arithmetic (see
/// <see cref="DocumentCompletenessScorer"/>, which the Functions app uses too).</summary>
public sealed class CompletenessScoringService : DocumentCompletenessScorer;
