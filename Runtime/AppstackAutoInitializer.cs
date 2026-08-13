using UnityEngine;
using UnityEngine.Scripting;

namespace Appstack
{
    [Preserve]
    internal static class AppstackAutoInitializer
    {
        [Preserve]
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            var settings = Resources.Load<AppstackAutoInitializationSettings>(
                AppstackAutoInitializationSettings.ResourceName);
            if (settings == null || !settings.AutoInitialize)
            {
                return;
            }

#if UNITY_IOS
            const AppstackTargetPlatform platform = AppstackTargetPlatform.IOS;
#else
            const AppstackTargetPlatform platform = AppstackTargetPlatform.Android;
#endif
            var resolved = settings.Resolve(platform, Debug.isDebugBuild);
            if (!resolved.PlatformEnabled)
            {
                return;
            }

            if (!resolved.HasApiKey)
            {
                Debug.LogError(
                    $"[AppstackSDK] Auto-initialization skipped: no " +
                    $"{resolved.Environment} API key is configured for {platform}.");
                return;
            }

            if (!AppstackSDK.ConfigureAutomatically(resolved.ApiKey, settings.LogLevel))
            {
                return;
            }

#if UNITY_IOS
            if (settings.EnableAppleAdsAttribution)
            {
                AppstackSDK.EnableAppleAdsAttribution();
            }
#endif
            Debug.Log(
                $"[AppstackSDK] Auto-initialized for {platform} / " +
                $"{resolved.Environment}" +
                (resolved.UsedProductionFallback ? " using the production-key fallback." : "."));
#endif
        }
    }
}
