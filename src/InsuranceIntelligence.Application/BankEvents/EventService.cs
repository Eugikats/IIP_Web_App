using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using InsuranceIntelligence.Application.Workspaces;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.BankEvents;

/// <summary>
/// The workspace's bank events: recorded by a person, or read from a file the bank supplies. An event is stored once however
/// many times it arrives (a file sent again changes nothing), only the few safe details survive, and a date in the future is
/// refused. What to do about an event is the rules' business, not this service's.
/// </summary>
public sealed class EventService(IWorkspaceRecordStore store, TimeProvider clock)
{
    public const string Kind = "event";
    public const int MaxImportRows = 5000;
    public const int MaxImportBytes = 5 * 1024 * 1024;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<EventRecordResult> RecordAsync(
        TenantScope scope, EventInput input, EventSource source, string? sourceReference, string actorId, CancellationToken cancellationToken = default)
    {
        var made = Build(input, source, sourceReference, actorId);
        var existing = await store.GetAsync(scope, Kind, made.EventId, cancellationToken);
        if (existing is not null && Read(existing.PayloadJson) is { } already)
        {
            return new EventRecordResult(already, Created: false);
        }

        await store.SaveAsync(scope, Kind, made.EventId, JsonSerializer.Serialize(made, Json), cancellationToken);
        return new EventRecordResult(made, Created: true);
    }

    public async Task<IReadOnlyList<WorkspaceEvent>> ListAsync(
        TenantScope scope, EventProcessingState? state = null, string? eventType = null, int take = 200, CancellationToken cancellationToken = default)
    {
        var all = (await store.ListAsync(scope, Kind, 5000, cancellationToken))
            .Select(record => Read(record.PayloadJson))
            .Where(item => item is not null)
            .Select(item => item!)
            .Where(item => state is null || item.State == state)
            .Where(item => string.IsNullOrWhiteSpace(eventType) || item.EventType == eventType)
            .OrderByDescending(item => item.OccurredAt)
            .ThenBy(item => item.EventId, StringComparer.Ordinal)
            .Take(Math.Clamp(take, 1, 1000))
            .ToArray();
        return all;
    }

    public async Task<WorkspaceEvent?> GetAsync(TenantScope scope, string eventId, CancellationToken cancellationToken = default)
    {
        var record = await store.GetAsync(scope, Kind, eventId, cancellationToken);
        return record is null ? null : Read(record.PayloadJson);
    }

    /// <summary>Marks an event as looked at by the rules, so it is not looked at twice.</summary>
    public async Task MarkProcessedAsync(TenantScope scope, string eventId, CancellationToken cancellationToken = default)
    {
        var current = await GetAsync(scope, eventId, cancellationToken);
        if (current is null || current.State == EventProcessingState.Processed)
        {
            return;
        }

        var done = current with { State = EventProcessingState.Processed, ProcessedAt = clock.GetUtcNow() };
        await store.SaveAsync(scope, Kind, eventId, JsonSerializer.Serialize(done, Json), cancellationToken);
    }

    // ---- File import

