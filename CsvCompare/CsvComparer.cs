using System.Globalization;
using System.Text;

/// <summary>
/// Compares two CSV files (expected vs actual) using specified key columns.
/// Supports multiple comparison modes (InMemory, StreamExpected, Bucketed),
/// duplicate key policies, header-order flexibility, per-field tolerances, and logging.
/// </summary>
namespace CsvCompare
{
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
        /// <param name="expectedReader">TextReader for expected CSV</param>
        /// <param name="actualReader">TextReader for actual CSV</param>
        /// <param name="keyColumns">List of header names that form the primary/composite key (names refer to expected headers)</param>
        public ComparisonResult Compare(TextReader expectedReader, TextReader actualReader, List<string> keyColumns)
        {
            if (expectedReader == null) throw new ArgumentNullException(nameof(expectedReader));
            if (actualReader == null) throw new ArgumentNullException(nameof(actualReader));
            if (keyColumns == null || keyColumns.Count == 0) throw new ArgumentException("At least one key column must be specified.", nameof(keyColumns));

            // Choose mode
            switch (_options.Mode)
            {
                case ComparisonMode.InMemory:
                    return CompareInMemory(expectedReader, actualReader, keyColumns);
                case ComparisonMode.StreamExpected:
                    return CompareStreamExpected(expectedReader, actualReader, keyColumns);
                case ComparisonMode.Bucketed:
                    return CompareBucketed(expectedReader, actualReader, keyColumns);
                default:
                    throw new InvalidOperationException("Unsupported comparison mode.");
            }
        }

        #region InMemory mode (original behavior, with enhancements)

        private ComparisonResult CompareInMemory(TextReader expectedReader, TextReader actualReader, List<string> keyColumns)
        {
            var expectedParser = new CsvParser(expectedReader, _options.Delimiter);
            var actualParser = new CsvParser(actualReader, _options.Delimiter);

            var expectedHeaders = expectedParser.ReadHeader();
            var actualHeaders = actualParser.ReadHeader();

            // Validate headers (respect RequireHeaderOrder and FieldNameMapping)
            var headerMap = BuildHeaderMap(expectedHeaders, actualHeaders);

            // Build dictionary for expected records keyed by composite key
            var expectedDict = new Dictionary<string, CsvRecord>(StringComparer.Ordinal);
            foreach (var rec in expectedParser.ReadRecords(expectedHeaders))
            {
                string key = BuildKey(rec, keyColumns);
                HandleDuplicateInsert(expectedDict, key, rec, "expected");
            }

            // Build dictionary for actual records
            var actualDict = new Dictionary<string, CsvRecord>(StringComparer.Ordinal);
            foreach (var rec in actualParser.ReadRecords(actualHeaders))
            {
                // If mapping exists, remap actual record fields to expected header names
                var normalized = RemapRecord(rec, headerMap);
                string key = BuildKey(normalized, keyColumns);
                HandleDuplicateInsert(actualDict, key, normalized, "actual");
            }

            return CompareDictionaries(expectedDict, actualDict, expectedHeaders, keyColumns);
        }

        #endregion

        #region StreamExpected mode (load expected, stream actual)

        private ComparisonResult CompareStreamExpected(TextReader expectedReader, TextReader actualReader, List<string> keyColumns)
        {
            var expectedParser = new CsvParser(expectedReader, _options.Delimiter);
            var actualParser = new CsvParser(actualReader, _options.Delimiter);

            var expectedHeaders = expectedParser.ReadHeader();
            var actualHeaders = actualParser.ReadHeader();

            var headerMap = BuildHeaderMap(expectedHeaders, actualHeaders);

            // Load expected into dictionary
            var expectedDict = new Dictionary<string, CsvRecord>(StringComparer.Ordinal);
            foreach (var rec in expectedParser.ReadRecords(expectedHeaders))
            {
                string key = BuildKey(rec, keyColumns);
                HandleDuplicateInsert(expectedDict, key, rec, "expected");
            }

            var result = new ComparisonResult();

            // Track matched keys
            var matched = new HashSet<string>(StringComparer.Ordinal);

            // Stream actual and compare on the fly
            foreach (var rec in actualParser.ReadRecords(actualHeaders))
            {
                var actualRec = RemapRecord(rec, headerMap);
                string key = BuildKey(actualRec, keyColumns);

                if (!expectedDict.TryGetValue(key, out var expectedRec))
                {
                    result.ExtraInActual.Add(key);
                    continue;
                }

                // Compare fields
                var rm = CompareRecordFields(expectedRec, actualRec, expectedHeaders, keyColumns);
                if (rm.FieldMismatches.Count > 0)
                    result.FieldLevelMismatches.Add(rm);

                matched.Add(key);
            }

            // Any expected keys not matched are missing in actual
            foreach (var k in expectedDict.Keys)
            {
                if (!matched.Contains(k))
                    result.MissingInActual.Add(k);
            }

            return result;
        }

