namespace CsvCompare
{
    /// <summary>
    /// Options to control comparison behavior.
    /// </summary>
    public class CsvComparerOptions
    {
        /// <summary>
        /// If true, string comparisons are case-insensitive.
        /// </summary>
        public bool CaseInsensitive { get; set; } = false;

        /// <summary>
        /// Numeric tolerance for comparing floating point numbers.
        /// If null, numeric comparison is exact string equality.
        /// </summary>
        public double? NumericTolerance { get; set; } = 0.0001;

        /// <summary>
        /// Fields to ignore during comparison.
        /// </summary>
        public HashSet<string> IgnoreFields { get; } = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Custom comparators per field name. If present, used instead of default comparison.
        /// </summary>
        public Dictionary<string, Func<string, string, bool>> CustomComparators { get; } = new Dictionary<string, Func<string, string, bool>>(StringComparer.Ordinal);
    }
}