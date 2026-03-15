using System.Text;

namespace CsvCompare.Tests
{
    [TestFixture]
    public class CsvComparerTests
    {
        // Helper to create a TextReader from string
        private TextReader Reader(string s) => new StringReader(s);

        [Test]
        public void Test_HeaderOrderFlexibility_MappingAndOrderIgnored()
        {
            string expected = "A,B,C\r\n1,2,3\r\n";
            string actual = "C,B,A\r\n3,2,1\r\n";

            var options = new CsvComparerOptions
            {
                RequireHeaderOrder = false
            };

            var comparer = new CsvComparer(options);
            var result = comparer.Compare(Reader(expected), Reader(actual), ["A"]);

            Assert.AreEqual(0, result.MissingInActual.Count);
            Assert.AreEqual(0, result.ExtraInActual.Count);
        }

        [Test]
        public void Test_DuplicateKeyPolicies()
        {
            string expected = "Id,Name\r\n1,Alice\r\n1,Bob\r\n";
            string actual = "Id,Name\r\n1,Alice\r\n";

            // Throw policy
            var optionsThrow = new CsvComparerOptions { DuplicateKeyPolicy = DuplicateKeyPolicy.Throw };
            var comparerThrow = new CsvComparer(optionsThrow);
            Assert.Throws<InvalidDataException>(() => comparerThrow.Compare(Reader(expected), Reader(actual), ["Id"]));

            // UseFirst policy: first occurrence should be kept
            var optionsUseFirst = new CsvComparerOptions { DuplicateKeyPolicy = DuplicateKeyPolicy.UseFirst };
            var comparerUseFirst = new CsvComparer(optionsUseFirst);
            var resUseFirst = comparerUseFirst.Compare(Reader(expected), Reader(actual), ["Id"]);
            // actual contains Id=1 so no missing keys
            Assert.AreEqual(0, resUseFirst.MissingInActual.Count);

            // UseLast policy: last occurrence should be kept (no exception)
            var optionsUseLast = new CsvComparerOptions { DuplicateKeyPolicy = DuplicateKeyPolicy.UseLast };
            var comparerUseLast = new CsvComparer(optionsUseLast);
            var resUseLast = comparerUseLast.Compare(Reader(expected), Reader(actual), ["Id"]);
            Assert.AreEqual(0, resUseLast.MissingInActual.Count);

            // Aggregate policy currently behaves like UseLast in this implementation (no exception)
            var optionsAgg = new CsvComparerOptions { DuplicateKeyPolicy = DuplicateKeyPolicy.Aggregate };
            var comparerAgg = new CsvComparer(optionsAgg);
            var resAgg = comparerAgg.Compare(Reader(expected), Reader(actual), ["Id"]);
            Assert.AreEqual(0, resAgg.MissingInActual.Count);
        }

        [Test]
        public void Test_StreamExpected_Mode_ReducesMemory()
        {
            string expected = "Id,Value\r\n1,100\r\n2,200\r\n3,300\r\n";
            string actual = "Id,Value\r\n3,300\r\n2,200\r\n4,400\r\n";

            var options = new CsvComparerOptions { Mode = ComparisonMode.StreamExpected };
            var comparer = new CsvComparer(options);
            var result = comparer.Compare(Reader(expected), Reader(actual), ["Id"]);

            Assert.AreEqual(1, result.MissingInActual.Count); // Id=1 missing
            Assert.AreEqual(1, result.ExtraInActual.Count);   // Id=4 extra
        }

        [Test]
        public void Test_Bucketed_Mode_SmallBuckets()
        {
            // Create many small rows to exercise bucket splitting
            var sbExp = new StringBuilder();
            sbExp.AppendLine("Id,Val");
            var sbAct = new StringBuilder();
            sbAct.AppendLine("Id,Val");
            for (int i = 1; i <= 200; i++)
            {
                sbExp.AppendLine($"{i},E{i}");
                if (i % 10 != 0) // leave some missing in actual
                    sbAct.AppendLine($"{i},E{i}");
            }

            var options = new CsvComparerOptions { Mode = ComparisonMode.Bucketed, BucketCount = 8 };
            var comparer = new CsvComparer(options);
            var result = comparer.Compare(Reader(sbExp.ToString()), Reader(sbAct.ToString()), ["Id"]);

            Assert.Greater(result.MissingInActual.Count, 0);
        }

        [Test]
        public void Test_PerFieldTolerance_OverridesGlobal()
        {
            string expected = "Id,ValA,ValB\r\n1,100.0000,200.0000\r\n";
            string actual = "Id,ValA,ValB\r\n1,100.0005,200.1\r\n";

            var options = new CsvComparerOptions { NumericTolerance = 0.0001 };
            options.PerFieldNumericTolerance["ValB"] = 0.2; // ValB tolerant
            var comparer = new CsvComparer(options);
            var result = comparer.Compare(Reader(expected), Reader(actual), ["Id"]);

            // ValA difference 0.0005 > global tol -> mismatch; ValB within per-field tol -> ok
            Assert.AreEqual(1, result.FieldLevelMismatches.Count);
            Assert.AreEqual("ValA", result.FieldLevelMismatches[0].FieldMismatches[0].FieldName);
        }

        [Test]
        public void Test_KeyNormalization_CaseInsensitiveKeys()
        {
            string expected = "Id,Name\r\nabc,John\r\n";
            string actual = "Id,Name\r\nABC,John\r\n";

            var options = new CsvComparerOptions { NormalizeKeyCase = true };
            var comparer = new CsvComparer(options);
            var result = comparer.Compare(Reader(expected), Reader(actual), ["Id"]);

            Assert.AreEqual(0, result.MissingInActual.Count);
            Assert.AreEqual(0, result.ExtraInActual.Count);
        }

        [Test]
        public void Test_EnhancedReporting_Format()
        {
            string expected = "Id,Value\r\n1,10\r\n";
            string actual = "Id,Value\r\n1,11\r\n";

            var options = new CsvComparerOptions();
            var comparer = new CsvComparer(options);
            var result = comparer.Compare(Reader(expected), Reader(actual), ["Id"]);

            Assert.AreEqual(1, result.FieldLevelMismatches.Count);
            var fm = result.FieldLevelMismatches[0].FieldMismatches[0];
            Assert.AreEqual("FIELD", fm.Scope);
            Assert.AreEqual(1, fm.KeyColumns.Count);
            Assert.AreEqual("Id", fm.KeyColumns[0]);
            Assert.AreEqual("1", fm.KeyValues[0]);
        }
    }
}