        #endregion

        #region Bucketed mode (disk-backed hashing into buckets)

        private ComparisonResult CompareBucketed(TextReader expectedReader, TextReader actualReader, List<string> keyColumns)
        {
            // Create temp directory
            string tempDir = _options.TempDirectory ?? Path.Combine(Path.GetTempPath(), "CsvCompare_Buckets_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            _options.Logger?.Invoke($"Bucketed mode: temp dir = {tempDir}");

            try
            {
                // Prepare bucket writers for expected and actual
                int buckets = Math.Max(2, _options.BucketCount);
                var expectedPaths = new string[buckets];
                var actualPaths = new string[buckets];
                var expectedWriters = new StreamWriter[buckets];
                var actualWriters = new StreamWriter[buckets];

                for (int i = 0; i < buckets; i++)
                {
                    expectedPaths[i] = Path.Combine(tempDir, $"expected_bucket_{i}.csv");
                    actualPaths[i] = Path.Combine(tempDir, $"actual_bucket_{i}.csv");
                    expectedWriters[i] = new StreamWriter(expectedPaths[i], false, Encoding.UTF8, 65536);
                    actualWriters[i] = new StreamWriter(actualPaths[i], false, Encoding.UTF8, 65536);
                }

                // Read headers and write header lines to each bucket file (so each bucket is a valid CSV)
                var expectedParser = new CsvParser(expectedReader, _options.Delimiter);
                var actualParser = new CsvParser(actualReader, _options.Delimiter);
                var expectedHeaders = expectedParser.ReadHeader();
                var actualHeaders = actualParser.ReadHeader();
                var headerMap = BuildHeaderMap(expectedHeaders, actualHeaders);

                string headerLine = string.Join(_options.Delimiter, expectedHeaders);
                for (int i = 0; i < buckets; i++)
                {
                    expectedWriters[i].WriteLine(headerLine);
                    actualWriters[i].WriteLine(headerLine);
                }

                // Distribute expected rows into buckets
                foreach (var rec in expectedParser.ReadRecords(expectedHeaders))
                {
                    string key = BuildKey(rec, keyColumns);
                    int bucket = Math.Abs(key.GetHashCode()) % buckets;
                    expectedWriters[bucket].WriteLine(SerializeRecord(rec, expectedHeaders));
                }

                // Distribute actual rows into buckets (remap headers)
                foreach (var rec in actualParser.ReadRecords(actualHeaders))
                {
                    var normalized = RemapRecord(rec, headerMap);
                    string key = BuildKey(normalized, keyColumns);
                    int bucket = Math.Abs(key.GetHashCode()) % buckets;
                    actualWriters[bucket].WriteLine(SerializeRecord(normalized, expectedHeaders));
                }

                // Close writers
                for (int i = 0; i < buckets; i++)
                {
                    expectedWriters[i].Dispose();
                    actualWriters[i].Dispose();
                }

                // Compare each bucket pair using in-memory approach (but each bucket is small)
                var finalResult = new ComparisonResult();
                for (int i = 0; i < buckets; i++)
                {
                    _options.Logger?.Invoke($"Comparing bucket {i + 1}/{buckets}");
                    using var er = new StreamReader(expectedPaths[i], Encoding.UTF8);
                    using var ar = new StreamReader(actualPaths[i], Encoding.UTF8);
                    var subResult = CompareInMemory(er, ar, keyColumns);
                    // Merge results
                    finalResult.MissingInActual.AddRange(subResult.MissingInActual);
                    finalResult.ExtraInActual.AddRange(subResult.ExtraInActual);
                    finalResult.FieldLevelMismatches.AddRange(subResult.FieldLevelMismatches);
                }

                return finalResult;
            }
            finally
            {
                // Clean up temp directory
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch
                {
                    // ignore cleanup errors
                }
            }
        }

        #endregion

        #region Helpers: header mapping, remap, serialize

        /// <summary>
        /// Build a mapping from expected header name -> actual header name (or same if identical).
        /// Validates header presence and order according to options.
        /// </summary>
        private Dictionary<string, string> BuildHeaderMap(List<string> expectedHeaders, List<string> actualHeaders)
        {
            // If mapping provided in options, use it to map expected->actual
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var eh in expectedHeaders)
            {
                if (_options.FieldNameMapping.TryGetValue(eh, out var mapped))
                {
                    map[eh] = mapped;
                }
                else
                {
                    map[eh] = eh;
                }
            }

            if (_options.RequireHeaderOrder)
            {
                // After mapping, actual headers must match expected headers in order
                var mappedExpected = expectedHeaders.Select(eh => map[eh]).ToList();
                if (mappedExpected.Count != actualHeaders.Count || !mappedExpected.SequenceEqual(actualHeaders))
                    throw new InvalidDataException("CSV headers differ between expected and actual (order required).");
            }
            else
            {
                // Ensure all mapped expected headers exist in actual headers (order not required)
                var actualSet = new HashSet<string>(actualHeaders, StringComparer.Ordinal);
                foreach (var kv in map)
                {
                    if (!actualSet.Contains(kv.Value))
                        throw new InvalidDataException($"Actual CSV missing expected header (or mapped header): {kv.Value}");
                }
            }

            return map;
        }

