using System.Text;

namespace CsvCompare
{
    /// <summary>
    /// Simple CSV parser that supports quoted fields, commas inside quotes, escaped quotes (""), and newlines inside quoted fields.
    /// It yields header row and then CsvRecord rows.
    /// </summary>
    public class CsvParser
    {
        private readonly TextReader _reader;
        private readonly char _delimiter;

        public CsvParser(TextReader reader, char delimiter = ',')
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _delimiter = delimiter;
        }

        /// <summary>
        /// Reads the header row and returns the list of headers.
        /// </summary>
        public List<string> ReadHeader()
        {
            var row = ReadRow();
            if (row == null)
                throw new InvalidDataException("CSV has no header row.");
            return new List<string>(row);
        }

        /// <summary>
        /// Reads the next CSV row as list of fields. Returns null at EOF.
        /// </summary>
        public List<string> ReadRow()
        {
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            int ch;
            bool fieldStarted = false;
            while (true)
            {
                ch = _reader.Read();
                if (ch == -1)
                {
                    // EOF
                    if (fieldStarted || sb.Length > 0 || inQuotes)
                    {
                        fields.Add(sb.ToString());
                        return fields;
                    }
                    return fields.Count > 0 ? fields : null;
                }

                char c = (char)ch;
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        int next = _reader.Peek();
                        if (next == '"')
                        {
                            // Escaped quote
                            _reader.Read();
                            sb.Append('"');
                        }
                        else
                        {
                            // End quote
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                        fieldStarted = true;
                    }
                    else if (c == _delimiter)
                    {
                        fields.Add(sb.ToString());
                        sb.Clear();
                        fieldStarted = false;
                    }
                    else if (c == '\r')
                    {
                        // ignore, handle \r\n or lone \r
                        int next = _reader.Peek();
                        if (next == '\n')
                            _reader.Read();
                        fields.Add(sb.ToString());
                        return fields;
                    }
                    else if (c == '\n')
                    {
                        fields.Add(sb.ToString());
                        return fields;
                    }
                    else
                    {
                        sb.Append(c);
                        fieldStarted = true;
                    }
                }
            }
        }

        /// <summary>
        /// Enumerates CsvRecord objects (header must be read first).
        /// </summary>
        public IEnumerable<CsvRecord> ReadRecords(List<string> headers)
        {
            if (headers == null) throw new ArgumentNullException(nameof(headers));
            List<string> row;
            while ((row = ReadRow()) != null)
            {
                // If row has fewer fields, pad with empty strings; if more, keep extras with generated headers.
                var dict = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int i = 0; i < headers.Count; i++)
                {
                    string val = i < row.Count ? row[i] : string.Empty;
                    dict[headers[i]] = val;
                }
                // If row has extra columns beyond headers, add them with generated header names
                for (int i = headers.Count; i < row.Count; i++)
                {
                    dict[$"ExtraColumn{i - headers.Count + 1}"] = row[i];
                }
                yield return new CsvRecord(dict);
            }
        }
    }
}