# Appstack Unity SDK – Usage Guide

This guide matches the patterns used in the [Flutter](https://github.com/appstack-tech/appstack-flutter-sdk) and [React Native](https://github.com/appstack-tech/react-native-appstack-sdk) SDKs.

## SDK initialization

Call `Configure` once at startup (e.g. in a bootstrap scene or main menu).

```csharp
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

## Editor and unsupported platforms

In the Unity Editor and on non‑iOS/Android platforms, SDK methods are no-ops or return safe defaults (e.g. `GetAppstackId()` returns `null`, `IsSdkDisabled()` returns `true`). You can keep the same code paths; they will not call native code.

## Event type reference

| EventType         | Description                    |
|-------------------|--------------------------------|
| INSTALL           | App install (SDK may track)    |
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
