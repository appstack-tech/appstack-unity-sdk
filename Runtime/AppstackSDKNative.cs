using System;
using System.Collections.Generic;
using UnityEngine;

namespace Appstack
{
    /// <summary>
    /// Owns the active platform implementation used by <see cref="AppstackSDK"/>.
    /// </summary>
    internal static class AppstackSDKNative
    {
        private static IAppstackNativeBridge bridge = CreateDefaultBridge();

        public static bool ReportsConfigurationStatus => bridge.ReportsConfigurationStatus;

        public static void Configure(string apiKey, int logLevel, string customerUserId)
        {
            bridge.Configure(apiKey, logLevel, customerUserId);
        }

        public static void SendEvent(string eventType, string eventName, string parametersJson)
        {
            bridge.SendEvent(eventType, eventName, parametersJson);
        }

        public static void EnableAppleAdsAttribution()
        {
            bridge.EnableAppleAdsAttribution();
        }

        public static string GetAppstackId()
        {
            return bridge.GetAppstackId();
        }

        public static bool IsSdkDisabled()
        {
            return bridge.IsSdkDisabled();
        }

        public static void GetAttributionParams(
            Action<Dictionary<string, object>> onSuccess,
            Action<string> onError)
        {
            bridge.GetAttributionParams(onSuccess, onError);
        }

        /// <summary>
        /// Temporarily replaces the platform bridge for an internal test assembly.
        /// Disposing the returned scope restores the previous bridge.
        /// </summary>
        internal static IDisposable OverrideBridgeForTesting(IAppstackNativeBridge replacement)
        {
            if (replacement == null)
            {
                throw new ArgumentNullException(nameof(replacement));
            }

            var previous = bridge;
            bridge = replacement;
            return new BridgeOverrideScope(previous);
        }

        private static IAppstackNativeBridge CreateDefaultBridge()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new AndroidNativeBridge();
#elif UNITY_IOS && !UNITY_EDITOR
            return new IOSNativeBridge();
#else
            return new UnsupportedNativeBridge();
#endif
        }

        private sealed class BridgeOverrideScope : IDisposable
        {
            private IAppstackNativeBridge previous;

            public BridgeOverrideScope(IAppstackNativeBridge previousBridge)
            {
                previous = previousBridge;
            }

            public void Dispose()
            {
                if (previous == null)
                {
                    return;
                }

                bridge = previous;
                previous = null;
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private sealed class AndroidNativeBridge : IAppstackNativeBridge
        {
            public bool ReportsConfigurationStatus => true;

            public void Configure(string apiKey, int logLevel, string customerUserId)
            {
                AppstackAndroidBridge.Configure(
                    apiKey,
                    logLevel,
                    customerUserId,
                    AppstackVersion.WrapperVersion);
            }

            public void SendEvent(string eventType, string eventName, string parametersJson)
            {
                AppstackAndroidBridge.SendEvent(eventType, eventName, parametersJson);
            }

            public void EnableAppleAdsAttribution()
            {
                // Intentionally ignored on Android.
            }

            public string GetAppstackId()
            {
                return AppstackAndroidBridge.GetAppstackId();
            }

            public bool IsSdkDisabled()
            {
                return AppstackAndroidBridge.IsSdkDisabled();
            }

            public void GetAttributionParams(
                Action<Dictionary<string, object>> onSuccess,
                Action<string> onError)
            {
                AppstackAndroidBridge.GetAttributionParams(onSuccess, onError);
            }
        }
#elif UNITY_IOS && !UNITY_EDITOR
        private sealed class IOSNativeBridge : IAppstackNativeBridge
        {
            public bool ReportsConfigurationStatus => true;

            public void Configure(string apiKey, int logLevel, string customerUserId)
            {
                AppstackIOSBridge.Configure(
                    apiKey,
                    logLevel,
                    customerUserId,
                    AppstackVersion.WrapperVersion);
            }

            public void SendEvent(string eventType, string eventName, string parametersJson)
            {
                AppstackIOSBridge.SendEvent(eventType, eventName, parametersJson);
            }

            public void EnableAppleAdsAttribution()
            {
                AppstackIOSBridge.EnableAppleAdsAttribution();
            }

            public string GetAppstackId()
            {
                return AppstackIOSBridge.GetAppstackId();
            }

            public bool IsSdkDisabled()
            {
                return AppstackIOSBridge.IsSdkDisabled();
            }

            public void GetAttributionParams(
                Action<Dictionary<string, object>> onSuccess,
                Action<string> onError)
            {
                AppstackIOSBridge.GetAttributionParams(onSuccess, onError);
            }
        }
#else
        private sealed class UnsupportedNativeBridge : IAppstackNativeBridge
        {
            public bool ReportsConfigurationStatus => false;

            public void Configure(string apiKey, int logLevel, string customerUserId)
            {
                // Intentionally ignored outside supported mobile players.
            }

            public void SendEvent(string eventType, string eventName, string parametersJson)
            {
                // Intentionally ignored outside supported mobile players.
            }

            public void EnableAppleAdsAttribution()
            {
                // Intentionally ignored outside iOS players.
            }

            public string GetAppstackId()
            {
                return null;
            }

            public bool IsSdkDisabled()
            {
                return true;
            }

            public void GetAttributionParams(
                Action<Dictionary<string, object>> onSuccess,
                Action<string> onError)
            {
                onSuccess?.Invoke(new Dictionary<string, object>());
            }
        }
#endif
    }
}
