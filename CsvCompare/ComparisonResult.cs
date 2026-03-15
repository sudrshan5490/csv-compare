namespace CsvCompare
{
    public class FieldMismatch
    {
        public string FieldName { get; set; }
        public string ExpectedValue { get; set; }
        public string ActualValue { get; set; }

        /// <summary>
        /// Scope of mismatch (e.g., FIELD, RECORD). Default FIELD.
        /// </summary>
        public string Scope { get; set; } = "FIELD";

        /// <summary>
        /// Key column names for the record where mismatch occurred.
        /// </summary>
        public List<string> KeyColumns { get; set; } = new List<string>();

        /// <summary>
        /// Corresponding key values (same order as KeyColumns).
        /// </summary>
        public List<string> KeyValues { get; set; } = new List<string>();
    }

    public class RecordMismatch
    {
        public string Key { get; set; }
        public List<FieldMismatch> FieldMismatches { get; } = new List<FieldMismatch>();
    }

    public class ComparisonResult
    {
        public List<string> MissingInActual { get; } = new List<string>();
        public List<string> ExtraInActual { get; } = new List<string>();
        public List<RecordMismatch> FieldLevelMismatches { get; } = new List<RecordMismatch>();
    }
}