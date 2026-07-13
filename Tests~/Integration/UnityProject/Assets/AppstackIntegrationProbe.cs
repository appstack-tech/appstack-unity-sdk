using System.Collections.Generic;
using UnityEngine;

public sealed class AppstackIntegrationProbe : MonoBehaviour
{
    private void Start()
    {
#if !UNITY_EDITOR
        Appstack.AppstackSDK.Configure(
            "phase-d-build-only-key",
            logLevel: 0,
            customerUserId: "phase-d-user");

        Appstack.AppstackSDK.SendEvent(
            Appstack.EventType.CUSTOM,
            eventName: "phase_d_build",
            parameters: new Dictionary<string, object>
            {
                { "number", 42 },
                { "unicode", "café 🚀" },
                {
                    "nested",
                    new Dictionary<string, object>
                    {
                        { "enabled", true },
                        { "items", new object[] { "one", 2, null } }
                    }
                }
            });

        _ = Appstack.AppstackSDK.GetAppstackId();
        _ = Appstack.AppstackSDK.IsSdkDisabled();
        Appstack.AppstackSDK.EnableAppleAdsAttribution();
        Appstack.AppstackSDK.GetAttributionParams(
            _ => { },
            _ => { });
#endif
    }
}
