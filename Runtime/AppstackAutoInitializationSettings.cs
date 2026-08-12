using System;
using UnityEngine;

namespace Appstack
{
    internal enum AppstackEnvironmentMode
    {
        Automatic = 0,
        Development = 1,
        Production = 2,
    }

    internal enum AppstackResolvedEnvironment
    {
        Development = 0,
        Production = 1,
    }

    internal enum AppstackTargetPlatform
    {
        Unsupported = 0,
        IOS = 1,
        Android = 2,
    }

    internal enum AppstackLogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3,
    }

    internal readonly struct AppstackResolvedConfiguration
    {
        public AppstackResolvedConfiguration(
            bool platformEnabled,
            AppstackResolvedEnvironment environment,
            string apiKey,
            bool usedProductionFallback)
        {
            PlatformEnabled = platformEnabled;
            Environment = environment;
            ApiKey = apiKey;
            UsedProductionFallback = usedProductionFallback;
        }

        public bool PlatformEnabled { get; }
        public AppstackResolvedEnvironment Environment { get; }
        public string ApiKey { get; }
        public bool UsedProductionFallback { get; }
        public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);
    }

    internal sealed class AppstackAutoInitializationSettings : ScriptableObject
    {
        internal const string ResourceName = "AppstackSettings";
        internal const string AssetPath = "Assets/Appstack/Resources/AppstackSettings.asset";

        [SerializeField] private bool autoInitialize = true;
        [SerializeField] private bool iosEnabled = true;
        [SerializeField] private bool androidEnabled = true;
        [SerializeField] private AppstackEnvironmentMode environmentMode =
            AppstackEnvironmentMode.Automatic;
        [SerializeField] private bool allowProductionFallback = false;
        [SerializeField] private AppstackLogLevel logLevel = AppstackLogLevel.Info;
        [SerializeField] private bool enableAppleAdsAttribution = false;
        [SerializeField] private string iosDevelopmentApiKey = "";
        [SerializeField] private string iosProductionApiKey = "";
        [SerializeField] private string androidDevelopmentApiKey = "";
        [SerializeField] private string androidProductionApiKey = "";

        internal bool AutoInitialize => autoInitialize;
        internal bool IOSEnabled => iosEnabled;
        internal bool AndroidEnabled => androidEnabled;
        internal AppstackEnvironmentMode EnvironmentMode => environmentMode;
        internal bool AllowProductionFallback => allowProductionFallback;
        internal int LogLevel => (int)logLevel;
        internal bool EnableAppleAdsAttribution => enableAppleAdsAttribution;
        internal string IOSDevelopmentApiKey => iosDevelopmentApiKey;
        internal string IOSProductionApiKey => iosProductionApiKey;
        internal string AndroidDevelopmentApiKey => androidDevelopmentApiKey;
        internal string AndroidProductionApiKey => androidProductionApiKey;

        internal AppstackResolvedConfiguration Resolve(
            AppstackTargetPlatform platform,
            bool developmentBuild)
        {
            return AppstackSettingsResolver.Resolve(this, platform, developmentBuild);
        }
    }

    internal static class AppstackSettingsResolver
    {
        internal static AppstackResolvedConfiguration Resolve(
            AppstackAutoInitializationSettings settings,
            AppstackTargetPlatform platform,
            bool developmentBuild)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var enabled = platform switch
            {
                AppstackTargetPlatform.IOS => settings.IOSEnabled,
                AppstackTargetPlatform.Android => settings.AndroidEnabled,
                _ => false,
            };

            var environment = settings.EnvironmentMode switch
            {
                AppstackEnvironmentMode.Development =>
                    AppstackResolvedEnvironment.Development,
                AppstackEnvironmentMode.Production =>
                    AppstackResolvedEnvironment.Production,
                _ => developmentBuild
                    ? AppstackResolvedEnvironment.Development
                    : AppstackResolvedEnvironment.Production,
            };

            if (!enabled)
            {
                return new AppstackResolvedConfiguration(enabled, environment, null, false);
            }

            var developmentKey = platform switch
            {
                AppstackTargetPlatform.IOS => settings.IOSDevelopmentApiKey,
                AppstackTargetPlatform.Android => settings.AndroidDevelopmentApiKey,
                _ => null,
            };
            var productionKey = platform switch
            {
                AppstackTargetPlatform.IOS => settings.IOSProductionApiKey,
                AppstackTargetPlatform.Android => settings.AndroidProductionApiKey,
                _ => null,
            };

            if (environment == AppstackResolvedEnvironment.Production)
            {
                return new AppstackResolvedConfiguration(
                    true,
                    environment,
                    Normalize(productionKey),
                    false);
            }

            var normalizedDevelopmentKey = Normalize(developmentKey);
            if (!string.IsNullOrEmpty(normalizedDevelopmentKey))
            {
                return new AppstackResolvedConfiguration(
                    true,
                    environment,
                    normalizedDevelopmentKey,
                    false);
            }

            var normalizedProductionKey = Normalize(productionKey);
            var usesFallback = settings.AllowProductionFallback &&
                               !string.IsNullOrEmpty(normalizedProductionKey);
            return new AppstackResolvedConfiguration(
                true,
                environment,
                usesFallback ? normalizedProductionKey : null,
                usesFallback);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
