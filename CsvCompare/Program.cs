namespace CsvCompare
{
    class Program
    {
        static void Main(string[] args)
        {
            // Example usage:
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: CsvCompare <expected.csv> <actual.csv> <key1[,key2,...]>");
                return;
            }

            string expectedPath = args[0];
            string actualPath = args[1];
            var keys = new List<string>(args[2].Split(',', StringSplitOptions.RemoveEmptyEntries));

            var options = new CsvComparerOptions
            {
                CaseInsensitive = false,
                NumericTolerance = 0.0005
            };

            var comparer = new CsvComparer(options);

            using var expectedReader = new StreamReader(expectedPath);
            using var actualReader = new StreamReader(actualPath);

            try
            {
                var result = comparer.Compare(expectedReader, actualReader, keys);

                Console.WriteLine("Comparison Summary:");
                Console.WriteLine($"Missing in Actual: {result.MissingInActual.Count}");
                foreach (var k in result.MissingInActual) Console.WriteLine($"  MISSING: {k}");

                Console.WriteLine($"Extra in Actual: {result.ExtraInActual.Count}");
                foreach (var k in result.ExtraInActual) Console.WriteLine($"  EXTRA: {k}");

                Console.WriteLine($"Field-level mismatches: {result.FieldLevelMismatches.Count}");
                foreach (var rm in result.FieldLevelMismatches)
                {
                    Console.WriteLine($"Record Key: {rm.Key}");
                    foreach (var fm in rm.FieldMismatches)
                    {
                        Console.WriteLine($" Failed Fieldname: {fm.FieldName} | Expected Input Value: \"{fm.ExpectedValue}\" | Actual Value: \"{fm.ActualValue}\" | for record having unique field Key: {rm.Key}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during comparison: {ex.Message}");
            }
        }
    }
}