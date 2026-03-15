namespace CsvCompare.Tests
{
    [TestFixture]
    public class CsvComparerTests
    {
        // Helper to create a TextReader from string
        private TextReader Reader(string s) => new StringReader(s);

        [Test]
        public void Test_SimpleMatch_NoDifferences()
        {
            string csv = "Id,Name,Value\r\n1,Alice,10\r\n2,Bob,20\r\n";
            var options = new CsvComparerOptions { NumericTolerance = 0.0001 };
            var comparer = new CsvComparer(options);
            var result = comparer.Compare(Reader(csv), Reader(csv), new List<string> { "Id" });

            Assert.AreEqual(0, result.MissingInActual.Count);
            Assert.AreEqual(0, result.ExtraInActual.Count);
            Assert.AreEqual(0, result.FieldLevelMismatches.Count);
        }

        [Test]
        public void Test_MissingAndExtraKeys()
        {
            string expected = "Id,Name\r\n1,Alice\r\n2,Bob\r\n3,Charlie\r\n";
            string actual = "Id,Name\r\n2,Bob\r\n4,David\r\n";
            var comparer = new CsvComparer();
            var result = comparer.Compare(Reader(expected), Reader(actual), new List<string> { "Id" });

            Assert.AreEqual(1, result.MissingInActual.Count);
            Assert.AreEqual("1", result.MissingInActual[0].Split("||")[0]);
            Assert.AreEqual(1, result.ExtraInActual.Count);
            Assert.AreEqual("4", result.ExtraInActual[0].Split("||")[0]);
        }

        [Test]
        public void Test_FieldLevelMismatch_StringCaseSensitive()
        {
            string expected = "Id,Name\r\n1,Alice\r\n";
            string actual = "Id,Name\r\n1,alice\r\n";
            var options = new CsvComparerOptions { CaseInsensitive = false, NumericTolerance = null };
            var comparer = new CsvComparer(options);
            var result = comparer.Compare(Reader(expected), Reader(actual), new List<string> { "Id" });

            Assert.AreEqual(0, result.MissingInActual.Count);
            Assert.AreEqual(0, result.ExtraInActual.Count);
            Assert.AreEqual(1, result.FieldLevelMismatches.Count);
            Assert.AreEqual("Name", result.FieldLevelMismatches[0].FieldMismatches[0].FieldName);
        }

        [Test]
        public void Test_FieldLevelMismatch_NumericTolerance()
        {
            string expected = "Id,Value\r\n1,533.5761748850723\r\n";
            string actual = "Id,Value\r\n1,533.5761748850728\r\n";
            var options = new CsvComparerOptions { NumericTolerance = 0.000001 };
            var comparer = new CsvComparer(options);
            var result = comparer.Compare(Reader(expected), Reader(actual), new List<string> { "Id" });

            // difference is 5e-13, within tolerance
            Assert.AreEqual(0, result.FieldLevelMismatches.Count);
        }

        [Test]
        public void Test_QuotedFields_CommasAndNewlines()
        {
            string expected = "Id,Notes\r\n1,\"Line1, still same\"\r\n2,\"Multi\nLine\nText\"\r\n";
            string actual = "Id,Notes\r\n1,\"Line1, still same\"\r\n2,\"Multi\nLine\nText\"\r\n";
            var comparer = new CsvComparer();
            var result = comparer.Compare(Reader(expected), Reader(actual), new List<string> { "Id" });
            Assert.AreEqual(0, result.FieldLevelMismatches.Count);
        }

        [Test]
        public void Test_CompositeKey()
        {
            string expected = "Country,Code,Value\r\nUS,001,10\r\nIN,091,20\r\n";
            string actual = "Country,Code,Value\r\nIN,091,20\r\nUS,001,11\r\n";
            var options = new CsvComparerOptions { NumericTolerance = 0.0001 };
            var comparer = new CsvComparer(options);
            var result = comparer.Compare(Reader(expected), Reader(actual), new List<string> { "Country", "Code" });

            Assert.AreEqual(0, result.MissingInActual.Count);
            Assert.AreEqual(0, result.ExtraInActual.Count);
            Assert.AreEqual(1, result.FieldLevelMismatches.Count);
            Assert.AreEqual("Value", result.FieldLevelMismatches[0].FieldMismatches[0].FieldName);
        }

        [Test]
        public void Test_EmptyAndWhitespaceFields()
        {
            string expected = "Id,Note\r\n1,\r\n2,   \r\n";
            string actual = "Id,Note\r\n1,\r\n2,\r\n";
            var comparer = new CsvComparer(new CsvComparerOptions { NumericTolerance = null });
            var result = comparer.Compare(Reader(expected), Reader(actual), new List<string> { "Id" });
            // whitespace vs empty considered equal
            Assert.AreEqual(0, result.FieldLevelMismatches.Count);
        }

        [Test]
        public void Test_HeaderMismatch_Throws()
        {
            string expected = "A,B,C\r\n1,2,3\r\n";
            string actual = "A,B,D\r\n1,2,3\r\n";
            var comparer = new CsvComparer();
            Assert.Throws<InvalidDataException>(() => comparer.Compare(Reader(expected), Reader(actual), new List<string> { "A" }));
        }

        [Test]
        public void Test_DuplicateKey_Throws()
        {
            string expected = "Id,Name\r\n1,Alice\r\n1,Bob\r\n";
            string actual = "Id,Name\r\n1,Alice\r\n";
            var comparer = new CsvComparer();
            Assert.Throws<InvalidDataException>(() => comparer.Compare(Reader(expected), Reader(actual), new List<string> { "Id" }));
        }

        [Test]
        public void Test_CustomComparator_PerField()
        {
            string expected = "Id,Date\r\n1,2023-03-01\r\n";
            string actual = "Id,Date\r\n1,01-Mar-2023\r\n";
            var options = new CsvComparerOptions();
            options.CustomComparators["Date"] = (e, a) =>
            {
                if (DateTime.TryParse(e, out var de) && DateTime.TryParse(a, out var da))
                    return de.Date == da.Date;
                return string.Equals(e, a, StringComparison.Ordinal);
            };
            var comparer = new CsvComparer(options);
            var result = comparer.Compare(Reader(expected), Reader(actual), new List<string> { "Id" });
            Assert.AreEqual(0, result.FieldLevelMismatches.Count);
        }
    }
}