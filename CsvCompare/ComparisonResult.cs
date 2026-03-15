namespace CsvCompare
{
    public class FieldMismatch
    {
        public string FieldName { get; set; }
        public string ExpectedValue { get; set; }
        public string ActualValue { get; set; }
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