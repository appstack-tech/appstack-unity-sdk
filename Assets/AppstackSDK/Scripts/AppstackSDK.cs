using System;
using System.Collections.Generic;
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
        /// <summary>
        /// Configure the SDK with your API key and optional parameters.
        /// Must be called before any other SDK methods.
        /// </summary>
        /// <param name="apiKey">Your Appstack API key from the dashboard.</param>
        /// <param name="isDebug">Enable debug mode (optional, default false).</param>
        /// <param name="endpointBaseUrl">Custom endpoint base URL (optional).</param>
        /// <param name="logLevel">Log level: 0=DEBUG, 1=INFO, 2=WARN, 3=ERROR (optional, default 1).</param>
        /// <param name="customerUserId">Optional customer user ID (optional).</param>
        public static void Configure(
            string apiKey,
            bool isDebug = false,
            string endpointBaseUrl = null,
            int logLevel = 1,
            string customerUserId = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key must be a non-empty string", nameof(apiKey));
            if (logLevel < 0 || logLevel > 3)
                throw new ArgumentOutOfRangeException(nameof(logLevel), "logLevel must be between 0 and 3");

            try
            {
                AppstackSDKNative.Configure(
                    apiKey.Trim(),
                    isDebug,
                    endpointBaseUrl?.Trim() ?? "",
                    logLevel,
                    customerUserId?.Trim() ?? "");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AppstackSDK] Configure failed: {e.Message}");
                throw;
            }

#if !UNITY_EDITOR
            try
            {
                var disabled = AppstackSDKNative.IsSdkDisabled();
                if (disabled)
                    Debug.LogWarning("[AppstackSDK] SDK is disabled. Please check your API key.");
                else
                    Debug.Log("[AppstackSDK] SDK enabled and ready to track events.");
            }
            catch { /* ignore */ }
#endif
        }

        /// <summary>
        /// Send an event with optional parameters.
        /// </summary>
        /// <param name="eventType">Event type from the EventType enum (required).</param>
        /// <param name="eventName">Event name for custom events (optional; required when eventType is CUSTOM).</param>
        /// <param name="parameters">Optional parameters (e.g. revenue, currency).</param>
        public static void SendEvent(
            EventType eventType,
            string eventName = null,
            Dictionary<string, object> parameters = null)
        {
            var eventTypeStr = eventType.ToString();
            if (eventType == EventType.CUSTOM && string.IsNullOrWhiteSpace(eventName))
                throw new ArgumentException("eventName is required when eventType is CUSTOM", nameof(eventName));

            var parametersJson = ParametersToJson(parameters);
            try
            {
                AppstackSDKNative.SendEvent(eventTypeStr, eventName ?? "", parametersJson);
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
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                AppstackSDKNative.EnableAppleAdsAttribution();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AppstackSDK] EnableAppleAdsAttribution failed: {e.Message}");
                throw;
            }
#else
            Debug.Log("[AppstackSDK] EnableAppleAdsAttribution is only supported on iOS.");
#endif
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

            try
            {
                AppstackSDKNative.GetAttributionParams(onSuccess, onError ?? (_ => { }));
            }
            catch (Exception e)
            {
                Debug.LogError($"[AppstackSDK] GetAttributionParams failed: {e.Message}");
                onError?.Invoke(e.Message);
            }
        }

        private static string ParametersToJson(Dictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0)
                return "{}";

            var parts = new System.Text.StringBuilder();
            parts.Append('{');
            var first = true;
            foreach (var kv in parameters)
            {
                if (!first) parts.Append(',');
                first = false;
                parts.Append('"');
                parts.Append(EscapeJsonString(kv.Key));
                parts.Append("\":");
                parts.Append(ValueToJson(kv.Value));
            }
            parts.Append('}');
            return parts.ToString();
        }

        private static string ValueToJson(object value)
        {
            if (value == null) return "null";
            if (value is string s) return "\"" + EscapeJsonString(s) + "\"";
            if (value is bool b) return b ? "true" : "false";
            if (value is int || value is long || value is float || value is double)
                return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            return "\"" + EscapeJsonString(value.ToString()) + "\"";
        }

        private static string EscapeJsonString(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }
}
