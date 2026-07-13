using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;

namespace Appstack.Tests
{
    public sealed class AppstackJsonTests
    {
        [Test]
        public void ParseObjectPreservesUtf8AndEscapedSeparators()
        {
            var result = AppstackJson.ParseObject(
                "{\"campaign\":\"Café, verano\",\"matched\":true}");

            Assert.That(result["campaign"], Is.EqualTo("Café, verano"));
            Assert.That(result["matched"], Is.EqualTo(true));
        }

        [Test]
        public void ParseObjectReturnsEmptyDictionaryForMalformedJson()
        {
            var result = AppstackJson.ParseObject("{not-json}");

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void SerializeObjectEscapesStringsAndPreservesPrimitiveTypes()
        {
            var json = AppstackJson.SerializeObject(
                new Dictionary<string, object>
                {
                    { "message", "Café \"summer\"\n" },
                    { "matched", true },
                    { "count", 3 },
                    { "empty", null },
                });

            Assert.That(
                json,
                Is.EqualTo(
                    "{\"message\":\"Café \\\"summer\\\"\\n\"," +
                    "\"matched\":true,\"count\":3,\"empty\":null}"));
        }

        [Test]
        public void SerializeObjectUsesInvariantCultureForNumbers()
        {
            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-AR");

                var json = AppstackJson.SerializeObject(
                    new Dictionary<string, object> { { "revenue", 12.5 } });

                Assert.That(json, Is.EqualTo("{\"revenue\":12.5}"));
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }
    }
}
