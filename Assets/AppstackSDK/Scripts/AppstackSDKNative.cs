using System;
using System.Collections.Generic;
using UnityEngine;

namespace Appstack
{
    /// <summary>
    /// Platform-specific native bridge. Do not call directly; use <see cref="AppstackSDK"/>.
    /// </summary>
    internal static class AppstackSDKNative
    {
#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _AppstackUnity_Configure(
            string apiKey,
            bool isDebug,
            string endpointBaseUrl,
            int logLevel,
            string customerUserId);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _AppstackUnity_SendEvent(
            string eventType,
            string eventName,
            string parametersJson);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _AppstackUnity_EnableAppleAdsAttribution();

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern IntPtr _AppstackUnity_GetAppstackId();

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern bool _AppstackUnity_IsSdkDisabled();

        private delegate void GetAttributionParamsCompletion(IntPtr paramsJson, IntPtr error);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _AppstackUnity_GetAttributionParams(
            GetAttributionParamsCompletion completion);

        private static Action<Dictionary<string, object>> _pendingAttributionSuccess;
        private static Action<string> _pendingAttributionError;
        private static readonly object _attributionLock = new object();

        [AOT.MonoPInvokeCallback(typeof(GetAttributionParamsCompletion))]
        private static void OnGetAttributionParams(IntPtr paramsJson, IntPtr error)
        {
            lock (_attributionLock)
            {
                var onSuccess = _pendingAttributionSuccess;
                var onError = _pendingAttributionError;
                _pendingAttributionSuccess = null;
                _pendingAttributionError = null;

                if (error != IntPtr.Zero && MarshalUtility.PtrToString(error) != null)
                {
                    onError?.Invoke(MarshalUtility.PtrToString(error));
                    return;
                }
                var json = paramsJson != IntPtr.Zero ? MarshalUtility.PtrToString(paramsJson) : "{}";
                try
                {
                    var dict = JsonUtilityJson.Parse(json ?? "{}");
                    onSuccess?.Invoke(dict);
                }
                catch (Exception e)
                {
                    onError?.Invoke(e.Message);
                }
            }
        }

        public static void Configure(
            string apiKey,
            bool isDebug,
            string endpointBaseUrl,
            int logLevel,
            string customerUserId)
        {
            _AppstackUnity_Configure(
                apiKey ?? "",
                isDebug,
                endpointBaseUrl ?? "",
                logLevel,
                customerUserId ?? "");
        }

        public static void SendEvent(string eventType, string eventName, string parametersJson)
        {
            _AppstackUnity_SendEvent(
                eventType ?? "",
                eventName ?? "",
                parametersJson ?? "{}");
        }

        public static void EnableAppleAdsAttribution()
        {
            _AppstackUnity_EnableAppleAdsAttribution();
        }

        public static string GetAppstackId()
        {
            var ptr = _AppstackUnity_GetAppstackId();
            return ptr != IntPtr.Zero ? MarshalUtility.PtrToString(ptr) : null;
        }

        public static bool IsSdkDisabled()
        {
            return _AppstackUnity_IsSdkDisabled();
        }

        public static void GetAttributionParams(Action<Dictionary<string, object>> onSuccess, Action<string> onError)
        {
            lock (_attributionLock)
            {
                _pendingAttributionSuccess = onSuccess;
                _pendingAttributionError = onError;
            }
            _AppstackUnity_GetAttributionParams(OnGetAttributionParams);
        }
#elif UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaClass _bridgeClass;

        private static AndroidJavaClass BridgeClass
        {
            get
            {
                if (_bridgeClass == null)
                    _bridgeClass = new AndroidJavaClass("com.appstack.unity.AppstackUnityBridge");
                return _bridgeClass;
            }
        }

        public static void Configure(
            string apiKey,
            bool isDebug,
            string endpointBaseUrl,
            int logLevel,
            string customerUserId)
        {
            BridgeClass.CallStatic(
                "configure",
                apiKey ?? "",
                isDebug,
                endpointBaseUrl ?? "",
                logLevel,
                customerUserId ?? "");
        }

