using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;

namespace Appstack.Tests
{
    public sealed class AppstackJsonTests
    {
        [Test]
        public void SerializeObjectEscapesStringsAndControlCharacters()
        {
            var json = AppstackJson.SerializeObject(
                new Dictionary<string, object>
                {
                    { "message", "quote=\" slash=/ backslash=\\\n\r\t\b\f\u0001" },
                });

            Assert.That(
                json,
                Is.EqualTo(
                    "{\"message\":\"quote=\\\" slash=/ backslash=\\\\" +
                    "\\n\\r\\t\\b\\f\\u0001\"}"));
        }

        [Test]
        public void SerializeObjectPreservesUtf8EmojiAndNonLatinText()
        {
            var json = AppstackJson.SerializeObject(
                new Dictionary<string, object>
                {
                    { "campaign", "Café 夏 🚀" },
                });

            Assert.That(json, Is.EqualTo("{\"campaign\":\"Café 夏 🚀\"}"));
        }

        [Test]
        public void SerializeObjectSupportsPrimitiveNumericTypes()
        {
            var json = AppstackJson.SerializeObject(
                new Dictionary<string, object>
                {
                    { "null", null },
                    { "boolean", true },
                    { "sbyte", (sbyte)-1 },
                    { "byte", (byte)2 },
                    { "short", (short)-3 },
                    { "ushort", (ushort)4 },
                    { "int", -5 },
                    { "uint", (uint)6 },
                    { "long", -7L },
                    { "ulong", 8UL },
                    { "float", 1.25f },
                    { "double", -2.5d },
                    { "decimal", 3.75m },
                });

            Assert.That(
                json,
                Is.EqualTo(
                    "{\"null\":null,\"boolean\":true,\"sbyte\":-1,\"byte\":2," +
                    "\"short\":-3,\"ushort\":4,\"int\":-5,\"uint\":6," +
                    "\"long\":-7,\"ulong\":8,\"float\":1.25," +
                    "\"double\":-2.5,\"decimal\":3.75}"));
        }

        [Test]
        public void SerializeObjectSupportsNestedObjectsAndArrays()
        {
            var json = AppstackJson.SerializeObject(
                new Dictionary<string, object>
                {
                    {
                        "items",
                        new object[]
                        {
                            1,
                            "two",
                            new Dictionary<string, object> { { "active", true } },
                        }
                    },
                });

            Assert.That(
                json,
                Is.EqualTo("{\"items\":[1,\"two\",{\"active\":true}]}"));
        }

        [Test]
        public void SerializeObjectUsesInvariantCultureForNumbers()
        {
            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-AR");

                var json = AppstackJson.SerializeObject(
                    new Dictionary<string, object>
                    {
                        { "double", 12.5 },
                        { "decimal", 8.25m },
                    });

                Assert.That(json, Is.EqualTo("{\"double\":12.5,\"decimal\":8.25}"));
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }

        [Test]
        public void SerializeObjectReturnsEmptyObjectForNullOrEmptyInput()
        {
            Assert.That(AppstackJson.SerializeObject(null), Is.EqualTo("{}"));
            Assert.That(
                AppstackJson.SerializeObject(new Dictionary<string, object>()),
                Is.EqualTo("{}"));
        }

        [TestCaseSource(nameof(NonFiniteNumbers))]
        public void SerializeObjectRejectsNonFiniteNumbers(object value)
        {
            Assert.Throws<ArgumentException>(
                () => AppstackJson.SerializeObject(
                    new Dictionary<string, object> { { "value", value } }));
        }

        [Test]
        public void SerializeObjectRejectsUnsupportedObjectsAndNonStringKeys()
        {
            Assert.Throws<ArgumentException>(
                () => AppstackJson.SerializeObject(
                    new Dictionary<string, object> { { "value", DateTime.UtcNow } }));

            Assert.Throws<ArgumentException>(
                () => AppstackJson.SerializeObject(
                    new Dictionary<string, object>
                    {
                        { "nested", new Hashtable { { 1, "not a string key" } } },
                    }));
        }

