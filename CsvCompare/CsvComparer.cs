using System.Globalization;

namespace CsvCompare
{
    /// <summary>
    /// Compares two CSV files (expected vs actual) using specified key columns.
    /// </summary>
    public class CsvComparer
    {
        private readonly CsvComparerOptions _options;

        public CsvComparer(CsvComparerOptions options = null)
        {
            _options = options ?? new CsvComparerOptions();
        }

        /// <summary>
        /// Compare two CSV streams. Returns ComparisonResult.
        /// </summary>
        /// <param name="expectedReader">StreamReader for expected CSV</param>
        /// <param name="actualReader">StreamReader for actual CSV</param>
        /// <param name="keyColumns">List of header names that form the primary/composite key</param>
        public ComparisonResult Compare(TextReader expectedReader, TextReader actualReader, List<string> keyColumns)
        {
            if (expectedReader == null) throw new ArgumentNullException(nameof(expectedReader));
            if (actualReader == null) throw new ArgumentNullException(nameof(actualReader));
            if (keyColumns == null || keyColumns.Count == 0) throw new ArgumentException("At least one key column must be specified.", nameof(keyColumns));

            var expectedParser = new CsvParser(expectedReader);
            var actualParser = new CsvParser(actualReader);

            var expectedHeaders = expectedParser.ReadHeader();
            var actualHeaders = actualParser.ReadHeader();

            // Validate header counts and names
            if (expectedHeaders.Count != actualHeaders.Count || !expectedHeaders.SequenceEqual(actualHeaders))
            {
                // For this implementation, we require same headers and same count.
                // If not same, throw to indicate precondition violation.
                throw new InvalidDataException("CSV headers differ between expected and actual. Both files must have same headers and same field count.");
            }

            // Build dictionary for expected records keyed by composite key
            var expectedDict = new Dictionary<string, CsvRecord>(StringComparer.Ordinal);
            foreach (var rec in expectedParser.ReadRecords(expectedHeaders))
            {
                string key = BuildKey(rec, keyColumns);
                if (expectedDict.ContainsKey(key))
                {
                    // Duplicate key in expected file; choose last or throw. We'll throw to highlight data issue.
                    throw new InvalidDataException($"Duplicate key '{key}' found in expected CSV.");
                }
                expectedDict[key] = rec;
            }

            // Build dictionary for actual records
            var actualDict = new Dictionary<string, CsvRecord>(StringComparer.Ordinal);
            foreach (var rec in actualParser.ReadRecords(actualHeaders))
            {
                string key = BuildKey(rec, keyColumns);
                if (actualDict.ContainsKey(key))
                {
                    throw new InvalidDataException($"Duplicate key '{key}' found in actual CSV.");
                }
                actualDict[key] = rec;
            }

            var result = new ComparisonResult();

            // Keys in expected but not in actual
            foreach (var k in expectedDict.Keys)
            {
                if (!actualDict.ContainsKey(k))
                    result.MissingInActual.Add(k);
            }

            // Keys in actual but not in expected
            foreach (var k in actualDict.Keys)
            {
                if (!expectedDict.ContainsKey(k))
                    result.ExtraInActual.Add(k);
            }

            // Keys present in both: field-level comparison
            var commonKeys = expectedDict.Keys.Intersect(actualDict.Keys);
            foreach (var k in commonKeys)
            {
                var expectedRec = expectedDict[k];
                var actualRec = actualDict[k];
                var mismatch = new RecordMismatch { Key = k };

                foreach (var header in expectedHeaders)
                {
                    if (_options.IgnoreFields.Contains(header))
                        continue;

                    string expectedVal = expectedRec.GetValue(header) ?? string.Empty;
                    string actualVal = actualRec.GetValue(header) ?? string.Empty;

                    // If header is part of key, skip comparing it (optional)
                    if (keyColumns.Contains(header))
                        continue;

                    bool equal = CompareValues(header, expectedVal, actualVal);

                    if (!equal)
                    {
                        mismatch.FieldMismatches.Add(new FieldMismatch
                        {
                            FieldName = header,
                            ExpectedValue = expectedVal,
                            ActualValue = actualVal
                        });
                    }
                }

                if (mismatch.FieldMismatches.Count > 0)
                    result.FieldLevelMismatches.Add(mismatch);
            }

            return result;
        }

        private bool CompareValues(string fieldName, string expectedVal, string actualVal)
        {
            // Custom comparator
            if (_options.CustomComparators.TryGetValue(fieldName, out var comparator))
            {
                return comparator(expectedVal, actualVal);
            }

            // Case-insensitive option
            var stringComparison = _options.CaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            // If both are empty or whitespace, consider equal
            if (string.IsNullOrWhiteSpace(expectedVal) && string.IsNullOrWhiteSpace(actualVal))
                return true;

            // Try numeric comparison if both parse as double and tolerance is set
            if (_options.NumericTolerance.HasValue)
            {
                if (double.TryParse(expectedVal, NumberStyles.Any, CultureInfo.InvariantCulture, out var d1) &&
                    double.TryParse(actualVal, NumberStyles.Any, CultureInfo.InvariantCulture, out var d2))
                {
                    return Math.Abs(d1 - d2) <= _options.NumericTolerance.Value;
                }
            }

            // Default string comparison
            return string.Equals(expectedVal?.Trim(), actualVal?.Trim(), stringComparison);
        }

        private string BuildKey(CsvRecord rec, List<string> keyColumns)
        {
            var parts = new List<string>();
            foreach (var k in keyColumns)
            {
                var v = rec.GetValue(k) ?? string.Empty;
                parts.Add(v.Trim());
            }
            // Use a delimiter unlikely to appear in keys
            return string.Join("||", parts);
        }
    }
}