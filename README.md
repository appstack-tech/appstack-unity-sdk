# Appstack Unity SDK

Track events and revenue with Apple Search Ads attribution in your Unity app. Same API strategy as the [Flutter](https://github.com/appstack-tech/appstack-flutter-sdk) and [React Native](https://github.com/appstack-tech/react-native-appstack-sdk) SDKs.

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://opensource.org/licenses/MIT)

## Overview

The Appstack Unity SDK lets you:

- Track standardized and custom events
- Track revenue events with currency (for ROAS / optimization)
- Enable Apple Ads attribution on iOS
- Retrieve the Appstack installation ID and attribution parameters

## Requirements

- **Unity:** Unity 6 (`6000.0`) or newer
- **iOS:** 15.0+ (required by the Appstack iOS SDK)
- **Android:** minSdk 21, targetSdk 34+, Java 17+

## Installation

### Option A: Unity Package Manager (OpenUPM)

1. Add the **OpenUPM** scoped registry: [OpenUPM – Getting started](https://openupm.com/docs/getting-started.html) (add `https://package.openupm.com` as a scoped registry for `com.appstack`).
2. In Unity: **Window → Package Manager → + → Add package by name** → `com.appstack.unity-sdk`.
3. Complete **iOS** and **Android** setup below.

### Option B: Local package

1. Clone this repository and add it from Unity Package Manager with **Add package from disk**, selecting its root `package.json`, or use a `file:` dependency in the consuming project's manifest.
2. Complete **iOS** and **Android** setup below.

### iOS setup

Resolve the Appstack iOS Swift package with EDM4U and configure iOS.
See [Documentation~/iOS.md](Documentation~/iOS.md).

### Android setup

Add the Appstack Android SDK dependency (EDM4U or manual Gradle).  
See [Documentation~/Android.md](Documentation~/Android.md).

**Registering on OpenUPM (one-time)**  
To have the package appear in the OpenUPM registry, submit it once via the [OpenUPM add form](https://openupm.com/packages/add/) or by opening a PR to [openupm/openupm](https://github.com/openupm/openupm) with the metadata from [`.openupm/package-metadata.yml`](.openupm/package-metadata.yml). After that, new Git tags will be built and published automatically.

## Quick Start

```csharp
using Appstack;
using UnityEngine;

public class AppstackInitializer : MonoBehaviour
{
    async void Start()
    {
        string apiKey = Application.platform == RuntimePlatform.IPhonePlayer
            ? "your-ios-api-key"
            : "your-android-api-key";

        AppstackSDK.Configure(apiKey);

#if UNITY_IOS && !UNITY_EDITOR
        AppstackSDK.EnableAppleAdsAttribution();
#endif

        // Send events
        AppstackSDK.SendEvent(EventType.SIGN_UP);
        AppstackSDK.SendEvent(EventType.PURCHASE, parameters: new Dictionary<string, object>
        {
            { "revenue", 29.99 },
            { "currency", "USD" }
        });
    }
}
```

## API (aligned with Flutter / React Native)

### `AppstackSDK.Configure(apiKey, logLevel?, customerUserId?)`

Initializes the SDK. Must be called before any other methods.

- **apiKey** – Your platform-specific API key from the Appstack dashboard.
- **logLevel** – 0=DEBUG, 1=INFO, 2=WARN, 3=ERROR; default `1`. (iOS has no dedicated WARN tier, so 2 behaves like 3 there.)
- **customerUserId** – Optional customer user ID.

### `AppstackSDK.SendEvent(eventType, eventName?, parameters?)`

Sends an event.

- **eventType** – Use the `EventType` enum (e.g. `EventType.PURCHASE`, `EventType.CUSTOM`).
- **eventName** – Required when `eventType == EventType.CUSTOM`; ignored otherwise.
- **parameters** – Optional `Dictionary<string, object>` (e.g. `revenue`, `currency`).

### `AppstackSDK.EnableAppleAdsAttribution()`

Enables Apple Search Ads attribution (iOS only). No-op on Android.

### `AppstackSDK.GetAppstackId()`

Returns the Appstack installation ID string.

### `AppstackSDK.IsSdkDisabled()`

Returns `true` if the SDK is disabled (e.g. invalid API key).

### `AppstackSDK.GetAttributionParams(onSuccess, onError?)`

Retrieves attribution parameters asynchronously.  
`onSuccess(Dictionary<string, object>)` and optional `onError(string)`.

### Event types

Same set as Flutter/React Native:  
`INSTALL`, `LOGIN`, `SIGN_UP`, `REGISTER`, `PURCHASE`, `ADD_TO_CART`, `ADD_TO_WISHLIST`, `INITIATE_CHECKOUT`, `START_TRIAL`, `SUBSCRIBE`, `LEVEL_START`, `LEVEL_COMPLETE`, `TUTORIAL_COMPLETE`, `SEARCH`, `VIEW_ITEM`, `VIEW_CONTENT`, `SHARE`, `CUSTOM`.

## Documentation

- [Usage guide](USAGE.md)
- [iOS setup](Documentation~/iOS.md)
- [Android setup](Documentation~/Android.md)

## Releasing (CD)

Releases use **GitHub Actions** and **OpenUPM** (UPM).

1. **Update version:** Set `version` in `Assets/AppstackSDK/package.json` and update `CHANGELOG.md`.
2. **Tag and push:** e.g. `git tag 1.0.0 && git push origin 1.0.0`
3. **Workflow** [`.github/workflows/release.yml`](.github/workflows/release.yml):
   - Verifies `package.json` version matches the tag
   - Creates a **GitHub Release** with a zip for manual install
   - **OpenUPM** builds from the same tag once the package is [registered on OpenUPM](https://openupm.com/packages/add/) (one-time: submit the repo via their form or a PR to [openupm/openupm](https://github.com/openupm/openupm) with the package metadata; see [`.openupm/package-metadata.yml`](.openupm/package-metadata.yml)).

## License

MIT – see [LICENSE.md](LICENSE.md).
