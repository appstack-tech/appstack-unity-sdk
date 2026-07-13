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
    }
}
