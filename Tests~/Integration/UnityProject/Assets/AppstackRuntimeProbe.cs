using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public sealed class AppstackRuntimeProbe : MonoBehaviour
{
    private const int ExpectedCallbacks = 3;
    private const string ResultPrefix = "APPSTACK_RUNTIME_RESULT:";

    // Android's logcat truncates a single Unity log line at roughly one kilobyte,
    // so link cases are reported one per line rather than in the result payload.
    private const string LinkPrefix = "APPSTACK_RUNTIME_LINK:";

    // Links the operating system itself delivered, as opposed to the parser cases
    // handed to the SDK directly.
    private const string DeliveredLinkPrefix = "APPSTACK_RUNTIME_DEEPLINK:";

    private readonly object gate = new object();
    private readonly List<int> callbackThreads = new List<int>();
    private readonly List<string> callbackErrors = new List<string>();
    private int callbackCount;
    private int successCount;
    private bool attributionValidated = true;
    private int mainThreadId;

    private void Awake()
    {
        Application.deepLinkActivated += OnDeepLinkActivated;
    }

    private void OnDestroy()
    {
        Application.deepLinkActivated -= OnDeepLinkActivated;
    }

    /// A cold start caused by a tapped link arrives here rather than through the
    /// event, which only fires while the player is already running.
    private void ReportColdStartLink()
    {
        if (!string.IsNullOrEmpty(Application.absoluteURL))
            ReportDeliveredLink("absoluteURL", Application.absoluteURL);
    }

    private void OnDeepLinkActivated(string url)
    {
        ReportDeliveredLink("deepLinkActivated", url);
    }

    private static void ReportDeliveredLink(string source, string url)
    {
        string host;
        try
        {
            host = new Uri(url).Host;
        }
        catch (Exception)
        {
            host = null;
        }

        // Filtering on the delivered link's own host proves the allow list accepts
        // the genuine host rather than only that parsing succeeded.
        var result = RunLinkCase(source, url, host == null ? null : new[] { host });
        Debug.Log(DeliveredLinkPrefix + JsonUtility.ToJson(result));
    }

    private IEnumerator Start()
    {
        mainThreadId = Thread.CurrentThread.ManagedThreadId;

        ReportColdStartLink();

        // The native link parser performs no network request and needs no SDK
        // state, so it is exercised before any attribution traffic.
        foreach (var linkCase in RunUniversalLinkCases())
            Debug.Log(LinkPrefix + JsonUtility.ToJson(linkCase));

        // Auto-initialization runs before this scene. The application-owned user ID
        // remains a separate login-time concern.
        Appstack.AppstackSDK.SetCustomerUserId("runtime-validation-user");

        var appstackId = Appstack.AppstackSDK.GetAppstackId();

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

    /// Exercises the pinned native parser through the production bridge. Every case
    /// must behave identically on both platforms, so the host-side validator asserts
    /// one shared expectation table against whichever player produced this result.
    private static LinkCaseResult[] RunUniversalLinkCases()
    {
        var allowed = new[] { "links.example.com" };
        return new[]
        {
            RunLinkCase(
                "standard",
                "https://links.example.com/abc123" +
                "?deep_link_path=%2Fshop%2F42&utf8=caf%C3%A9%20%F0%9F%9A%80&flag&empty=",
                allowed),
            RunLinkCase("anyHostAllowed", "https://links.example.com/xyz789?a=1", null),
            RunLinkCase("hostCaseInsensitive", "https://LINKS.EXAMPLE.COM/abc123", allowed),
            RunLinkCase("encodedIdDecoded", "https://links.example.com/abc%20123", allowed),
            RunLinkCase("repeatedParamLastWins", "https://links.example.com/abc123?k=1&k=2", allowed),
            RunLinkCase("hostNotAllowed", "https://other.example.com/abc123", allowed),
            RunLinkCase("sharedHostRejected", "https://appstack.link/abc123", null),
            RunLinkCase("devSharedHostRejected", "https://dev.appstack.link/abc123", null),
            RunLinkCase("twoPathSegments", "https://links.example.com/a/b", allowed),
            RunLinkCase("noPathSegment", "https://links.example.com/", allowed),
            RunLinkCase("plainHttpRejected", "http://links.example.com/abc123", allowed),
            RunLinkCase("emptyAllowedHostsRejects", "https://links.example.com/abc123", new string[0]),
        };
    }

    private static LinkCaseResult RunLinkCase(string name, string url, string[] allowedHosts)
    {
        try
        {
            var link = Appstack.AppstackSDK.HandleUniversalLink(url, allowedHosts);
            if (link == null) return new LinkCaseResult { name = name, matched = false };

            var pairs = new List<string>();
            if (link.QueryParams != null)
            {
                foreach (var pair in link.QueryParams)
                    pairs.Add(pair.Key + "=" + pair.Value);
            }

            pairs.Sort(StringComparer.Ordinal);
            return new LinkCaseResult
            {
                name = name,
                matched = true,
                deeplinkId = link.DeeplinkId,
                url = link.Url,
                queryParams = pairs.ToArray(),
            };
        }
        catch (Exception exception)
        {
            return new LinkCaseResult
            {
                name = name,
                error = exception.GetType().Name + ": " + exception.Message,
            };
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

    [Serializable]
    private sealed class LinkCaseResult
    {
        public string name;
        public bool matched;
        public string deeplinkId;
        public string url;

        /// Query parameters flattened as "key=value" pairs ordered by key, because
        /// JsonUtility cannot serialize a dictionary.
        public string[] queryParams;
        public string error;
    }
}
