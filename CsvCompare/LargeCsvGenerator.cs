using System.Text;

namespace CsvCompare.Tools
{
    /// <summary>
    /// Generates synthetic CSV files for testing and stress‑testing the comparer.
    /// No external libraries are used. Produces RFC4180-like quoted fields when needed.
    /// </summary>
    public static class LargeCsvGenerator
    {
        /// <summary>
        /// Generate a CSV file with given rows and columns.
        /// Example: LargeCsvGenerator.Generate("big_expected.csv", 100000, 10, seed: 42, delimiter: ',');
        /// </summary>
        /// <param name="path">Output CSV file path.</param>
        /// <param name="rows">Number of data rows to generate (excluding header).</param>
        /// <param name="columns">Number of columns (must be >= 1).</param>
        /// <param name="seed">Random seed for reproducible output.</param>
        /// <param name="pctQuoted">Fraction of fields that will contain special characters and be quoted.</param>
        /// <param name="delimiter">Delimiter character to use (default comma).</param>
        public static void Generate(string path, int rows, int columns, int seed = 1, double pctQuoted = 0.05, char delimiter = ',')
        {
            if (rows < 0) throw new ArgumentOutOfRangeException(nameof(rows));
            if (columns < 1) throw new ArgumentOutOfRangeException(nameof(columns));
            if (pctQuoted < 0 || pctQuoted > 1) throw new ArgumentOutOfRangeException(nameof(pctQuoted));

            var rnd = new Random(seed);

            // Ensure directory exists
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            // Use a large buffer for performance
            using var sw = new StreamWriter(path, false, Encoding.UTF8, 65536);

            // Write header
            for (int c = 0; c < columns; c++)
            {
                if (c > 0) sw.Write(delimiter);
                sw.Write($"Col{c + 1}");
            }
            sw.Write("\r\n");

            // Generate rows
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    if (c > 0) sw.Write(delimiter);
                    string value = GenerateFieldValue(r, c, rnd, pctQuoted);
                    // Quote if contains delimiter, newline, or quote
                    if (value.IndexOfAny(new char[] { delimiter, '\r', '\n', '"' }) >= 0)
                    {
                        // escape quotes by doubling
                        sw.Write('"');
                        sw.Write(value.Replace("\"", "\"\""));
                        sw.Write('"');
                    }
                    else
                    {
                        sw.Write(value);
                    }
                }
                sw.Write("\r\n");
            }
        }

        /// <summary>
        /// Produces a synthetic field value based on row/column indices and randomness.
        /// Column 1 (index 0) is treated as a key-like value (zero-padded numeric).
        /// Some columns produce numeric values; others produce text with occasional special chars.
        /// </summary>
        private static string GenerateFieldValue(int row, int col, Random rnd, double pctQuoted)
        {
            // First column: key-like numeric id (zero-padded)
            if (col == 0)
            {
                return (row + 1).ToString("D9"); // e.g., 000000001
            }

            // Numeric-like columns every 5th column
            if (col % 5 == 0)
            {
                double baseVal = row * 0.12345 + col;
                double noise = (rnd.NextDouble() - 0.5) * 0.001;
                return (baseVal + noise).ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
            }

            // Occasionally produce quoted/multiline/quoted-quote text
            if (rnd.NextDouble() < pctQuoted)
            {
                string[] samples = new[]
                {
                    "This is a sample, with comma",
                    "Multi\nLine\nText",
                    "He said \"Hello\"",
                    "Normal text with unicode ✓",
                    "Text with trailing spaces "
                };
                return samples[rnd.Next(samples.Length)];
            }

            // Default short text
            string[] words = new[] { "Alpha", "Beta", "Gamma", "Delta", "Epsilon" };
            return words[rnd.Next(words.Length)] + "_" + (row % 1000);
        }
    }
}