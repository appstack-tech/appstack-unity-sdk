#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Appstack
{
    internal static class AppstackAndroidBridge
    {
        private const string SdkClassName = "com.appstack.attribution.AppstackAttributionSdk";
        private const string EventTypeClassName = "com.appstack.attribution.EventType";
        private const string LogLevelClassName = "com.appstack.attribution.LogLevel";
        private const string UnityBridgeClassName = "com.appstack.unity.AppstackUnityBridge";
        private const string CallbackInterfaceName =
            "com.appstack.unity.AppstackUnityBridge$AttributionParamsCallback";
        private const string WrapperVersion = "unity-1.0.0";
        private const int GetMetaDataFlag = 128;

        private static readonly object CallbackLock = new object();
        private static readonly Dictionary<int, PendingCallback> Callbacks =
            new Dictionary<int, PendingCallback>();
        private static readonly AttributionCallbackProxy NativeCallback =
            new AttributionCallbackProxy();
        private static int nextRequestId;

        private sealed class PendingCallback
        {
            public PendingCallback(
                Action<Dictionary<string, object>> onSuccess,
                Action<string> onError,
                SynchronizationContext synchronizationContext)
            {
                OnSuccess = onSuccess;
                OnError = onError;
                SynchronizationContext = synchronizationContext;
            }

            public Action<Dictionary<string, object>> OnSuccess { get; }
            public Action<string> OnError { get; }
            public SynchronizationContext SynchronizationContext { get; }
        }

        private sealed class AttributionCallbackProxy : AndroidJavaProxy
        {
            public AttributionCallbackProxy() : base(CallbackInterfaceName)
            {
            }

            public void onResult(int requestId, string json, string error)
            {
                CompleteRequest(requestId, json, error);
            }
        }

        public static void Configure(string apiKey, int logLevel, string customerUserId)
        {
            using (var sdk = new AndroidJavaClass(SdkClassName))
            using (var context = GetApplicationContext())
            using (var nativeLogLevel = GetNativeLogLevel(logLevel))
            {
                var proxyUrl = ReadDevProxyUrl(context);
                if (!string.IsNullOrWhiteSpace(proxyUrl))
                {
                    sdk.CallStatic("setProxyUrl", proxyUrl);
                }

                // The pinned SDK exposes this wrapper-only API. Do not fall back to the
                // public configure method because that would omit wrapper attribution.
                sdk.CallStatic(
                    "configureWrapper",
                    context,
                    apiKey,
                    WrapperVersion,
                    nativeLogLevel,
                    null,
                    string.IsNullOrWhiteSpace(customerUserId) ? null : customerUserId);
            }
        }

        public static void SendEvent(string eventType, string eventName, string parametersJson)
        {
            using (var sdk = new AndroidJavaClass(SdkClassName))
            using (var nativeEventType = GetNativeEventType(eventType))
            using (var parameters = JsonToJavaMap(parametersJson))
            {
                sdk.CallStatic(
                    "sendEvent",
                    nativeEventType,
                    string.IsNullOrWhiteSpace(eventName) ? null : eventName,
                    parameters);
            }
        }

        public static string GetAppstackId()
        {
            using (var sdk = new AndroidJavaClass(SdkClassName))
            {
                return sdk.CallStatic<string>("getAppstackId");
            }
        }

        public static bool IsSdkDisabled()
        {
            using (var sdk = new AndroidJavaClass(SdkClassName))
            {
                return sdk.CallStatic<bool>("isSdkDisabled");
            }
        }

        public static void GetAttributionParams(
            Action<Dictionary<string, object>> onSuccess,
            Action<string> onError)
        {
            var requestId = Interlocked.Increment(ref nextRequestId);
            lock (CallbackLock)
            {
                Callbacks[requestId] =
                    new PendingCallback(onSuccess, onError, SynchronizationContext.Current);
            }

            try
            {
                using (var bridge = new AndroidJavaClass(UnityBridgeClassName))
                {
                    bridge.CallStatic("awaitAttributionParams", requestId, NativeCallback);
                }
            }
            catch (Exception exception)
            {
                CompleteRequest(requestId, null, exception.Message);
            }
        }

        private static void CompleteRequest(int requestId, string json, string error)
        {
            PendingCallback pending;
            lock (CallbackLock)
            {
                if (!Callbacks.TryGetValue(requestId, out pending))
                {
                    return;
                }

                Callbacks.Remove(requestId);
            }

            void CompleteOnCapturedContext()
            {
                if (!string.IsNullOrEmpty(error))
                {
                    pending.OnError?.Invoke(error);
                    return;
                }

                pending.OnSuccess?.Invoke(AppstackJson.ParseObject(json));
            }

            if (pending.SynchronizationContext != null)
            {
                pending.SynchronizationContext.Post(_ => CompleteOnCapturedContext(), null);
            }
            else
            {
                CompleteOnCapturedContext();
            }
        }

        private static AndroidJavaObject GetApplicationContext()
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                if (activity == null)
                {
                    throw new InvalidOperationException("UnityPlayer.currentActivity is unavailable.");
                }

                return activity.Call<AndroidJavaObject>("getApplicationContext");
            }
        }

        private static string ReadDevProxyUrl(AndroidJavaObject context)
        {
            try
            {
                using (var packageManager = context.Call<AndroidJavaObject>("getPackageManager"))
                using (var applicationInfo = packageManager.Call<AndroidJavaObject>(
                           "getApplicationInfo",
                           context.Call<string>("getPackageName"),
                           GetMetaDataFlag))
                using (var metadata = applicationInfo.Get<AndroidJavaObject>("metaData"))
                {
                    return metadata?.Call<string>("getString", "APPSTACK_DEV_PROXY_URL");
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AndroidJavaObject GetNativeLogLevel(int logLevel)
        {
            var name = logLevel switch
            {
                0 => "DEBUG",
                1 => "INFO",
                2 => "WARN",
                3 => "ERROR",
                _ => "INFO",
            };

            using (var logLevelClass = new AndroidJavaClass(LogLevelClassName))
            {
                return logLevelClass.GetStatic<AndroidJavaObject>(name);
            }
        }

        private static AndroidJavaObject GetNativeEventType(string eventType)
        {
            using (var eventTypeClass = new AndroidJavaClass(EventTypeClassName))
            {
                try
                {
                    return eventTypeClass.GetStatic<AndroidJavaObject>(eventType);
                }
                catch (AndroidJavaException)
                {
                    return eventTypeClass.GetStatic<AndroidJavaObject>("CUSTOM");
                }
            }
        }

        private static AndroidJavaObject JsonToJavaMap(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json == "{}")
            {
                return null;
            }

            using (var bridge = new AndroidJavaClass(UnityBridgeClassName))
            {
                return bridge.CallStatic<AndroidJavaObject>("parametersFromJson", json);
            }
        }
    }
}
#endif
