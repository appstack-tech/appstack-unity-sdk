#if UNITY_EDITOR && UNITY_ANDROID
using System;
using System.IO;
using System.Text;
using UnityEditor.Android;
using UnityEditor.Build;

namespace Appstack.Editor
{
    internal sealed class AppstackAndroidPostGenerateGradleProject
        : IPostGenerateGradleAndroidProject
    {
        private const string SourceRulesPath =
            "Runtime/Plugins/Android/proguard-user.txt";
        private const string GeneratedRulesFileName = "proguard-unity.txt";
        private const string BeginMarker = "# BEGIN Appstack Unity SDK";
        private const string EndMarker = "# END Appstack Unity SDK";

        public int callbackOrder => 999;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(AppstackAndroidPostGenerateGradleProject).Assembly);
            if (packageInfo == null)
            {
                throw new BuildFailedException(
                    "Appstack Android build: unable to resolve the package path.");
            }

            var sourcePath = Path.Combine(packageInfo.resolvedPath, SourceRulesPath);
            if (!File.Exists(sourcePath))
            {
                throw new BuildFailedException(
                    $"Appstack Android build: ProGuard rules are missing at {sourcePath}.");
            }

            var generatedPath = Path.Combine(path, GeneratedRulesFileName);
            if (!File.Exists(generatedPath))
            {
                throw new BuildFailedException(
                    $"Appstack Android build: generated ProGuard file is missing at " +
                    $"{generatedPath}.");
            }

            var sourceRules = File.ReadAllText(sourcePath).Trim();
            if (string.IsNullOrEmpty(sourceRules))
            {
                throw new BuildFailedException(
                    $"Appstack Android build: ProGuard rules are empty at {sourcePath}.");
            }

            var generatedBytes = File.ReadAllBytes(generatedPath);
            var hasUtf8Bom = generatedBytes.Length >= 3 &&
                             generatedBytes[0] == 0xEF &&
                             generatedBytes[1] == 0xBB &&
                             generatedBytes[2] == 0xBF;
            var generatedRules = File.ReadAllText(generatedPath);
            var mergedRules = MergeRules(generatedRules, sourceRules);
            if (string.Equals(generatedRules, mergedRules, StringComparison.Ordinal))
            {
                return;
            }

            File.WriteAllText(generatedPath, mergedRules, new UTF8Encoding(hasUtf8Bom));
        }

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
