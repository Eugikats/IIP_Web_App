using System.Globalization;
using System.Text.RegularExpressions;
using InsuranceIntelligence.Domain.Cases;

namespace InsuranceIntelligence.Domain.Products;

public enum ClaimFieldType
{
    Text,
    Number,
    Date,
    Select,
    Phone,
    Currency,
    YesNo,
}

/// <summary>
/// One question a product asks about a claim (build-loop item 5), for example "Vehicle registration"
/// or "Was anyone injured?". The key is stable and is what a claim stores a value under; the label,
/// help text and options are what people read. <c>CustomerVisible</c> fields are asked of the
/// claimant; the rest are for staff only. <c>ClaimTypeKeys</c> limits a field to certain claim types
/// (empty means every claim type of the product), like a document requirement.
/// </summary>
public sealed record ClaimFieldDefinition(
    string Key,
    string Label,
    ClaimFieldType Type,
    bool Required,
    IReadOnlyList<string>? Options,
    string? HelpText,
    int SortOrder,
    bool CustomerVisible,
    IReadOnlyList<string>? ClaimTypeKeys = null)
{
    public bool AppliesTo(string? claimTypeKey) =>
        ClaimTypeKeys is not { Count: > 0 }
        || (!string.IsNullOrWhiteSpace(claimTypeKey)
            && ClaimTypeKeys.Any(key => string.Equals(key, claimTypeKey, StringComparison.OrdinalIgnoreCase)));
}

/// <summary>The questions for a claim: the claim's pinned product (for its claim type) when it has
/// configured any, otherwise the built-in definitions that mirror the original Motor and Medical
/// FNOL records, so a claim on a product with no configured fields behaves exactly as before.</summary>
public static class ClaimFieldResolver
{
    public static IReadOnlyList<ClaimFieldDefinition> ForProduct(Product? product, string productLine, string? claimTypeKey)
    {
        if (product is { ClaimFields.Count: > 0 })
        {
            return product.ClaimFields
                .Where(field => field.AppliesTo(claimTypeKey))
                .OrderBy(field => field.SortOrder)
                .ToArray();
        }

        return BuiltInClaimFields.For(productLine);
    }
}

/// <summary>
/// The built-in field definitions. Their keys are the property names of
/// <see cref="MotorFnolDetails"/> and <see cref="MedicalFnolDetails"/> in camelCase, which is what
/// lets an existing claim's values be read through the same definitions without a migration, and
/// lets a claim filed through the dynamic form still feed injury detection and routing.
/// </summary>
public static class BuiltInClaimFields
{
    private static readonly string[] YesNoUnknown = ["Unknown", "No", "Yes"];

    public static IReadOnlyList<ClaimFieldDefinition> For(string? productLine) =>
        string.Equals(productLine, "Motor", StringComparison.OrdinalIgnoreCase) ? Motor
        : string.Equals(productLine, "Medical", StringComparison.OrdinalIgnoreCase) ? Medical
        : [];

    public static readonly IReadOnlyList<ClaimFieldDefinition> Motor =
    [
        new("vehicleRegistration", "Vehicle registration", ClaimFieldType.Text, false, null, "As shown on the number plate, for example UAX 123A.", 0, true),
        new("accidentTime", "Time of the accident", ClaimFieldType.Text, false, null, "For example 14:30, or 'early evening'.", 1, true),
        new("accidentLocation", "Where it happened", ClaimFieldType.Text, false, null, "The road, town or landmark.", 2, true),
        new("driverName", "Driver at the time", ClaimFieldType.Text, false, null, null, 3, true),
        new("drivingPermitNumber", "Driving permit number", ClaimFieldType.Text, false, null, null, 4, true),
        new("thirdPartyInvolved", "Was another vehicle or person involved?", ClaimFieldType.Select, false, YesNoUnknown, null, 5, true),
        new("injuryIndicator", "Was anyone injured?", ClaimFieldType.Select, false, ["Unknown", "No injuries reported", "Injury reported"], "If anyone was hurt, say so; it changes how fast we handle the claim.", 6, true),
        new("policeReportStatus", "Police report", ClaimFieldType.Select, false, ["Not needed", "Not yet reported", "Reported, abstract pending", "Abstract obtained"], null, 7, true),
        new("preferredCommunicationChannel", "How should we contact you?", ClaimFieldType.Select, false, ["Phone", "WhatsApp", "Email", "SMS", "Broker"], null, 8, true),
    ];

