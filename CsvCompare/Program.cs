namespace CsvCompare
{
    class Program
    {
        static void Main(string[] args)
        {
            // Simple CLI dispatcher:
            // generate <path> <rows> <cols> [seed]
            // compare <expected> <actual> <key1[,key2]> <outCsv> <outJson> [mode] [bucketCount] [caseInsensitive]
            if (args.Length == 0)
            {
                Console.WriteLine("Commands:");
                Console.WriteLine("  generate <path> <rows> <cols> [seed]");
                Console.WriteLine("  compare <expected> <actual> <key1[,key2]> <outCsv> <outJson> [mode] [bucketCount] [caseInsensitive]");
                return;
            }

            var cmd = args[0].ToLowerInvariant();
            try
            {
                if (cmd == "generate" && args.Length >= 4)
                {
                    string path = args[1];
                    int rows = int.Parse(args[2]);
                    int cols = int.Parse(args[3]);
                    int seed = args.Length >= 5 ? int.Parse(args[4]) : 1;
                    Tools.LargeCsvGenerator.Generate(path, rows, cols, seed);
                    Console.WriteLine($"Generated {rows} rows x {cols} cols to {path}");
                    return;
                }

                if (cmd == "compare" && args.Length >= 6)
                {
                    string expected = args[1];
                    string actual = args[2];
                    var keys = new List<string>(args[3].Split(',', StringSplitOptions.RemoveEmptyEntries));
                    string outCsv = args[4];
                    string outJson = args[5];

                    var options = new CsvComparerOptions
                    {
                        CaseInsensitive = args.Length >= 9 ? bool.Parse(args[8]) : false,
                        NumericTolerance = 0.0001,
                        Mode = args.Length >= 7 ? Enum.Parse<ComparisonMode>(args[6], true) : ComparisonMode.InMemory,
                        BucketCount = args.Length >= 8 ? int.Parse(args[7]) : 64
                    };

                    options.Logger = s => Console.WriteLine("[LOG] " + s);

                    var comparer = new CsvComparer(options);
                    using var er = new StreamReader(expected);
                    using var ar = new StreamReader(actual);
                    var result = comparer.Compare(er, ar, keys);

                    // Write reports
                    Directory.CreateDirectory(Path.GetDirectoryName(outCsv) ?? ".");
                    Reporting.MismatchReportWriter.WriteCsvSummary(outCsv, result);
                    if (!string.IsNullOrEmpty(outJson))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(outJson) ?? ".");
                        Reporting.MismatchReportWriter.WriteJsonSummary(outJson, result);
                    }

                    Console.WriteLine("Comparison complete.");
                    Console.WriteLine($"Missing: {result.MissingInActual.Count}, Extra: {result.ExtraInActual.Count}, Field mismatches: {result.FieldLevelMismatches.Count}");
                    return;
                }

                Console.WriteLine("Invalid command or arguments.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }
}