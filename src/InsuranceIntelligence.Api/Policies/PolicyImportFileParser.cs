using ClosedXML.Excel;

namespace InsuranceIntelligence.Api.Policies;

public sealed record ParsedImportFile(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows, bool Truncated);

public sealed class UnsupportedImportFileException(string message) : Exception(message);

/// <summary>Outcome N: "upload an export of policies... as CSV or Excel." Both formats resolve to
/// the same shape (a header row plus data rows of plain strings) so everything downstream, column
/// mapping, validation, import, never needs to know which format a file arrived in.</summary>
public static class PolicyImportFileParser
{
    public const int MaxRows = 5000;

    public static ParsedImportFile Parse(string fileName, Stream content)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".csv" => ParseCsv(content),
            ".xlsx" => ParseXlsx(content),
            _ => throw new UnsupportedImportFileException($"Unsupported file type '{extension}'. Upload a .csv or .xlsx file."),
        };
    }

    private static ParsedImportFile ParseXlsx(Stream content)
    {
        using var workbook = new XLWorkbook(content);
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new UnsupportedImportFileException("This workbook has no worksheets.");
        var usedRange = sheet.RangeUsed();
        if (usedRange is null)
        {
            return new ParsedImportFile([], [], false);
        }

        var rowsUsed = usedRange.RowsUsed().ToList();
        if (rowsUsed.Count == 0)
        {
            return new ParsedImportFile([], [], false);
        }

        var headerRow = rowsUsed[0];
        var columnCount = usedRange.ColumnCount();
        var headers = Enumerable.Range(1, columnCount).Select(column => CellText(headerRow.Cell(column))).ToArray();

        var truncated = rowsUsed.Count - 1 > MaxRows;
        var rows = rowsUsed.Skip(1).Take(MaxRows)
            .Select(row => (IReadOnlyList<string>)Enumerable.Range(1, columnCount).Select(column => CellText(row.Cell(column))).ToArray())
            .ToArray();

        return new ParsedImportFile(headers, rows, truncated);
    }

    private static string CellText(IXLCell cell) => cell.GetFormattedString().Trim();

    private static ParsedImportFile ParseCsv(Stream content)
    {
        using var reader = new StreamReader(content);
        var allRows = new List<IReadOnlyList<string>>();
        var field = new System.Text.StringBuilder();
        var row = new List<string>();
        var inQuotes = false;
        var sawAnyContent = false;

        void EndField()
        {
            row.Add(field.ToString());
            field.Clear();
        }

        void EndRow()
        {
            EndField();
            if (row.Count > 1 || !string.IsNullOrEmpty(row[0]))
            {
                allRows.Add(row.ToArray());
            }

            row = [];
        }

        int current;
        while ((current = reader.Read()) != -1)
        {
            var ch = (char)current;
            sawAnyContent = true;
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        reader.Read();
                        field.Append('"');
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(ch);
                }

                continue;
            }

            switch (ch)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    EndField();
                    break;
                case '\r':
                    break;
                case '\n':
                    EndRow();
                    break;
                default:
                    field.Append(ch);
                    break;
            }
        }

        if (sawAnyContent && (field.Length > 0 || row.Count > 0))
        {
            EndRow();
        }

        if (allRows.Count == 0)
        {
            return new ParsedImportFile([], [], false);
        }

        var headers = allRows[0];
        var truncated = allRows.Count - 1 > MaxRows;
        var rows = allRows.Skip(1).Take(MaxRows).ToArray();
        return new ParsedImportFile(headers, rows, truncated);
    }
}