        public static void SendEvent(string eventType, string eventName, string parametersJson)
        {
            BridgeClass.CallStatic("sendEvent", eventType ?? "", eventName ?? "", parametersJson ?? "{}");
        }

        public static void EnableAppleAdsAttribution()
        {
            // No-op on Android
            BridgeClass.CallStatic("enableAppleAdsAttribution");
        }

        public static string GetAppstackId()
        {
            return BridgeClass.CallStatic<string>("getAppstackId");
        }

        public static bool IsSdkDisabled()
        {
            return BridgeClass.CallStatic<bool>("isSdkDisabled");
        }

        public static void GetAttributionParams(Action<Dictionary<string, object>> onSuccess, Action<string> onError)
        {
            try
            {
                var json = BridgeClass.CallStatic<string>("getAttributionParams");
                var dict = string.IsNullOrEmpty(json) ? new Dictionary<string, object>() : JsonUtilityJson.Parse(json);
                onSuccess?.Invoke(dict);
            }
            catch (Exception e)
            {
                onError?.Invoke(e.Message);
            }
        }
#else
        public static void Configure(
            string apiKey,
            bool isDebug,
            string endpointBaseUrl,
            int logLevel,
            string customerUserId)
        {
            Debug.Log("[AppstackSDK] Configure is only supported on iOS and Android.");
        }

        public static void SendEvent(string eventType, string eventName, string parametersJson)
        {
            Debug.Log($"[AppstackSDK] SendEvent (editor/unsupported): {eventType}");
        }

        public static void EnableAppleAdsAttribution()
        {
            Debug.Log("[AppstackSDK] EnableAppleAdsAttribution is iOS only.");
        }

        public static string GetAppstackId()
        {
            return null;
        }

        public static bool IsSdkDisabled()
        {
            return true;
        }

        public static void GetAttributionParams(Action<Dictionary<string, object>> onSuccess, Action<string> onError)
        {
            onSuccess?.Invoke(new Dictionary<string, object>());
        }
#endif
    }

    internal static class MarshalUtility
    {
        public static string PtrToString(IntPtr ptr)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(ptr);
#else
            return null;
#endif
        }
    }

    /// <summary>
    /// Simple JSON parsing for attribution params (Dictionary&lt;string, object&gt;) without external deps.
    /// Handles only flat key-value objects with string/number/bool values.
    /// </summary>
    internal static class JsonUtilityJson
    {
        public static Dictionary<string, object> Parse(string json)
        {
            var result = new Dictionary<string, object>();
            if (string.IsNullOrWhiteSpace(json) || json == "{}")
                return result;
            json = json.Trim();
            if (json.StartsWith("{") && json.EndsWith("}"))
                json = json.Substring(1, json.Length - 2);
            var pairs = SplitTopLevel(json, ',');
            foreach (var pair in pairs)
            {
                var colon = pair.IndexOf(':');
                if (colon <= 0)
                    continue;
                var key = Unquote(pair.Substring(0, colon).Trim());
                var valueStr = pair.Substring(colon + 1).Trim();
                result[key] = ParseValue(valueStr);
            }
            return result;
        }

        private static object ParseValue(string valueStr)
        {
            if (valueStr.StartsWith("\"") && valueStr.EndsWith("\""))
                return Unquote(valueStr);
            if (valueStr == "true") return true;
            if (valueStr == "false") return false;
            if (valueStr == "null") return null;
            if (long.TryParse(valueStr, out var l)) return l;
            if (double.TryParse(valueStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var d)) return d;
            return valueStr;
        }

        private static string Unquote(string s)
        {
            if (s.Length >= 2 && s.StartsWith("\"") && s.EndsWith("\""))
                return s.Substring(1, s.Length - 2).Replace("\\\"", "\"");
            return s;
        }

        private static List<string> SplitTopLevel(string json, char sep)
        {
            var list = new List<string>();
            int depth = 0;
            int start = 0;
            for (int i = 0; i < json.Length; i++)
            {
                var c = json[i];
                if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') depth--;
                else if (depth == 0 && c == sep)
                {
                    list.Add(json.Substring(start, i - start));
                    start = i + 1;
                }
            }
            if (start < json.Length)
                list.Add(json.Substring(start));
            return list;
        }
    }
}
