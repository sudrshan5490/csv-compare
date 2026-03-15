using System.Text;

namespace CsvCompare.Reporting
{
    /// <summary>
    /// Writes comparison results to CSV and JSON files.
    /// Uses only System.* APIs (no external libraries).
    /// </summary>
    public static class MismatchReportWriter
    {
        /// <summary>
        /// Writes a CSV summary of mismatches.
        /// Columns: Type,Key,KeyColumns,KeyValues,Field,Scope,Expected,Actual
        /// Type = MISSING | EXTRA | FIELD_MISMATCH
        /// </summary>
        public static void WriteCsvSummary(string path, ComparisonResult result)
        {
            using var sw = new StreamWriter(path, false, Encoding.UTF8, 65536);
            sw.WriteLine("Type,Key,KeyColumns,KeyValues,Field,Scope,Expected,Actual");
            foreach (var k in result.MissingInActual)
            {
                sw.WriteLine(EscapeCsv("MISSING") + "," + EscapeCsv(k) + ",,, , ,");
            }
            foreach (var k in result.ExtraInActual)
            {
                sw.WriteLine(EscapeCsv("EXTRA") + "," + EscapeCsv(k) + ",,, , ,");
            }
            foreach (var rec in result.FieldLevelMismatches)
            {
                foreach (var fm in rec.FieldMismatches)
                {
                    string keyCols = fm.KeyColumns != null ? string.Join(";", fm.KeyColumns) : "";
                    string keyVals = fm.KeyValues != null ? string.Join(";", fm.KeyValues) : "";
                    sw.WriteLine(EscapeCsv("FIELD_MISMATCH") + "," + EscapeCsv(rec.Key) + "," +
                                 EscapeCsv(keyCols) + "," + EscapeCsv(keyVals) + "," +
                                 EscapeCsv(fm.FieldName) + "," + EscapeCsv(fm.Scope) + "," +
                                 EscapeCsv(fm.ExpectedValue) + "," + EscapeCsv(fm.ActualValue));
                }
            }
        }

        /// <summary>
        /// Writes a simple JSON summary. Not a full JSON serializer but sufficient for this structure.
        /// </summary>
        public static void WriteJsonSummary(string path, ComparisonResult result)
        {
            using var sw = new StreamWriter(path, false, Encoding.UTF8, 65536);
            sw.WriteLine("{");
            sw.WriteLine($"  \"MissingInActual\": [{JoinQuoted(result.MissingInActual)}],");
            sw.WriteLine($"  \"ExtraInActual\": [{JoinQuoted(result.ExtraInActual)}],");
            sw.WriteLine("  \"FieldLevelMismatches\": [");
            for (int i = 0; i < result.FieldLevelMismatches.Count; i++)
            {
                var rec = result.FieldLevelMismatches[i];
                sw.WriteLine("    {");
                sw.WriteLine($"      \"Key\": {Quote(rec.Key)},");
                sw.WriteLine("      \"Mismatches\": [");
                for (int j = 0; j < rec.FieldMismatches.Count; j++)
                {
                    var fm = rec.FieldMismatches[j];
                    sw.WriteLine("        {");
                    sw.WriteLine($"          \"FieldName\": {Quote(fm.FieldName)},");
                    sw.WriteLine($"          \"Scope\": {Quote(fm.Scope)},");
                    sw.WriteLine($"          \"KeyColumns\": {JsonArray(fm.KeyColumns)},");
                    sw.WriteLine($"          \"KeyValues\": {JsonArray(fm.KeyValues)},");
                    sw.WriteLine($"          \"ExpectedValue\": {Quote(fm.ExpectedValue)},");
                    sw.WriteLine($"          \"ActualValue\": {Quote(fm.ActualValue)}");
                    sw.Write("        }");
                    sw.WriteLine(j < rec.FieldMismatches.Count - 1 ? "," : "");
                }
                sw.WriteLine("      ]");
                sw.Write("    }");
                sw.WriteLine(i < result.FieldLevelMismatches.Count - 1 ? "," : "");
            }
            sw.WriteLine("  ]");
            sw.WriteLine("}");
        }

        private static string EscapeCsv(string s)
        {
            if (s == null) return "";
            if (s.IndexOfAny(new char[] { ',', '"', '\r', '\n' }) >= 0)
            {
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            }
            return s;
        }

        private static string Quote(string s)
        {
            if (s == null) return "null";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
        }

        private static string JoinQuoted(List<string> list)
        {
            if (list == null || list.Count == 0) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                sb.Append(Quote(list[i]));
                if (i < list.Count - 1) sb.Append(", ");
            }
            return sb.ToString();
        }

        private static string JsonArray(List<string> list)
        {
            if (list == null) return "[]";
            var sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0; i < list.Count; i++)
            {
                sb.Append(Quote(list[i]));
                if (i < list.Count - 1) sb.Append(", ");
            }
            sb.Append("]");
            return sb.ToString();
        }
    }
}