    public static readonly IReadOnlyList<ClaimFieldDefinition> Medical =
    [
        new("patientName", "Patient name", ClaimFieldType.Text, false, null, null, 0, true),
        new("relationshipToPolicyholder", "Relationship to the policyholder", ClaimFieldType.Text, false, null, "For example self, spouse, child.", 1, true),
        new("treatmentDate", "Treatment date", ClaimFieldType.Date, false, null, null, 2, true),
        new("providerName", "Hospital or clinic", ClaimFieldType.Text, false, null, null, 3, true),
        new("admissionType", "Type of visit", ClaimFieldType.Select, false, ["Outpatient", "Inpatient", "Emergency", "Maternity", "Dental or optical"], null, 4, true),
        new("treatingDoctor", "Treating doctor", ClaimFieldType.Text, false, null, null, 5, true),
        new("diagnosisSummary", "What was it for?", ClaimFieldType.Text, false, null, "A short description is enough.", 6, true),
        new("preferredCommunicationChannel", "How should we contact you?", ClaimFieldType.Select, false, ["Phone", "WhatsApp", "Email", "SMS", "Broker"], null, 7, true),
    ];
}

/// <summary>Reads and writes the values of a claim's fields. Values live in
/// <see cref="ClaimCase.FieldValues"/>; a claim filed before fields existed has its values in the
/// original Motor/Medical FNOL record, which this reads through so nothing has to be migrated.</summary>
public static class ClaimFieldValues
{
    public static IReadOnlyDictionary<string, string> For(ClaimCase claimCase)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in LegacyValues(claimCase))
        {
            values[key] = value;
        }

        if (claimCase.FieldValues is not null)
        {
            foreach (var (key, value) in claimCase.FieldValues)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values[key] = value;
                }
            }
        }

        return values;
    }

    /// <summary>A readable label for a field key when the product's definition is not at hand:
    /// the built-in label if there is one, otherwise the camelCase key split into words.</summary>
    public static string LabelFor(string key)
    {
        var builtIn = BuiltInClaimFields.Motor.Concat(BuiltInClaimFields.Medical)
            .FirstOrDefault(field => string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase));
        if (builtIn is not null)
        {
            return builtIn.Label;
        }

        var words = Regex.Replace(key, "([a-z0-9])([A-Z])", "$1 $2").ToLowerInvariant();
        return words.Length == 0 ? key : char.ToUpperInvariant(words[0]) + words[1..];
    }

    private static IEnumerable<KeyValuePair<string, string>> LegacyValues(ClaimCase claimCase) =>
        FromFnol(claimCase.MotorFnolDetails, claimCase.MedicalFnolDetails);

    /// <summary>The values held in the original Motor and Medical FNOL records, keyed like the
    /// built-in field definitions.</summary>
    public static IEnumerable<KeyValuePair<string, string>> FromFnol(MotorFnolDetails? motorDetails, MedicalFnolDetails? medicalDetails)
    {
        var pairs = new List<(string Key, string? Value)>();
        if (motorDetails is { } motor)
        {
            pairs.Add(("vehicleRegistration", motor.VehicleRegistration));
            pairs.Add(("accidentTime", motor.AccidentTime));
            pairs.Add(("accidentLocation", motor.AccidentLocation));
            pairs.Add(("driverName", motor.DriverName));
            pairs.Add(("drivingPermitNumber", motor.DrivingPermitNumber));
            pairs.Add(("thirdPartyInvolved", motor.ThirdPartyInvolved));
            pairs.Add(("injuryIndicator", motor.InjuryIndicator));
            pairs.Add(("policeReportStatus", motor.PoliceReportStatus));
            pairs.Add(("preferredCommunicationChannel", motor.PreferredCommunicationChannel));
        }

        if (medicalDetails is { } medical)
        {
            pairs.Add(("patientName", medical.PatientName));
            pairs.Add(("relationshipToPolicyholder", medical.RelationshipToPolicyholder));
            pairs.Add(("treatmentDate", medical.TreatmentDate));
            pairs.Add(("providerName", medical.ProviderName));
            pairs.Add(("admissionType", medical.AdmissionType));
            pairs.Add(("treatingDoctor", medical.TreatingDoctor));
            pairs.Add(("diagnosisSummary", medical.DiagnosisSummary));
            pairs.Add(("preferredCommunicationChannel", medical.PreferredCommunicationChannel));
        }

        return pairs
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value!.Trim()));
    }

    /// <summary>Builds the Motor FNOL record from field values, so a claim filed through the
    /// dynamic form still feeds injury detection and routing (which read the record). Only built-in
    /// keys are read; a tenant's own fields never affect routing.</summary>
    public static MotorFnolDetails? ToMotorFnol(IReadOnlyDictionary<string, string> values, MotorFnolDetails? existing)
    {
        var any = values.Keys.Any(key => BuiltInClaimFields.Motor.Any(field => string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase)));
        if (!any)
        {
            return existing;
        }

        string? Get(string key) => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
        return new MotorFnolDetails(
            Get("vehicleRegistration") ?? existing?.VehicleRegistration,
            Get("accidentTime") ?? existing?.AccidentTime,
            Get("accidentLocation") ?? existing?.AccidentLocation,
            Get("driverName") ?? existing?.DriverName,
            Get("drivingPermitNumber") ?? existing?.DrivingPermitNumber,
            Get("thirdPartyInvolved") ?? existing?.ThirdPartyInvolved,
            Get("injuryIndicator") ?? existing?.InjuryIndicator,
            Get("policeReportStatus") ?? existing?.PoliceReportStatus,
            Get("preferredCommunicationChannel") ?? existing?.PreferredCommunicationChannel,
            existing?.UploadedDocuments);
    }

    public static MedicalFnolDetails? ToMedicalFnol(IReadOnlyDictionary<string, string> values, MedicalFnolDetails? existing)
    {
        var any = values.Keys.Any(key => BuiltInClaimFields.Medical.Any(field => string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase)));
        if (!any)
        {
            return existing;
        }

        string? Get(string key) => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
        return new MedicalFnolDetails(
            Get("patientName") ?? existing?.PatientName,
            Get("relationshipToPolicyholder") ?? existing?.RelationshipToPolicyholder,
            Get("treatmentDate") ?? existing?.TreatmentDate,
            Get("providerName") ?? existing?.ProviderName,
            Get("admissionType") ?? existing?.AdmissionType,
            Get("treatingDoctor") ?? existing?.TreatingDoctor,
            Get("diagnosisSummary") ?? existing?.DiagnosisSummary,
            Get("preferredCommunicationChannel") ?? existing?.PreferredCommunicationChannel,
            existing?.UploadedDocuments);
    }
}

