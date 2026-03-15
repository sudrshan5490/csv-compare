namespace CsvCompare
{
    /// <summary>
    /// Policy for handling duplicate keys found in a CSV file.
    /// </summary>
    public enum DuplicateKeyPolicy
    {
        Throw,
        UseFirst,
        UseLast,
        Aggregate
    }

    /// <summary>
    /// Mode of comparison to control memory/disk usage.
    /// </summary>
    public enum ComparisonMode
    {
        InMemory,       // load both files into memory (fast, memory heavy)
        StreamExpected, // load expected into memory, stream actual (memory-lean if actual is large)
        Bucketed        // hash keys into buckets on disk and compare bucket-by-bucket (scalable)
    }

    /// <summary>
    /// Options to control comparison behavior and future customizations.
    /// </summary>
    public class CsvComparerOptions
    {
        /// <summary>
        /// If true, string comparisons are case-insensitive.
        /// </summary>
        public bool CaseInsensitive { get; set; } = false;

        /// <summary>
        /// If true, key parts are normalized to lower-case when building composite keys.
        /// </summary>
        public bool NormalizeKeyCase { get; set; } = false;

        /// <summary>
        /// Numeric tolerance for comparing floating point numbers (global).
        /// If null, numeric comparison is exact string equality unless per-field tolerance exists.
        /// </summary>
        public double? NumericTolerance { get; set; } = 0.0001;

        /// <summary>
        /// Per-field numeric tolerance overrides global tolerance for specific fields.
        /// </summary>
        public Dictionary<string, double> PerFieldNumericTolerance { get; } = new Dictionary<string, double>(StringComparer.Ordinal);

        /// <summary>
        /// Fields to ignore during comparison.
        /// </summary>
        public HashSet<string> IgnoreFields { get; } = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Custom comparators per field name. If present, used instead of default comparison.
        /// </summary>
        public Dictionary<string, Func<string, string, bool>> CustomComparators { get; } = new Dictionary<string, Func<string, string, bool>>(StringComparer.Ordinal);

        /// <summary>
        /// Map expected header name -> actual header name (useful when headers differ).
        /// </summary>
        public Dictionary<string, string> FieldNameMapping { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Whether header order must match exactly. If false, headers are matched by name (or mapping).
        /// </summary>
        public bool RequireHeaderOrder { get; set; } = true;

        /// <summary>
        /// Duplicate key handling policy.
        /// </summary>
        public DuplicateKeyPolicy DuplicateKeyPolicy { get; set; } = DuplicateKeyPolicy.Throw;

        /// <summary>
        /// Comparison mode (InMemory, StreamExpected, Bucketed).
        /// </summary>
        public ComparisonMode Mode { get; set; } = ComparisonMode.InMemory;

        /// <summary>
        /// Number of buckets to use in Bucketed mode. Reasonable default is 64.
        /// </summary>
        public int BucketCount { get; set; } = 64;

        /// <summary>
        /// Directory for temporary bucket files. If null, system temp is used.
        /// </summary>
        public string TempDirectory { get; set; } = null;

        /// <summary>
        /// Delimiter used by CSV parser (default comma).
        /// </summary>
        public char Delimiter { get; set; } = ',';

        /// <summary>
        /// Logger callback for progress messages. Optional.
        /// </summary>
        public Action<string> Logger { get; set; } = null;
    }
}