        [Test]
        public void SerializeObjectRejectsReferenceCycles()
        {
            var values = new Dictionary<string, object>();
            values["self"] = values;

            Assert.Throws<ArgumentException>(() => AppstackJson.SerializeObject(values));
        }

        [Test]
        public void ParseObjectPreservesUtf8EscapesAndStringSeparators()
        {
            var result = AppstackJson.ParseObject(
                "{\"campaign\":\"Café 夏 🚀,source:organic\"," +
                "\"escaped\":\"line\\n\\t\\b\\f\\/\\u0021\"}");

            Assert.That(result["campaign"], Is.EqualTo("Café 夏 🚀,source:organic"));
            Assert.That(result["escaped"], Is.EqualTo("line\n\t\b\f/!"));
        }

        [Test]
        public void ParseObjectReadsNumbersBooleansAndNull()
        {
            var result = AppstackJson.ParseObject(
                "{\"integer\":-42,\"decimal\":12.5,\"exponent\":-1.25e3," +
                "\"yes\":true,\"no\":false,\"empty\":null}");

            Assert.That(result["integer"], Is.EqualTo(-42L));
            Assert.That(result["decimal"], Is.EqualTo(12.5d));
            Assert.That(result["exponent"], Is.EqualTo(-1250d));
            Assert.That(result["yes"], Is.True);
            Assert.That(result["no"], Is.False);
            Assert.That(result["empty"], Is.Null);
        }

        [Test]
        public void ParseObjectReadsNestedObjectsAndArrays()
        {
            var result = AppstackJson.ParseObject(
                "{\"campaign\":{\"name\":\"summer\"}," +
                "\"touchpoints\":[1,\"organic\",false,null]}");

            var campaign = result["campaign"] as Dictionary<string, object>;
            var touchpoints = result["touchpoints"] as List<object>;

            Assert.That(campaign, Is.Not.Null);
            Assert.That(campaign["name"], Is.EqualTo("summer"));
            Assert.That(touchpoints, Is.EqualTo(new object[] { 1L, "organic", false, null }));
        }

        [Test]
        public void ParseObjectUsesLastValueForDuplicateKeys()
        {
            var result = AppstackJson.ParseObject("{\"campaign\":\"first\",\"campaign\":\"last\"}");

            Assert.That(result["campaign"], Is.EqualTo("last"));
            Assert.That(result.Count, Is.EqualTo(1));
        }

        [Test]
        public void ParseObjectHandlesLargeAttributionPayload()
        {
            var expected = new string('é', 20000);

            var result = AppstackJson.ParseObject("{\"payload\":\"" + expected + "\"}");

            Assert.That(result["payload"], Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not-json")]
        [TestCase("[]")]
        [TestCase("{\"missing\":}")]
        [TestCase("{\"leadingZero\":01}")]
        [TestCase("{\"trailing\":1,}")]
        [TestCase("{\"escape\":\"\\x\"}")]
        [TestCase("{\"valid\":1} trailing")]
        public void ParseObjectReturnsEmptyDictionaryForMalformedJson(string json)
        {
            var result = AppstackJson.ParseObject(json);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void SerializeAndParseRoundTripNestedJsonData()
        {
            var original = new Dictionary<string, object>
            {
                { "name", "launch" },
                { "values", new object[] { 1, 2.5, true, null } },
                { "metadata", new Dictionary<string, object> { { "source", "test" } } },
            };

            var parsed = AppstackJson.ParseObject(AppstackJson.SerializeObject(original));

            Assert.That(parsed["name"], Is.EqualTo("launch"));
            Assert.That(
                parsed["values"],
                Is.EqualTo(new object[] { 1L, 2.5d, true, null }));
            Assert.That(
                ((Dictionary<string, object>)parsed["metadata"])["source"],
                Is.EqualTo("test"));
        }

        private static IEnumerable NonFiniteNumbers
        {
            get
            {
                yield return float.NaN;
                yield return float.PositiveInfinity;
                yield return float.NegativeInfinity;
                yield return double.NaN;
                yield return double.PositiveInfinity;
                yield return double.NegativeInfinity;
            }
        }
    }
}
