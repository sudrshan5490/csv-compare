using System.Text;

namespace CsvCompare
{
    /// <summary>
    /// Represents a CSV record (row) as a mapping from header to value.
    /// </summary>
    public class CsvRecord
    {
        public Dictionary<string, string> Fields { get; }

        public CsvRecord(IEnumerable<string> headers)
        {
            Fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var h in headers)
                Fields[h] = string.Empty;
        }

        public CsvRecord(Dictionary<string, string> fields)
        {
            Fields = new Dictionary<string, string>(fields, StringComparer.Ordinal);
        }

        public string GetValue(string header)
        {
            if (Fields.TryGetValue(header, out var v))
                return v;
            return null;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var kv in Fields)
            {
                sb.Append($"{kv.Key}={kv.Value};");
            }
            return sb.ToString();
        }
    }
}