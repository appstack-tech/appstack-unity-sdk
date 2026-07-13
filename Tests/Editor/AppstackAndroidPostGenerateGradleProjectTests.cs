using Appstack.Editor;
using NUnit.Framework;
using UnityEditor.Build;

namespace Appstack.Tests
{
    public sealed class AppstackAndroidPostGenerateGradleProjectTests
    {
        private const string BeginMarker = "# BEGIN Appstack Unity SDK";
        private const string EndMarker = "# END Appstack Unity SDK";

        [Test]
        public void MergeRulesInsertsBlockWithoutJoiningExistingFinalLine()
        {
            var result = AppstackAndroidPostGenerateGradleProject.MergeRules(
                "-keep class existing.Rule",
                "-keep class appstack.Rule");

            Assert.That(
                result,
                Is.EqualTo(
                    "-keep class existing.Rule\n\n" +
                    BeginMarker + "\n" +
                    "-keep class appstack.Rule\n" +
                    EndMarker + "\n"));
        }

        [Test]
        public void MergeRulesNormalizesSourceToGeneratedCrLfLineEndings()
        {
            var result = AppstackAndroidPostGenerateGradleProject.MergeRules(
                "-keep class existing.Rule\r\n",
                "-keep class appstack.One\n-keep class appstack.Two");

            Assert.That(
                result,
                Is.EqualTo(
                    "-keep class existing.Rule\r\n\r\n" +
                    BeginMarker + "\r\n" +
                    "-keep class appstack.One\r\n" +
                    "-keep class appstack.Two\r\n" +
                    EndMarker + "\r\n"));
        }

        [Test]
        public void MergeRulesReplacesExistingBlockIdempotently()
        {
            var existing =
                "before\n" +
                BeginMarker + "\n" +
                "old rule\n" +
                EndMarker + "\n" +
                "after\n";

            var replaced = AppstackAndroidPostGenerateGradleProject.MergeRules(
                existing,
                "new rule");
            var repeated = AppstackAndroidPostGenerateGradleProject.MergeRules(
                replaced,
                "new rule");

            Assert.That(replaced, Does.Contain("new rule"));
            Assert.That(replaced, Does.Not.Contain("old rule"));
            Assert.That(repeated, Is.EqualTo(replaced));
        }

        [TestCase(BeginMarker + "\nrule\n")]
        [TestCase("rule\n" + EndMarker)]
        [TestCase(BeginMarker + "\n" + BeginMarker + "\n" + EndMarker)]
        [TestCase(EndMarker + "\nrule\n" + BeginMarker)]
        public void MergeRulesRejectsMalformedMarkers(string generatedRules)
        {
            Assert.Throws<BuildFailedException>(
                () => AppstackAndroidPostGenerateGradleProject.MergeRules(
                    generatedRules,
                    "new rule"));
        }
    }
}
