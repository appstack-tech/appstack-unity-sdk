using System;
using System.Collections.Generic;
using UnityEngine;

namespace Appstack
{
    /// <summary>
    /// Platform dispatcher used by <see cref="AppstackSDK"/>.
    /// </summary>
    internal static class AppstackSDKNative
    {
        public static void Configure(string apiKey, int logLevel, string customerUserId)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            AppstackAndroidBridge.Configure(apiKey, logLevel, customerUserId);
#elif UNITY_IOS && !UNITY_EDITOR
            AppstackIOSBridge.Configure(apiKey, logLevel, customerUserId);
#else
            Debug.Log("[AppstackSDK] Configure is only supported on iOS and Android.");
#endif
        }

        public static void SendEvent(string eventType, string eventName, string parametersJson)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            AppstackAndroidBridge.SendEvent(eventType, eventName, parametersJson);
#elif UNITY_IOS && !UNITY_EDITOR
            AppstackIOSBridge.SendEvent(eventType, eventName, parametersJson);
#else
            Debug.Log($"[AppstackSDK] SendEvent (editor/unsupported): {eventType}");
#endif
        }

        public static void EnableAppleAdsAttribution()
        {
#if UNITY_IOS && !UNITY_EDITOR
            AppstackIOSBridge.EnableAppleAdsAttribution();
#endif
        }

        public static string GetAppstackId()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return AppstackAndroidBridge.GetAppstackId();
#elif UNITY_IOS && !UNITY_EDITOR
            return AppstackIOSBridge.GetAppstackId();
#else
            return null;
#endif
        }

        public static bool IsSdkDisabled()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return AppstackAndroidBridge.IsSdkDisabled();
#elif UNITY_IOS && !UNITY_EDITOR
            return AppstackIOSBridge.IsSdkDisabled();
#else
            return true;
#endif
        }

        public static void GetAttributionParams(
            Action<Dictionary<string, object>> onSuccess,
            Action<string> onError)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            AppstackAndroidBridge.GetAttributionParams(onSuccess, onError);
#elif UNITY_IOS && !UNITY_EDITOR
            AppstackIOSBridge.GetAttributionParams(onSuccess, onError);
#else
            onSuccess?.Invoke(new Dictionary<string, object>());
#endif
        }
    }
}