        /// <summary>
        /// Remap an actual record's fields to expected header names using headerMap.
        /// Returns a new CsvRecord whose Fields keys are expected header names.
        /// </summary>
        private CsvRecord RemapRecord(CsvRecord actualRec, Dictionary<string, string> headerMap)
        {
            // headerMap: expected -> actual
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in headerMap)
            {
                string expectedName = kv.Key;
                string actualName = kv.Value;
                string val = actualRec.GetValue(actualName) ?? string.Empty;
                dict[expectedName] = val;
            }
            return new CsvRecord(dict);
        }

        /// <summary>
        /// Serialize a record to a CSV line using expected headers and delimiter.
        /// </summary>
        private string SerializeRecord(CsvRecord rec, List<string> expectedHeaders)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < expectedHeaders.Count; i++)
            {
                if (i > 0) sb.Append(_options.Delimiter);
                var v = rec.GetValue(expectedHeaders[i]) ?? string.Empty;
                if (v.IndexOfAny(new char[] { _options.Delimiter, '\r', '\n', '"' }) >= 0)
                {
                    sb.Append('"');
                    sb.Append(v.Replace("\"", "\"\""));
                    sb.Append('"');
                }
                else
                {
                    sb.Append(v);
                }
            }
            return sb.ToString();
        }

        #endregion

        #region Compare dictionaries and records

        private ComparisonResult CompareDictionaries(Dictionary<string, CsvRecord> expectedDict, Dictionary<string, CsvRecord> actualDict, List<string> expectedHeaders, List<string> keyColumns)
        {
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
                var rm = CompareRecordFields(expectedRec, actualRec, expectedHeaders, keyColumns);
                if (rm.FieldMismatches.Count > 0)
                    result.FieldLevelMismatches.Add(rm);
            }

            return result;
        }

        /// <summary>
        /// Compare fields of two records and return RecordMismatch (with key string).
        /// </summary>
        private RecordMismatch CompareRecordFields(CsvRecord expectedRec, CsvRecord actualRec, List<string> headers, List<string> keyColumns)
        {
            var mismatch = new RecordMismatch { Key = BuildKey(expectedRec, keyColumns) };

            foreach (var header in headers)
            {
                if (_options.IgnoreFields.Contains(header))
                    continue;

                // Skip key columns from field-level comparison
                if (keyColumns.Contains(header))
                    continue;

                string expectedVal = expectedRec.GetValue(header) ?? string.Empty;
                string actualVal = actualRec.GetValue(header) ?? string.Empty;

                bool equal = CompareValues(header, expectedVal, actualVal);

                if (!equal)
                {
                    mismatch.FieldMismatches.Add(new FieldMismatch
                    {
                        FieldName = header,
                        ExpectedValue = expectedVal,
                        ActualValue = actualVal,
                        Scope = "FIELD",
                        KeyColumns = keyColumns.ToList(),
                        KeyValues = keyColumns.Select(k => expectedRec.GetValue(k) ?? string.Empty).ToList()
                    });
                }
            }

            return mismatch;
        }

        #endregion

        #region Duplicate handling and insertion helper

        private void HandleDuplicateInsert(Dictionary<string, CsvRecord> dict, string key, CsvRecord rec, string whichFile)
        {
            if (!dict.ContainsKey(key))
            {
                dict[key] = rec;
                return;
            }

            switch (_options.DuplicateKeyPolicy)
            {
                case DuplicateKeyPolicy.Throw:
                    throw new InvalidDataException($"Duplicate key '{key}' found in {whichFile} CSV.");
                case DuplicateKeyPolicy.UseFirst:
                    // ignore new
                    return;
                case DuplicateKeyPolicy.UseLast:
                    dict[key] = rec;
                    return;
                case DuplicateKeyPolicy.Aggregate:
                    // Aggregate by appending suffixes to field names (rare). We'll keep last for simplicity but could be extended.
                    dict[key] = rec;
                    return;
                default:
                    throw new InvalidOperationException("Unknown DuplicateKeyPolicy.");
            }
        }

        #endregion

        #region Key building and value comparison

        /// <summary>
        /// Build composite key string from record using keyColumns (expected header names).
        /// Normalizes case if configured.
        /// </summary>
        private string BuildKey(CsvRecord rec, List<string> keyColumns)
        {
            var parts = new List<string>();
            foreach (var k in keyColumns)
            {
                var v = rec.GetValue(k) ?? string.Empty;
                v = v.Trim();
                if (_options.NormalizeKeyCase || _options.CaseInsensitive)
                    v = v.ToLowerInvariant();
                parts.Add(v);
            }
            return string.Join("||", parts);
        }

        /// <summary>
        /// Compare two values for a given field using custom comparator, per-field tolerance, global tolerance, and case options.
        /// </summary>
        private bool CompareValues(string fieldName, string expectedVal, string actualVal)
        {
            // Custom comparator
            if (_options.CustomComparators.TryGetValue(fieldName, out var comparator))
            {
                return comparator(expectedVal, actualVal);
            }

            // If both are empty or whitespace, consider equal
            if (string.IsNullOrWhiteSpace(expectedVal) && string.IsNullOrWhiteSpace(actualVal))
                return true;

            // Try numeric comparison if tolerance is set (per-field or global)
            double? tol = null;
            if (_options.PerFieldNumericTolerance.TryGetValue(fieldName, out var perTol))
                tol = perTol;
            else
                tol = _options.NumericTolerance;

            if (tol.HasValue)
            {
                if (double.TryParse(expectedVal, NumberStyles.Any, CultureInfo.InvariantCulture, out var d1) &&
                    double.TryParse(actualVal, NumberStyles.Any, CultureInfo.InvariantCulture, out var d2))
                {
                    return Math.Abs(d1 - d2) <= tol.Value;
                }
            }

            // Default string comparison with case option
            var comparison = _options.CaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return string.Equals(expectedVal?.Trim(), actualVal?.Trim(), comparison);
        }

        #endregion
    }
}