/// <summary>Checks submitted field values against the definitions for the claim. Pure.</summary>
public static class ClaimFieldValidator
{
    public sealed record Result(IReadOnlyDictionary<string, string> Values, IReadOnlyDictionary<string, string> Errors)
    {
        public bool IsValid => Errors.Count == 0;
    }

    /// <param name="requireRequired">Whether a missing required field is an error. True when a claim
    /// is first filed; false when someone is saving part of the details.</param>
    /// <param name="audience">Customer: only customer-visible fields are accepted and unknown or
    /// staff-only keys are refused. Staff: every field is accepted.</param>
    public static Result Validate(
        IReadOnlyList<ClaimFieldDefinition> definitions,
        IReadOnlyDictionary<string, string>? submitted,
        bool requireRequired,
        bool customerAudience)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var byKey = definitions.ToDictionary(definition => definition.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var (rawKey, rawValue) in submitted ?? new Dictionary<string, string>())
        {
            if (!byKey.TryGetValue(rawKey, out var definition) || (customerAudience && !definition.CustomerVisible))
            {
                errors[rawKey] = "That detail is not asked for on this claim.";
                continue;
            }

            var value = rawValue?.Trim() ?? string.Empty;
            if (value.Length == 0)
            {
                continue;
            }

            var problem = Check(definition, value, out var normalized);
            if (problem is not null)
            {
                errors[definition.Key] = problem;
                continue;
            }

            values[definition.Key] = normalized;
        }

        if (requireRequired)
        {
            foreach (var definition in definitions.Where(definition => definition.Required && (!customerAudience || definition.CustomerVisible)))
            {
                if (!values.ContainsKey(definition.Key) && !errors.ContainsKey(definition.Key))
                {
                    errors[definition.Key] = $"{definition.Label} is needed.";
                }
            }
        }

        return new Result(values, errors);
    }

    private static string? Check(ClaimFieldDefinition definition, string value, out string normalized)
    {
        normalized = value;
        if (value.Length > 2000)
        {
            return $"{definition.Label} is too long.";
        }

        switch (definition.Type)
        {
            case ClaimFieldType.Text:
                return null;
            case ClaimFieldType.Number:
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    return $"{definition.Label} must be a number.";
                }

                normalized = number.ToString(CultureInfo.InvariantCulture);
                return null;
            case ClaimFieldType.Currency:
                if (!decimal.TryParse(value.Replace(",", string.Empty), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount < 0)
                {
                    return $"{definition.Label} must be an amount, for example 1500000.";
                }

                normalized = amount.ToString(CultureInfo.InvariantCulture);
                return null;
            case ClaimFieldType.Date:
                if (!DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    return $"{definition.Label} must be a date.";
                }

                normalized = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                return null;
            case ClaimFieldType.Phone:
                var digits = Regex.Replace(value, "[^0-9+]", string.Empty);
                if (digits.Count(char.IsDigit) is < 7 or > 15)
                {
                    return $"{definition.Label} must be a phone number.";
                }

                normalized = digits;
                return null;
            case ClaimFieldType.YesNo:
                if (string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                {
                    normalized = "Yes";
                    return null;
                }

                if (string.Equals(value, "no", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
                {
                    normalized = "No";
                    return null;
                }

                return $"{definition.Label} must be yes or no.";
            case ClaimFieldType.Select:
                var option = definition.Options?.FirstOrDefault(candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase));
                if (option is null)
                {
                    return $"{definition.Label} must be one of: {string.Join(", ", definition.Options ?? [])}.";
                }

                normalized = option;
                return null;
            default:
                return null;
        }
    }
}
