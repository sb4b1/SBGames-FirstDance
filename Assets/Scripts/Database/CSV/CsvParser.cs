using System;
using System.Collections.Generic;
using System.Text;

namespace SBGames.Database.CSV
{
    /// <summary>
    /// Robust, zero-dependency CSV parser and serializer compliant with RFC-4180.
    /// Handles quoted fields, escaped quotes (""), newlines within fields, and header mappings.
    /// </summary>
    public static class CsvParser
    {
        public static List<string[]> ParseRows(string csvContent, char delimiter = ',')
        {
            var rows = new List<string[]>();
            if (string.IsNullOrEmpty(csvContent)) return rows;

            var currentRow = new List<string>();
            var currentField = new StringBuilder();
            bool insideQuotes = false;

            for (int i = 0; i < csvContent.Length; i++)
            {
                char c = csvContent[i];

                if (insideQuotes)
                {
                    if (c == '"')
                    {
                        // Check for escaped quote ("")
                        if (i + 1 < csvContent.Length && csvContent[i + 1] == '"')
                        {
                            currentField.Append('"');
                            i++; // skip next quote
                        }
                        else
                        {
                            insideQuotes = false;
                        }
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        insideQuotes = true;
                    }
                    else if (c == delimiter)
                    {
                        currentRow.Add(currentField.ToString());
                        currentField.Clear();
                    }
                    else if (c == '\r')
                    {
                        if (i + 1 < csvContent.Length && csvContent[i + 1] == '\n')
                        {
                            i++;
                        }
                        currentRow.Add(currentField.ToString());
                        currentField.Clear();
                        rows.Add(currentRow.ToArray());
                        currentRow = new List<string>();
                    }
                    else if (c == '\n')
                    {
                        currentRow.Add(currentField.ToString());
                        currentField.Clear();
                        rows.Add(currentRow.ToArray());
                        currentRow = new List<string>();
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
            }

            // Flush trailing field/row
            if (currentField.Length > 0 || currentRow.Count > 0)
            {
                currentRow.Add(currentField.ToString());
                rows.Add(currentRow.ToArray());
            }

            return rows;
        }

        public static List<Dictionary<string, string>> ParseTable(string csvContent, char delimiter = ',')
        {
            var result = new List<Dictionary<string, string>>();
            var rows = ParseRows(csvContent, delimiter);
            if (rows.Count < 2) return result;

            var headers = rows[0];

            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                for (int c = 0; c < headers.Length; c++)
                {
                    string header = headers[c].Trim();
                    if (string.IsNullOrEmpty(header)) continue;

                    string value = c < row.Length ? row[c] : string.Empty;
                    dict[header] = value;
                }

                result.Add(dict);
            }

            return result;
        }

        public static string SerializeTable(
            IReadOnlyList<string> headers,
            IEnumerable<IReadOnlyDictionary<string, string>> rows,
            char delimiter = ',')
        {
            var sb = new StringBuilder();

            // Write headers
            for (int i = 0; i < headers.Count; i++)
            {
                if (i > 0) sb.Append(delimiter);
                sb.Append(EscapeField(headers[i], delimiter));
            }
            sb.AppendLine();

            // Write rows
            foreach (var row in rows)
            {
                for (int i = 0; i < headers.Count; i++)
                {
                    if (i > 0) sb.Append(delimiter);
                    string header = headers[i];
                    string val = row.TryGetValue(header, out var v) ? v : string.Empty;
                    sb.Append(EscapeField(val, delimiter));
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string EscapeField(string field, char delimiter)
        {
            if (string.IsNullOrEmpty(field)) return string.Empty;

            bool containsDelimiter = field.IndexOf(delimiter) >= 0;
            bool containsQuote = field.IndexOf('"') >= 0;
            bool containsNewline = field.IndexOf('\n') >= 0 || field.IndexOf('\r') >= 0;

            if (containsDelimiter || containsQuote || containsNewline)
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }

            return field;
        }
    }
}
