# Appstack Unity SDK

Track events and revenue, enable Apple Ads attribution on iOS, and retrieve
attribution data from Unity applications.

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://opensource.org/licenses/MIT)

## Requirements

- Unity 6 (`6000.0`) or newer
- iOS 15.0 or newer
- Android API level 21 or newer, target API level 34+, and Java 17+
- For Android builds, either External Dependency Manager for Unity (EDM4U) or
  the documented manual Gradle dependency configuration

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

iOS dependency setup is automatic. Before building for Android, the Appstack
native Android SDK must be added to the generated Gradle project. Install EDM4U
for automatic resolution (recommended), or follow the manual Gradle setup. The
Appstack Unity package does not install EDM4U automatically.

- [iOS setup](Documentation~/iOS.md)
- [Android setup](Documentation~/Android.md)

## Quick start

### Automatic initialization

Open **Edit → Project Settings → Appstack**, select **Create Appstack
Settings**, and enter the development and production API keys for each platform
you ship. Appstack initializes before the first scene without requiring a
GameObject or startup script.

By default, Unity Development Builds use the development key and other builds
use the production key. The environment can instead be pinned to Development or
Production. **Allow Production Fallback** lets a development build use its
production key when no development key is configured; this is explicit and
disabled by default. Production builds never fall back to a development key.

iOS and Android can be enabled independently. A build is blocked only when
auto-initialization and its current target platform are enabled but no key can
be resolved for that build. Missing keys for another platform do not affect the
build.

Creating settings opts the project into auto-initialization. Installing the
package alone creates no settings and changes no runtime behavior.

### Manual initialization

For consent flows, custom bootstrap ordering, or remotely supplied
configuration, leave the settings asset absent or turn off **Auto Initialize**.
Call `Configure` once during application startup and before using other SDK
methods:

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

The first successful automatic or manual configuration wins. Repeating the
same configuration is a silent no-op; a conflicting repeat is ignored with a
warning that does not expose either API key. Failed configuration attempts may
be retried.

The password fields mask API keys visually only. Keys remain plaintext in the
settings asset and version control. Because Unity includes the entire Resources
asset, every configured key—including development keys and keys for the other
mobile platform—may be present in production player builds. Treat them as
application ingestion credentials, not administrative secrets.

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

### Set or clear the customer user ID

The customer user ID is your own identifier for the signed-in user. Appstack
attaches it to events so server-to-server events — which identify the user by
this ID rather than by the install — can be joined back to the install that
produced them.

Pass it to `Configure` when you already know it at startup. More often a login
reveals it afterwards, so set it whenever it becomes known:

```csharp
AppstackSDK.SetCustomerUserId("user-123"); // on login
AppstackSDK.ClearCustomerUserId();         // on logout
```

`SetCustomerUserId(null)` and an empty or whitespace ID also clear it, so
`ClearCustomerUserId()` is only a more explicit spelling of the same call. Note
this differs from `Configure`, where an empty `customerUserId` means "not
provided": `Configure` never clears.

Callable at any time, before or after `Configure`, as often as you like — the
last call wins. It applies to every event sent from here on, including ones the
native SDK has buffered but not yet flushed, and does not send anything by
itself: make sure at least one event follows, or no mapping is ever formed.
Calling `Configure` again to change the ID does not work — a repeat `Configure`
is a no-op and its `customerUserId` is ignored.

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