    private static readonly Dictionary<string, string> ColumnNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["external_id"] = "external_id", ["id"] = "external_id", ["event_id"] = "external_id", ["reference"] = "external_id",
        ["phone"] = "phone", ["msisdn"] = "phone", ["mobile"] = "phone", ["phone_number"] = "phone",
        ["event_type"] = "event_type", ["event"] = "event_type", ["type"] = "event_type",
        ["occurred_at"] = "occurred_at", ["date"] = "occurred_at", ["event_date"] = "occurred_at",
        ["amount"] = "amount", ["loan_amount"] = "amount",
        ["term_months"] = "term_months", ["term"] = "term_months",
        ["product_code"] = "product_code", ["product"] = "product_code",
        ["branch"] = "branch",
    };

    /// <summary>
    /// Reads a CSV of events. The first row names the columns; columns this service does not accept (a balance, an account
    /// number) are not read and are named in the result so the bank sees what was left out. A bad row is reported with its
    /// row number and the rest still load.
    /// </summary>
    public async Task<EventImportResult> ImportCsvAsync(
        TenantScope scope, string fileName, string csv, string actorId, CancellationToken cancellationToken = default)
    {
        if (Encoding.UTF8.GetByteCount(csv) > MaxImportBytes)
        {
            throw new EventException("The file is larger than 5 MB. Send it in parts.");
        }

        var rows = Csv.Parse(csv);
        if (rows.Count == 0)
        {
            throw new EventException("The file is empty.");
        }

        var header = rows[0].Select(name => name.Trim()).ToArray();
        var columns = new Dictionary<string, int>(StringComparer.Ordinal);
        var ignored = new List<string>();
        for (var index = 0; index < header.Length; index++)
        {
            if (header[index].Length == 0)
            {
                continue;
            }

            if (ColumnNames.TryGetValue(header[index], out var canonical))
            {
                columns.TryAdd(canonical, index);
            }
            else
            {
                ignored.Add(header[index]);
            }
        }

        foreach (var required in new[] { "phone", "event_type", "occurred_at" })
        {
            if (!columns.ContainsKey(required))
            {
                throw new EventException($"The file needs a column for {required.Replace('_', ' ')}. Accepted names: {string.Join(", ", ColumnNames.Where(pair => pair.Value == required).Select(pair => pair.Key))}.");
            }
        }

        var dataRows = rows.Skip(1).Where(row => row.Any(cell => !string.IsNullOrWhiteSpace(cell))).ToList();
        if (dataRows.Count > MaxImportRows)
        {
            throw new EventException($"The file has {dataRows.Count} rows. At most {MaxImportRows} are read at a time.");
        }

        var created = 0;
        var duplicates = 0;
        var rejected = new List<EventRowProblem>();
        for (var index = 0; index < dataRows.Count; index++)
        {
            var rowNumber = index + 2;
            var row = dataRows[index];
            string Cell(string column) => columns.TryGetValue(column, out var at) && at < row.Count ? row[at].Trim() : string.Empty;
            try
            {
                var input = new EventInput(
                    Cell("event_type"),
                    Cell("phone"),
                    ParseDate(Cell("occurred_at")),
                    NullIfEmpty(Cell("external_id")),
                    ParseDecimal(Cell("amount"), "amount"),
                    ParseInt(Cell("term_months"), "term months"),
                    NullIfEmpty(Cell("product_code")),
                    NullIfEmpty(Cell("branch")));
                var result = await RecordAsync(scope, input, EventSource.FileImport, $"{fileName} row {rowNumber}", actorId, cancellationToken);
                if (result.Created) created++; else duplicates++;
            }
            catch (EventException problem)
            {
                rejected.Add(new EventRowProblem(rowNumber, problem.Message));
            }
        }

        return new EventImportResult(dataRows.Count, created, duplicates, rejected, ignored);
    }

    // ---- Checking one event

    private WorkspaceEvent Build(EventInput input, EventSource source, string? sourceReference, string actorId)
    {
        var type = BankEventCatalog.Find(input.EventType)
            ?? throw new EventException($"Unknown event type '{input.EventType}'. Use one of: {string.Join(", ", BankEventCatalog.All.Select(item => item.Code))}.");
        var phone = NormalisePhone(input.Phone);
        if (input.OccurredAt is null)
        {
            throw new EventException("The date of the event is required.");
        }

        var now = clock.GetUtcNow();
        if (input.OccurredAt.Value > now.AddHours(1))
        {
            throw new EventException("The date of the event is in the future.");
        }

        if (input.OccurredAt.Value < now.AddYears(-2))
        {
            throw new EventException("The event is more than two years old.");
        }

        var details = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (input.Amount is { } amount)
        {
            if (amount < 0 || amount > 100_000_000_000m)
            {
                throw new EventException("The amount is not a sensible figure.");
            }

            details["amount"] = amount.ToString("0.##", CultureInfo.InvariantCulture);
        }

        if (input.TermMonths is { } term)
        {
            if (term is < 1 or > 600)
            {
                throw new EventException("The term must be between 1 and 600 months.");
            }

            details["term_months"] = term.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(input.ProductCode))
        {
            details["product_code"] = Short(input.ProductCode, "product code");
        }

        if (!string.IsNullOrWhiteSpace(input.Branch))
        {
            details["branch"] = Short(input.Branch, "branch");
        }

        var occurred = input.OccurredAt.Value.ToUniversalTime();
        var id = IdOf(type.Code, phone, occurred, input.ExternalId);
        return new WorkspaceEvent(id, type.Code, phone, occurred, now, source, sourceReference, details, actorId);
    }

    /// <summary>Digits only, with Uganda's country code added to a local number. Refuses what cannot be a mobile number.</summary>
    public static string NormalisePhone(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length == 10 && digits[0] == '0')
        {
            digits = "256" + digits[1..];
        }
        else if (digits.Length == 9)
        {
            digits = "256" + digits;
        }

        if (digits.Length is < 11 or > 15)
        {
            throw new EventException("The phone number is not usable. Give it with the country code, for example +256 7XX XXX XXX.");
        }

        return digits;
    }

    /// <summary>The same as <see cref="NormalisePhone"/> but null for something that is not a number, for filters.</summary>
    public static string? NormalisePhoneOrNull(string? value)
    {
        try
        {
            return NormalisePhone(value);
        }
        catch (EventException)
        {
            return null;
        }
    }

    private static string IdOf(string type, string phone, DateTimeOffset occurred, string? externalId)
    {
        var key = $"{type}|{phone}|{occurred:yyyy-MM-dd}|{externalId?.Trim()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..24].ToLowerInvariant();
    }

    private static string Short(string value, string what)
    {
        var clean = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return clean.Length <= 60 ? clean : throw new EventException($"The {what} is too long.");
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTimeOffset? ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string[] formats = ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ssK", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy"];
        if (DateTimeOffset.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed;
        }

        throw new EventException($"The date '{value}' is not understood. Use 2026-10-27 or 27/10/2026.");
    }

    private static decimal? ParseDecimal(string value, string what)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Replace(",", string.Empty).Replace("UGX", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new EventException($"The {what} '{value}' is not a number.");
    }

    private static int? ParseInt(string value, string what)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new EventException($"The {what} '{value}' is not a whole number.");
    }

    private static WorkspaceEvent? Read(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<WorkspaceEvent>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>A small CSV reader: commas, quoted cells with commas, doubled quotes and line breaks inside quotes.</summary>
internal static class Csv
{
    public static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        text = text.TrimStart('﻿');
        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            if (quoted)
            {
                if (character == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else if (character == '"')
                {
                    quoted = false;
                }
                else
                {
                    cell.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    quoted = true;
                    break;
                case ',':
                    row.Add(cell.ToString());
                    cell.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    row.Add(cell.ToString());
                    cell.Clear();
                    rows.Add(row);
                    row = [];
                    break;
                default:
                    cell.Append(character);
                    break;
            }
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }

        return rows;
    }
}
