using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Appstack.Editor
{
    internal sealed class AppstackBuildValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            var settings = AppstackSettingsAsset.Load();
            Validate(
                settings,
                ToTargetPlatform(report.summary.platform),
                (report.summary.options & BuildOptions.Development) != 0);
        }

        internal static void Validate(
            AppstackAutoInitializationSettings settings,
            AppstackTargetPlatform platform,
            bool developmentBuild)
        {
            if (settings == null ||
                !settings.AutoInitialize ||
                platform == AppstackTargetPlatform.Unsupported)
            {
                return;
            }

            var resolved = settings.Resolve(platform, developmentBuild);
            if (!resolved.PlatformEnabled)
            {
                return;
            }

            if (!resolved.HasApiKey)
            {
                throw new BuildFailedException(
                    $"Appstack auto-initialization is enabled for {platform}, but no " +
                    $"{resolved.Environment} API key is configured. Add the key under " +
                    "Edit > Project Settings > Appstack, configure the production fallback " +
                    "for development builds, or disable Appstack for this platform.");
            }
        }

        internal static AppstackTargetPlatform ToTargetPlatform(BuildTarget target)
        {
            return target switch
            {
                BuildTarget.iOS => AppstackTargetPlatform.IOS,
                BuildTarget.Android => AppstackTargetPlatform.Android,
                _ => AppstackTargetPlatform.Unsupported,
            };
        }
    }
}
