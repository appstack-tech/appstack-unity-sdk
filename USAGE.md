# Appstack Unity SDK – Usage Guide

This guide covers SDK initialization, event tracking, revenue recommendations,
and attribution data.

## SDK initialization

### Automatic initialization

Open **Edit → Project Settings → Appstack** and create the settings asset. The
default configuration enables auto-initialization for iOS and Android and runs
before the first scene:

- Automatic environment selection uses development keys for Unity Development
  Builds and production keys for other builds.
- Development and Production modes pin every build to that environment.
- **Allow Production Fallback** permits a development build with no development
  key to use its production key. It is off by default, and production never
  falls back to development.
- Disable a platform if the project intentionally does not ship Appstack on it.
- Apple Ads Attribution can be enabled as part of iOS auto-initialization.

Only the current target is validated. For example, an iOS build does not
require Android keys. An enabled target with no resolvable key fails its build
with a configuration error rather than shipping with Appstack unexpectedly
disabled.

No settings asset means manual mode; merely installing the package does not
initialize the SDK.

The password fields mask API keys visually only. Values remain plaintext in the
settings asset and version control. The complete Resources asset is included in
players, so development and other-platform keys may also be present in a
production build. Use application ingestion credentials rather than
administrative secrets.

### Manual initialization

Call `Configure` once at startup (e.g. in a bootstrap scene or main menu) when
auto-initialization is not appropriate. Manual mode is recommended when user
consent or another application bootstrap step must happen first.

```csharp
using System.Collections.Generic;
using Appstack;
using UnityEngine;

#if UNITY_IOS && !UNITY_EDITOR
    string apiKey = "your-ios-api-key";
#elif UNITY_ANDROID && !UNITY_EDITOR
    string apiKey = "your-android-api-key";
#else
    string apiKey = "your-api-key"; // Editor or fallback
#endif

AppstackSDK.Configure(apiKey);

// Optional: check status
bool disabled = AppstackSDK.IsSdkDisabled();
if (disabled)
    Debug.LogWarning("Appstack SDK is disabled – check your API key.");
```

With all options:

```csharp
AppstackSDK.Configure(
    apiKey: "your-api-key",
    logLevel: 1,           // 0=DEBUG, 1=INFO, 2=WARN, 3=ERROR (iOS folds WARN into ERROR)
    customerUserId: "user-123"
);
```

The first successful automatic or manual configuration wins. An identical
repeat is ignored silently; a conflicting repeat logs a warning and is ignored.
A failed attempt does not lock the wrapper and can be retried.

## Customer user ID

When the ID is only known after `Configure` — usually after a login — set it
then. A repeat `Configure` is a no-op and ignores its `customerUserId`, so it
cannot be used to change the ID.

```csharp
AppstackSDK.SetCustomerUserId("user-123"); // on login
AppstackSDK.ClearCustomerUserId();         // on logout
```

`SetCustomerUserId(null)`, `""`, and whitespace all clear the ID too;
`ClearCustomerUserId()` is just the explicit spelling. (On `Configure`, an empty
`customerUserId` means "not provided" instead — `Configure` never clears.)

Safe to call at any time, from any thread, as often as you like — last write
wins. Clearing on logout matters: otherwise the previous user's ID stays
attached to every later event. The call sends nothing by itself, so make sure at
least one event follows.

## Event tracking

### Standard events

Use the `EventType` enum (same names as Flutter/React Native).

```csharp
AppstackSDK.SendEvent(EventType.SIGN_UP);
AppstackSDK.SendEvent(EventType.LOGIN);
AppstackSDK.SendEvent(EventType.ADD_TO_CART);
AppstackSDK.SendEvent(EventType.PURCHASE, parameters: new Dictionary<string, object>
{
    { "revenue", 29.99 },
    { "currency", "USD" }
});
```

### Custom events

Use `EventType.CUSTOM` and pass an `eventName`:

```csharp
AppstackSDK.SendEvent(
    EventType.CUSTOM,
    eventName: "level_completed",
    parameters: new Dictionary<string, object> { { "level", 12 } }
);
```

Event parameters may contain strings, Booleans, finite numeric values, nulls,
nested string-keyed dictionaries, and arrays. Unsupported objects and non-finite
numbers such as `NaN` or infinity throw an `ArgumentException` before the event is
sent.

### Revenue (EAC recommendations)

For any revenue event, send `revenue` (or `price`) and `currency`:

```csharp
AppstackSDK.SendEvent(EventType.PURCHASE, parameters: new Dictionary<string, object>
{
    { "revenue", 4.99 },
    { "currency", "EUR" }
});
```

## Apple Search Ads attribution (iOS)

Call after `Configure` on iOS builds only:

```csharp
#if UNITY_IOS && !UNITY_EDITOR
    AppstackSDK.EnableAppleAdsAttribution();
#endif
```

Requires iOS 15.0+ and the iOS setup described in [Documentation~/iOS.md](Documentation~/iOS.md).

## Appstack ID and attribution parameters

```csharp
string appstackId = AppstackSDK.GetAppstackId();

AppstackSDK.GetAttributionParams(
    onSuccess: params => {
        foreach (var kv in params)
            Debug.Log($"Attribution: {kv.Key} = {kv.Value}");
    },
    onError: error => Debug.LogError($"Attribution error: {error}")
);
```

Callbacks are delivered on the synchronization context captured when
`GetAttributionParams` is called, when one is available. Calling it from Unity's
main thread allows the callbacks to safely update Unity objects.

## Editor and unsupported platforms

In the Unity Editor and on non-iOS/Android platforms, SDK methods are no-ops or
return safe defaults. For example, `GetAppstackId()` returns `null` and
`IsSdkDisabled()` returns `true`. These platforms do not call native code.

## Event type reference

| EventType         | Description                    |
|-------------------|--------------------------------|
| INSTALL           | Automatic only; manual sends ignored |
| LOGIN             | User login                     |
| SIGN_UP / REGISTER| Registration                   |
| PURCHASE          | Purchase (use revenue/currency)|
| ADD_TO_CART       | Add to cart                    |
| ADD_TO_WISHLIST   | Add to wishlist                |
| INITIATE_CHECKOUT | Checkout started              |
| START_TRIAL       | Trial started                  |
| SUBSCRIBE         | Subscription                   |
| LEVEL_START       | Level started (games)          |
| LEVEL_COMPLETE    | Level completed                |
| TUTORIAL_COMPLETE  | Onboarding completed          |
| SEARCH            | Search                         |
| VIEW_ITEM         | View item                      |
| VIEW_CONTENT      | View content                   |
| SHARE             | Share                          |
| CUSTOM            | Custom (requires eventName)     |
