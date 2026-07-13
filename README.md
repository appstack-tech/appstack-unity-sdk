# Appstack Unity SDK

Track events and revenue, enable Apple Ads attribution on iOS, and retrieve
attribution data from Unity applications.

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://opensource.org/licenses/MIT)

## Requirements

- Unity 6 (`6000.0`) or newer
- iOS 15.0 or newer
- Android API level 21 or newer, target API level 34+, and Java 17+
- External Dependency Manager for Unity (EDM4U) recommended for automatic
  Android dependency resolution

## Installation

### OpenUPM

1. Add `https://package.openupm.com` as a scoped registry for `com.appstack`.
2. In Unity, open **Window → Package Manager**.
3. Select **+ → Add package by name** and enter `com.appstack.unity-sdk`.

See the [OpenUPM getting-started guide](https://openupm.com/docs/getting-started.html)
for scoped-registry instructions.

### Local package

Clone this repository, then select **Window → Package Manager → + → Add package
from disk** and choose the repository's root `package.json`.

You can also add a local dependency to your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.appstack.unity-sdk": "file:../appstack-unity-sdk"
  }
}
```

## Platform setup

iOS dependency setup is automatic. EDM4U is recommended for Android, but
Android projects can use the documented manual Gradle setup instead.

- [iOS setup](Documentation~/iOS.md)
- [Android setup](Documentation~/Android.md)

## Quick start

Call `Configure` once during application startup and before using other SDK
methods.

```csharp
using System.Collections.Generic;
using Appstack;
using UnityEngine;

public sealed class AppstackInitializer : MonoBehaviour
{
    [SerializeField] private string iosApiKey;
    [SerializeField] private string androidApiKey;

    private void Start()
    {
#if UNITY_IOS && !UNITY_EDITOR
        string apiKey = iosApiKey;
#elif UNITY_ANDROID && !UNITY_EDITOR
        string apiKey = androidApiKey;
#else
        string apiKey = "your-api-key";
#endif

        AppstackSDK.Configure(apiKey);

#if UNITY_IOS && !UNITY_EDITOR
        AppstackSDK.EnableAppleAdsAttribution();
#endif

        AppstackSDK.SendEvent(
            EventType.PURCHASE,
            parameters: new Dictionary<string, object>
            {
                { "revenue", 29.99 },
                { "currency", "USD" }
            });
    }
}
```

## Public API

### Configure

```csharp
AppstackSDK.Configure(
    apiKey: "your-platform-api-key",
    logLevel: 1,
    customerUserId: "optional-user-id"
);
```

`logLevel` accepts `0=DEBUG`, `1=INFO`, `2=WARN`, and `3=ERROR`. iOS has no
dedicated warning level, so `WARN` behaves like `ERROR` there.

### Send standard and custom events

```csharp
AppstackSDK.SendEvent(EventType.LOGIN);

AppstackSDK.SendEvent(
    EventType.CUSTOM,
    eventName: "level_completed",
    parameters: new Dictionary<string, object> { { "level", 12 } }
);
```

An `eventName` is required for `CUSTOM` events and ignored for standard events.
Event parameters may contain strings, Booleans, finite numeric values, nulls,
nested string-keyed dictionaries, and arrays. Unsupported objects and non-finite
numbers such as `NaN` or infinity are rejected before reaching the native SDK.

### Retrieve the Appstack ID and attribution parameters

```csharp
string appstackId = AppstackSDK.GetAppstackId();

AppstackSDK.GetAttributionParams(
    onSuccess: parameters => Debug.Log($"Attribution: {parameters.Count} values"),
    onError: error => Debug.LogError($"Attribution error: {error}")
);
```

### Check SDK status

```csharp
bool disabled = AppstackSDK.IsSdkDisabled();
```

## More documentation

- [Usage guide](USAGE.md)
- [iOS setup](Documentation~/iOS.md)
- [Android setup](Documentation~/Android.md)
- Import the **Basic Integration** sample from the Unity Package Manager

For package architecture and contribution guidance, see
[DEVELOPMENT.md](DEVELOPMENT.md). Release maintainers should use
[RELEASING.md](RELEASING.md).

## License

MIT — see [LICENSE.md](LICENSE.md).
