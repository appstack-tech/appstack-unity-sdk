using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Appstack
{
    /// <summary>
    /// Main Appstack SDK class for Unity. Same API surface as Flutter and React Native SDKs.
    /// </summary>
    /// <example>
    /// <code>
    /// // Configure the SDK
    /// AppstackSDK.Configure("your-api-key");
    ///
    /// // Set the customer user ID later (e.g. on login), or clear it on logout
    /// AppstackSDK.SetCustomerUserId("user-123");
    /// AppstackSDK.ClearCustomerUserId();
    ///
    /// // Send events
    /// AppstackSDK.SendEvent(EventType.PURCHASE, parameters: new Dictionary&lt;string, object&gt; { { "revenue", 29.99 }, { "currency", "USD" } });
    ///
    /// // Enable Apple Ads Attribution (iOS only)
    /// #if UNITY_IOS &amp;&amp; !UNITY_EDITOR
    /// AppstackSDK.EnableAppleAdsAttribution();
    /// #endif
    /// </code>
    /// </example>
    public static class AppstackSDK
    {
        private static readonly object ConfigurationGate = new object();
        private static ConfigurationRecord activeConfiguration;

        /// <summary>
        /// Configure the SDK with your API key and optional parameters.
        /// Must be called before any other SDK methods.
        /// </summary>
        /// <param name="apiKey">Your Appstack API key from the dashboard.</param>
        /// <param name="logLevel">Log level: 0=DEBUG, 1=INFO, 2=WARN, 3=ERROR (optional, default 1).
        /// iOS has no dedicated WARN tier, so 2 behaves like 3 there.</param>
        /// <param name="customerUserId">Optional customer user ID (optional).</param>
        public static void Configure(
            string apiKey,
            int logLevel = 1,
            string customerUserId = null)
        {
            ConfigureInternal(apiKey, logLevel, customerUserId);
        }

        internal static bool ConfigureAutomatically(string apiKey, int logLevel)
        {
            return ConfigureInternal(apiKey, logLevel, null);
        }

        private static bool ConfigureInternal(
            string apiKey,
            int logLevel,
            string customerUserId)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key must be a non-empty string", nameof(apiKey));
            if (logLevel < 0 || logLevel > 3)
                throw new ArgumentOutOfRangeException(nameof(logLevel), "logLevel must be between 0 and 3");

            var normalizedApiKey = apiKey.Trim();
            var normalizedCustomerUserId = customerUserId?.Trim() ?? "";

            lock (ConfigurationGate)
            {
                if (activeConfiguration != null)
                {
                    var matches = activeConfiguration.Matches(
                        normalizedApiKey,
                        logLevel,
                        normalizedCustomerUserId);
                    if (!matches)
                    {
                        Debug.LogWarning(
                            "[AppstackSDK] Configure ignored because the SDK is already " +
                            "configured with different settings.");
                    }

                    return matches;
                }

                try
                {
                    AppstackSDKNative.Configure(
                        normalizedApiKey,
                        logLevel,
                        normalizedCustomerUserId);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AppstackSDK] Configure failed: {e.Message}");
                    throw;
                }

                activeConfiguration = new ConfigurationRecord(
                    normalizedApiKey,
                    logLevel,
                    normalizedCustomerUserId);
            }

            if (AppstackSDKNative.ReportsConfigurationStatus)
            {
                try
                {
                    var disabled = AppstackSDKNative.IsSdkDisabled();
                    if (disabled)
                        Debug.LogWarning("[AppstackSDK] SDK is disabled. Please check your API key.");
                    else
                        Debug.Log("[AppstackSDK] SDK enabled and ready to track events.");
                }
                catch
                {
                    // Configuration itself succeeded; status reporting is best-effort.
                }
            }

            return true;
        }

        /// <summary>
        /// Set — or clear — the customer user ID after <see cref="Configure"/>, e.g. once a
        /// login reveals it. A repeat <see cref="Configure"/> is a no-op, so it cannot be
        /// used to change the ID. Safe to call at any time; last write wins.
        /// </summary>
        /// <param name="customerUserId">Your identifier for the signed-in user. `null`, an
        /// empty string, or whitespace clears it — see <see cref="ClearCustomerUserId"/>.</param>
        public static void SetCustomerUserId(string customerUserId)
        {
            try
            {
                // "" is the clear marker on this entry point. It is unambiguous here because
                // the setter has its own native function — on Configure, "" means "not
                // provided" instead, and never clears. Sending "" rather than null also keeps
                // the marshalled argument well-defined on both platforms.
                AppstackSDKNative.SetCustomerUserId(customerUserId?.Trim() ?? "");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AppstackSDK] SetCustomerUserId failed: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Clear the stored customer user ID — call this on logout, otherwise the previous
        /// user's ID stays attached to every later event. Equivalent to
        /// <c>SetCustomerUserId(null)</c>.
        /// </summary>
        public static void ClearCustomerUserId()
        {
            SetCustomerUserId(null);
        }

        /// <summary>
        /// Send an event with optional parameters.
        /// </summary>
        /// <param name="eventType">Event type from the EventType enum (required).</param>
        /// <param name="eventName">Event name required for CUSTOM events; ignored for standard events.</param>
        /// <param name="parameters">Optional JSON-compatible parameters. Supports strings,
        /// Booleans, finite numbers, nulls, nested string-keyed dictionaries, and arrays.</param>
        /// <exception cref="ArgumentException">Thrown for missing custom event names or
        /// parameter values that cannot be represented as JSON.</exception>
        public static void SendEvent(
            EventType eventType,
            string eventName = null,
            Dictionary<string, object> parameters = null)
        {
            var eventTypeStr = eventType.ToString();
            if (eventType == EventType.CUSTOM && string.IsNullOrWhiteSpace(eventName))
                throw new ArgumentException("eventName is required when eventType is CUSTOM", nameof(eventName));

            var parametersJson = AppstackJson.SerializeObject(parameters);
            try
            {
                AppstackSDKNative.SendEvent(
                    eventTypeStr,
                    NativeEventName(eventType, eventName) ?? "",
                    parametersJson);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AppstackSDK] SendEvent failed: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Enable Apple Search Ads Attribution (iOS only). No-op on Android.
        /// </summary>
        public static void EnableAppleAdsAttribution()
        {
            try
            {
                AppstackSDKNative.EnableAppleAdsAttribution();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AppstackSDK] EnableAppleAdsAttribution failed: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get the Appstack ID for the current user/device.
        /// </summary>
        public static string GetAppstackId()
        {
            try
            {
                return AppstackSDKNative.GetAppstackId();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AppstackSDK] GetAppstackId failed: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Check if the SDK is disabled (e.g. invalid API key).
        /// </summary>
        public static bool IsSdkDisabled()
        {
            try
            {
                return AppstackSDKNative.IsSdkDisabled();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AppstackSDK] IsSdkDisabled failed: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get attribution parameters from the SDK (async via callbacks).
        /// </summary>
        /// <param name="onSuccess">Called with a dictionary of attribution parameters.</param>
        /// <param name="onError">Called with an error message if the request fails.</param>
        public static void GetAttributionParams(
            Action<Dictionary<string, object>> onSuccess,
            Action<string> onError = null)
        {
            if (onSuccess == null)
                throw new ArgumentNullException(nameof(onSuccess));

            var completed = 0;
            void CompleteSuccess(Dictionary<string, object> parameters)
            {
                if (Interlocked.Exchange(ref completed, 1) == 0)
                {
                    onSuccess(parameters);
                }
            }

            void CompleteError(string error)
            {
                if (Interlocked.Exchange(ref completed, 1) == 0)
                {
                    onError?.Invoke(error);
                }
            }

            try
            {
                AppstackSDKNative.GetAttributionParams(CompleteSuccess, CompleteError);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AppstackSDK] GetAttributionParams failed: {e.Message}");
                CompleteError(e.Message);
            }
        }

        internal static string NativeEventName(EventType eventType, string eventName)
        {
            return eventType == EventType.CUSTOM ? eventName : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetConfigurationState()
        {
            lock (ConfigurationGate)
            {
                activeConfiguration = null;
            }
        }

        internal static void ResetConfigurationStateForTesting()
        {
            ResetConfigurationState();
        }

        private sealed class ConfigurationRecord
        {
            private readonly string apiKey;
            private readonly int logLevel;
            private readonly string customerUserId;

            public ConfigurationRecord(
                string configuredApiKey,
                int configuredLogLevel,
                string configuredCustomerUserId)
            {
                apiKey = configuredApiKey;
                logLevel = configuredLogLevel;
                customerUserId = configuredCustomerUserId;
            }

            public bool Matches(
                string candidateApiKey,
                int candidateLogLevel,
                string candidateCustomerUserId)
            {
                return string.Equals(apiKey, candidateApiKey, StringComparison.Ordinal) &&
                       logLevel == candidateLogLevel &&
                       string.Equals(
                           customerUserId,
                           candidateCustomerUserId,
                           StringComparison.Ordinal);
            }
        }

    }
}
