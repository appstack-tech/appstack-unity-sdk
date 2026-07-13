# iOS setup

## Requirements

- Unity 6 (`6000.0`) or newer
- iOS 15.0 or newer
- External Dependency Manager for Unity (EDM4U) 1.2.187 or newer

## Configure the project

1. Install EDM4U 1.2.187 or newer in your Unity project.
2. Open **Edit → Project Settings → Player → iOS**.
3. Set **Target minimum iOS Version** to `15.0` or newer.
4. Build the iOS player normally.

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

1. Confirm EDM4U is version 1.2.187 or newer.
2. Delete the generated Xcode project and export it again from Unity.
3. Check the Unity Console for dependency-resolution errors.
4. Confirm the build machine can reach GitHub to resolve Swift packages.
