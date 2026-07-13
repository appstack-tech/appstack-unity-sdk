#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Appstack
{
    internal static class AppstackAndroidBridge
    {
        private const string UnityBridgeClassName = "com.appstack.unity.AppstackUnityBridge";
        private const string CallbackInterfaceName =
            "com.appstack.unity.AppstackUnityBridge$AttributionParamsCallback";

        private static readonly object CallbackLock = new object();
        private static readonly Dictionary<int, PendingCallback> Callbacks =
            new Dictionary<int, PendingCallback>();
        private static readonly AttributionCallbackProxy NativeCallback =
            new AttributionCallbackProxy();
        private static readonly Lazy<AndroidJavaClass> UnityBridge =
            new Lazy<AndroidJavaClass>(() => new AndroidJavaClass(UnityBridgeClassName));
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

        public static void Configure(
            string apiKey,
            int logLevel,
            string customerUserId,
            string wrapperVersion)
        {
            using (var context = GetApplicationContext())
            {
                UnityBridge.Value.CallStatic(
                    "configure",
                    context,
                    apiKey,
                    wrapperVersion,
                    logLevel,
                    customerUserId ?? string.Empty);
            }
        }

        public static void SendEvent(string eventType, string eventName, string parametersJson)
        {
            UnityBridge.Value.CallStatic(
                "sendEvent",
                eventType,
                eventName ?? string.Empty,
                parametersJson ?? "{}");
        }

        public static string GetAppstackId()
        {
            return UnityBridge.Value.CallStatic<string>("getAppstackId");
        }

        public static bool IsSdkDisabled()
        {
            return UnityBridge.Value.CallStatic<bool>("isSdkDisabled");
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
                UnityBridge.Value.CallStatic(
                    "awaitAttributionParams",
                    requestId,
                    NativeCallback);
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
    }
}
#endif
