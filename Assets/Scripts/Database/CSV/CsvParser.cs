using System;
using System.Collections.Generic;
using System.Linq;
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

        public static readonly string[] CharacterHeaders =
        {
            "Id", "CharacterName", "MaxLife", "MaxMana", "Movespeed", "Damage", "AttackRange", "AttackTimeInterval", "DefaultSkillIds", "Function", "Wealth", "ExtraItemIds"
        };

        public static AdventurerFunction ParseAdventurerFunction(IReadOnlyDictionary<string, string> row)
        {
            if (row != null &&
                row.TryGetValue("Function", out var funcStr) &&
                !string.IsNullOrWhiteSpace(funcStr) &&
                Enum.TryParse<AdventurerFunction>(funcStr.Trim(), true, out var parsedFunc) &&
                Enum.IsDefined(typeof(AdventurerFunction), parsedFunc))
            {
                return parsedFunc;
            }

            return AdventurerFunction.None;
        }

        public static AdventurerWealth ParseAdventurerWealth(IReadOnlyDictionary<string, string> row)
        {
            if (row != null &&
                row.TryGetValue("Wealth", out var wealthStr) &&
                !string.IsNullOrWhiteSpace(wealthStr) &&
                Enum.TryParse<AdventurerWealth>(wealthStr.Trim(), true, out var parsedWealth) &&
                Enum.IsDefined(typeof(AdventurerWealth), parsedWealth))
            {
                return parsedWealth;
            }

            return AdventurerWealth.Average;
        }

        public static Dictionary<string, string> BuildCharacterCsvRow(CharacterEntry c)
        {
            string extraItems = string.Empty;
            if (c?.extraItemIds != null && c.extraItemIds.Count > 0)
            {
                extraItems = string.Join(";", c.extraItemIds);
            }
            else if (c?.baseStats != null && c.baseStats.ExtraItemIds != null && c.baseStats.ExtraItemIds.Count > 0)
            {
                extraItems = string.Join(";", c.baseStats.ExtraItemIds);
            }

            return new Dictionary<string, string>
            {
                ["Id"] = c?.id ?? string.Empty,
                ["CharacterName"] = c?.characterName ?? string.Empty,
                ["MaxLife"] = c?.baseStats != null ? c.baseStats.MaxLife.ToString("F1") : "100",
                ["MaxMana"] = c?.baseStats != null ? c.baseStats.MaxMana.ToString("F1") : "50",
                ["Movespeed"] = c?.baseStats != null ? c.baseStats.Movespeed.ToString("F1") : "5",
                ["Damage"] = c?.baseStats != null ? c.baseStats.Damage.ToString("F1") : "10",
                ["AttackRange"] = c?.baseStats != null ? c.baseStats.AttackRange.ToString("F1") : "1",
                ["AttackTimeInterval"] = c?.baseStats != null ? c.baseStats.AttackTimeInterval.ToString("F1") : "1",
                ["DefaultSkillIds"] = c?.defaultSkillIds != null ? string.Join(";", c.defaultSkillIds) : string.Empty,
                ["Function"] = c?.baseStats != null ? c.baseStats.characterFunction.ToString() : AdventurerFunction.None.ToString(),
                ["Wealth"] = c?.baseStats != null ? c.baseStats.characterWealth.ToString() : AdventurerWealth.Average.ToString(),
                ["ExtraItemIds"] = extraItems
            };
        }

        public static void ApplyCharacterCsvRowToStats(IReadOnlyDictionary<string, string> row, CharacterBaseStatSO baseStats)
        {
            if (row == null || baseStats == null) return;

            if (row.TryGetValue("MaxLife", out var hpStr) && float.TryParse(hpStr, out var hp)) baseStats.MaxLife = hp;
            if (row.TryGetValue("MaxMana", out var manaStr) && float.TryParse(manaStr, out var mana)) baseStats.MaxMana = mana;
            if (row.TryGetValue("Movespeed", out var spdStr) && float.TryParse(spdStr, out var spd)) baseStats.Movespeed = spd;
            if (row.TryGetValue("Damage", out var dmgStr) && float.TryParse(dmgStr, out var dmg)) baseStats.Damage = dmg;
            if (row.TryGetValue("AttackRange", out var rangeStr) && float.TryParse(rangeStr, out var range)) baseStats.AttackRange = range;
            if (row.TryGetValue("AttackTimeInterval", out var atiStr) && float.TryParse(atiStr, out var ati)) baseStats.AttackTimeInterval = ati;
            baseStats.characterFunction = ParseAdventurerFunction(row);
            baseStats.characterWealth = ParseAdventurerWealth(row);

            if (row.TryGetValue("ExtraItemIds", out var extraStr))
            {
                baseStats.extraItemIds = extraStr
                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .ToList();
            }
        }
    }
}
