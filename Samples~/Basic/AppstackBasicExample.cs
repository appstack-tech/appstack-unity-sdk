using System.Collections.Generic;
using Appstack;
using UnityEngine;

public sealed class AppstackBasicExample : MonoBehaviour
{
    [SerializeField] private string apiKey;

    private void Start()
    {
        AppstackSDK.Configure(apiKey);
        AppstackSDK.SendEvent(
            EventType.CUSTOM,
            "unity_sample_started",
            new Dictionary<string, object>
            {
                { "scene", gameObject.scene.name },
            });
    }
}
