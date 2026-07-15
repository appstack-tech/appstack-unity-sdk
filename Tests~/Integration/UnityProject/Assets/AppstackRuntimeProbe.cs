using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public sealed class AppstackRuntimeProbe : MonoBehaviour
{
    private const int ExpectedCallbacks = 3;
    private const string ResultPrefix = "APPSTACK_RUNTIME_RESULT:";

    private readonly object gate = new object();
    private readonly List<int> callbackThreads = new List<int>();
    private readonly List<string> callbackErrors = new List<string>();
    private int callbackCount;
    private int successCount;
    private bool attributionValidated = true;
    private int mainThreadId;

    private IEnumerator Start()
    {
        mainThreadId = Thread.CurrentThread.ManagedThreadId;

        Appstack.AppstackSDK.Configure(
            "runtime-validation-local-key",
            logLevel: 0,
            customerUserId: "runtime-validation-user");

        var appstackId = Appstack.AppstackSDK.GetAppstackId();
        Appstack.AppstackSDK.EnableAppleAdsAttribution();

        for (var index = 0; index < ExpectedCallbacks; index++)
        {
            Appstack.AppstackSDK.GetAttributionParams(
                OnAttributionSuccess,
                OnAttributionError);
        }

        var deadline = Time.realtimeSinceStartup + 20f;
        while (GetCallbackCount() < ExpectedCallbacks &&
               Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        // Attribution callbacks complete only after native remote configuration.
        // Use that shared observable boundary before testing event delivery.
        Appstack.AppstackSDK.SendEvent(
            Appstack.EventType.CUSTOM,
            eventName: "runtime_validation_custom",
            parameters: new Dictionary<string, object>
            {
                { "number", 42 },
                { "unicode", "café 🚀" },
                {
                    "nested",
                    new Dictionary<string, object>
                    {
                        { "enabled", true },
                        { "items", new object[] { "one", 2, false } }
                    }
                }
            });

        Appstack.AppstackSDK.SendEvent(
            Appstack.EventType.LOGIN,
            parameters: new Dictionary<string, object>
            {
                { "state", "ready" },
                { "sequence", 2 }
            });

        // Native event delivery is fire-and-forget. Leave a short window for both
        // buffered and direct events to reach the recording backend.
        yield return new WaitForSecondsRealtime(3f);

        RuntimeResult result;
        lock (gate)
        {
            result = new RuntimeResult
            {
                platform = Application.platform.ToString(),
                unityVersion = Application.unityVersion,
                appstackIdPresent = !string.IsNullOrEmpty(appstackId),
                sdkDisabled = Appstack.AppstackSDK.IsSdkDisabled(),
                callbackCount = callbackCount,
                successCount = successCount,
                callbackThreads = callbackThreads.ToArray(),
                callbacksOnMainThread =
                    callbackThreads.Count == ExpectedCallbacks &&
                    callbackThreads.TrueForAll(id => id == mainThreadId),
                attributionValidated = attributionValidated,
                errors = callbackErrors.ToArray()
            };
        }

        Debug.Log(ResultPrefix + JsonUtility.ToJson(result));
    }

    private void OnAttributionSuccess(Dictionary<string, object> parameters)
    {
        var valid = parameters != null &&
                    parameters.TryGetValue("runtime_validation", out var state) &&
                    string.Equals(state as string, "attributed", StringComparison.Ordinal) &&
                    parameters.TryGetValue("unicode", out var unicode) &&
                    string.Equals(unicode as string, "café 🚀", StringComparison.Ordinal);

        lock (gate)
        {
            callbackCount++;
            successCount++;
            callbackThreads.Add(Thread.CurrentThread.ManagedThreadId);
            attributionValidated &= valid;
        }
    }

    private void OnAttributionError(string error)
    {
        lock (gate)
        {
            callbackCount++;
            callbackThreads.Add(Thread.CurrentThread.ManagedThreadId);
            callbackErrors.Add(error ?? string.Empty);
            attributionValidated = false;
        }
    }

    private int GetCallbackCount()
    {
        lock (gate)
        {
            return callbackCount;
        }
    }

    [Serializable]
    private sealed class RuntimeResult
    {
        public string platform;
        public string unityVersion;
        public bool appstackIdPresent;
        public bool sdkDisabled;
        public int callbackCount;
        public int successCount;
        public int[] callbackThreads;
        public bool callbacksOnMainThread;
        public bool attributionValidated;
        public string[] errors;
    }
}
