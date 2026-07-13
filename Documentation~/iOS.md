# iOS setup

## Requirements

- Unity 6 (`6000.0`) or newer
- iOS 15.0 or newer

## Configure the project

1. Open **Edit → Project Settings → Player → iOS**.
2. Set **Target minimum iOS Version** to `15.0` or newer.
3. Build the iOS player normally.

Appstack resolves its iOS dependency automatically during the Unity build. No
manual Xcode framework or Apple system-framework configuration is required.

## Enable Apple Ads attribution

Call this after configuring the SDK:

```csharp
#if UNITY_IOS && !UNITY_EDITOR
AppstackSDK.Configure("your-ios-api-key");
AppstackSDK.EnableAppleAdsAttribution();
#endif
```

Apple Ads attribution must be tested with an App Store or TestFlight
installation; simulator and ordinary development installs do not represent the
production attribution flow.

## Troubleshooting

If Xcode reports that `AppstackSDK` cannot be found:

1. Delete the generated Xcode project and export it again from Unity.
2. Check the Unity Console for postprocessing errors.
3. Confirm the generated Xcode project lists the `AppstackSDK` package product
   on both the `UnityFramework` and application targets.
4. Confirm the build machine can reach GitHub to resolve Swift packages.
