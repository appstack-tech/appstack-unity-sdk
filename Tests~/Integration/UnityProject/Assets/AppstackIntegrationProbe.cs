using System.Collections.Generic;
using UnityEngine;

public sealed class AppstackIntegrationProbe : MonoBehaviour
{
    private void Start()
    {
#if !UNITY_EDITOR
        // The generated fixture settings initialize Appstack before this scene.
        Appstack.AppstackSDK.SetCustomerUserId("player-validation-user");

        Appstack.AppstackSDK.SendEvent(
            Appstack.EventType.CUSTOM,
            eventName: "player_validation_build",
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
        Appstack.AppstackSDK.GetAttributionParams(
            _ => { },
            _ => { });
#endif
    }
}
