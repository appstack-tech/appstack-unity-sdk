#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Appstack
{
    internal static class AppstackIOSBridge
    {
        private static readonly PendingRequestRegistry<Dictionary<string, object>> Requests =
            new PendingRequestRegistry<Dictionary<string, object>>();
        private static readonly AttributionParamsCallback NativeCallback =
            OnAttributionParamsReceived;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void AttributionParamsCallback(
            int requestId,
            IntPtr json,
            IntPtr error);

        [DllImport("__Internal")]
        private static extern void AppstackUnityConfigure(
            string apiKey,
            int logLevel,
            string customerUserId,
            string wrapperVersion);

        [DllImport("__Internal")]
        private static extern void AppstackUnitySendEvent(
            string eventType,
            string eventName,
            string parametersJson);

        [DllImport("__Internal")]
        private static extern void AppstackUnityEnableAppleAdsAttribution();

        [DllImport("__Internal")]
        private static extern IntPtr AppstackUnityGetAppstackId();

        [DllImport("__Internal")]
        private static extern int AppstackUnityIsSdkDisabled();

        [DllImport("__Internal")]
        private static extern void AppstackUnityGetAttributionParams(
            int requestId,
            AttributionParamsCallback callback);

        [DllImport("__Internal")]
        private static extern void AppstackUnityFreeCString(IntPtr value);

        public static void Configure(
            string apiKey,
            int logLevel,
            string customerUserId,
            string wrapperVersion)
        {
            AppstackUnityConfigure(apiKey, logLevel, customerUserId, wrapperVersion);
        }

        public static void SendEvent(string eventType, string eventName, string parametersJson)
        {
            AppstackUnitySendEvent(eventType, eventName, parametersJson);
        }

        public static void EnableAppleAdsAttribution()
        {
            AppstackUnityEnableAppleAdsAttribution();
        }

        public static string GetAppstackId()
        {
            return PtrToUtf8StringAndFree(AppstackUnityGetAppstackId());
        }

        public static bool IsSdkDisabled()
        {
            return AppstackUnityIsSdkDisabled() != 0;
        }

        public static void GetAttributionParams(
            Action<Dictionary<string, object>> onSuccess,
            Action<string> onError)
        {
            var requestId = Requests.Register(
                onSuccess,
                onError,
                SynchronizationContext.Current);

            try
            {
                AppstackUnityGetAttributionParams(requestId, NativeCallback);
            }
            catch (Exception exception)
            {
                CompleteRequest(requestId, null, exception.Message);
            }
        }

        [AOT.MonoPInvokeCallback(typeof(AttributionParamsCallback))]
        private static void OnAttributionParamsReceived(
            int requestId,
            IntPtr jsonPointer,
            IntPtr errorPointer)
        {
            // Copy and release both buffers before consulting the callback registry so a
            // stale or duplicate native callback cannot leak native memory.
            var json = PtrToUtf8StringAndFree(jsonPointer);
            var error = PtrToUtf8StringAndFree(errorPointer);
            CompleteRequest(requestId, json, error);
        }

        private static void CompleteRequest(int requestId, string json, string error)
        {
            Requests.TryComplete(
                requestId,
                () => AppstackJson.ParseObject(json),
                error);
        }

        private static string PtrToUtf8StringAndFree(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var length = 0;
                while (Marshal.ReadByte(pointer, length) != 0)
                {
                    length++;
                }

                if (length == 0)
                {
                    return string.Empty;
                }

                var bytes = new byte[length];
                Marshal.Copy(pointer, bytes, 0, length);
                return Encoding.UTF8.GetString(bytes);
            }
            finally
            {
                AppstackUnityFreeCString(pointer);
            }
        }
    }
}
#endif
