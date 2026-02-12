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

- **Unity:** 2019.4 LTS or newer (tested with 2021.3+)
- **iOS:** 13.0+ (14.3+ recommended for Apple Search Ads)
- **Android:** minSdk 21, targetSdk 34+, Java 17+

## Installation

1. **Copy or clone** this package into your project, for example:
   - `Assets/AppstackSDK/` (full folder from this repo)

2. **iOS:** Add the Appstack iOS SDK (XCFramework) and configure Info.plist.  
   See [Assets/AppstackSDK/Plugins/iOS/README_iOS.md](Assets/AppstackSDK/Plugins/iOS/README_iOS.md).

3. **Android:** Add the Appstack Android SDK dependency (EDM4U or manual Gradle).  
   See [Assets/AppstackSDK/Plugins/Android/README_Android.md](Assets/AppstackSDK/Plugins/Android/README_Android.md).

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

### `AppstackSDK.Configure(apiKey, isDebug?, endpointBaseUrl?, logLevel?, customerUserId?)`

Initializes the SDK. Must be called before any other methods.

- **apiKey** – Your platform-specific API key from the Appstack dashboard.
- **isDebug** – Optional, default `false`.
- **endpointBaseUrl** – Optional custom endpoint.
- **logLevel** – 0=DEBUG, 1=INFO, 2=WARN, 3=ERROR; default `1`.
- **customerUserId** – Optional customer user ID.

### `AppstackSDK.SendEvent(eventType, eventName?, parameters?)`

Sends an event.

- **eventType** – Use the `EventType` enum (e.g. `EventType.PURCHASE`, `EventType.CUSTOM`).
- **eventName** – Required when `eventType == EventType.CUSTOM`; optional otherwise.
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
- [iOS setup](Assets/AppstackSDK/Plugins/iOS/README_iOS.md)
- [Android setup](Assets/AppstackSDK/Plugins/Android/README_Android.md)

## License

MIT – see [LICENSE](LICENSE).
