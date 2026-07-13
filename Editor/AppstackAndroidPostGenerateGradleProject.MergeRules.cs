#if UNITY_EDITOR
using System;
using UnityEditor.Build;

namespace Appstack.Editor
{
    internal sealed partial class AppstackAndroidPostGenerateGradleProject
    {
        private const string BeginMarker = "# BEGIN Appstack Unity SDK";
        private const string EndMarker = "# END Appstack Unity SDK";

        internal static string MergeRules(string generatedRules, string sourceRules)
        {
            var newline = generatedRules.Contains("\r\n") ? "\r\n" : "\n";
            var normalizedSource = NormalizeLineEndings(sourceRules, newline).Trim();
            var block = BeginMarker + newline + normalizedSource + newline + EndMarker;

            var beginCount = CountOccurrences(generatedRules, BeginMarker);
            var endCount = CountOccurrences(generatedRules, EndMarker);
            if (beginCount == 0 && endCount == 0)
            {
                var separator = string.IsNullOrEmpty(generatedRules) ||
                                generatedRules.EndsWith(
                                    newline + newline,
                                    StringComparison.Ordinal)
                    ? string.Empty
                    : generatedRules.EndsWith(newline, StringComparison.Ordinal)
                        ? newline
                        : newline + newline;
                return generatedRules + separator + block + newline;
            }

            if (beginCount != 1 || endCount != 1)
            {
                throw new BuildFailedException(
                    "Appstack Android build: the generated ProGuard file contains a " +
                    "malformed Appstack rules block.");
            }

            var beginIndex = generatedRules.IndexOf(BeginMarker, StringComparison.Ordinal);
            var endIndex = generatedRules.IndexOf(EndMarker, StringComparison.Ordinal);
            if (endIndex < beginIndex)
            {
                throw new BuildFailedException(
                    "Appstack Android build: the generated ProGuard file contains a " +
                    "malformed Appstack rules block.");
            }

            return generatedRules.Substring(0, beginIndex) +
                   block +
                   generatedRules.Substring(endIndex + EndMarker.Length);
        }

        private static string NormalizeLineEndings(string value, string newline)
        {
            return value.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", newline);
        }

        private static int CountOccurrences(string value, string search)
        {
            var count = 0;
            var index = 0;
            while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += search.Length;
            }

            return count;
        }
    }
}
#endif
