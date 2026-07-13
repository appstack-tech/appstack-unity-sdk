#if UNITY_EDITOR && UNITY_ANDROID
using System;
using System.IO;
using System.Text;
using UnityEditor.Android;
using UnityEditor.Build;

namespace Appstack.Editor
{
    internal sealed partial class AppstackAndroidPostGenerateGradleProject
        : IPostGenerateGradleAndroidProject
    {
        private const string SourceRulesPath =
            "Runtime/Plugins/Android/proguard-user.txt";
        private const string GeneratedRulesFileName = "proguard-unity.txt";
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

    }
}
#endif
