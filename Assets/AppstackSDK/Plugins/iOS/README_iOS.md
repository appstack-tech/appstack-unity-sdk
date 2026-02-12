# iOS Setup for Appstack Unity SDK

## 1. Add the Appstack iOS SDK (XCFramework)

Unity’s standard iOS build **does not support Swift Package Manager (SPM)**. The generated Xcode project does not resolve SPM dependencies, so the iOS SDK must be integrated as a vendored **XCFramework** (or static/framework binary) in `Plugins/iOS/`. This matches how other Unity iOS plugins ship native SDKs.

The Unity plugin expects the **AppstackSDK.xcframework** to be present so the native bridge can call the iOS SDK.

- **Option A:** Copy the xcframework from one of the sibling SDKs into this folder:
  - From `react-native-appstack-sdk/ios/AppstackSDK.xcframework`
  - Or from `appstack-flutter-sdk/appstack_plugin/ios/AppstackSDK.xcframework`
  - Or from `ios-appstack-sdk/AppstackSDK.xcframework`
- **Option B:** Download the xcframework from your Appstack distribution channel if provided.

Place it so the path is:
`Assets/AppstackSDK/Plugins/iOS/AppstackSDK.xcframework`

## 2. Swift header import

The ObjC bridge (`AppstackUnityBridge.mm`) imports the Swift-generated header using one of:

- `UnityFramework-Swift.h`
- `Unity-iPhone-Swift.h`
- `Unity-Swift.h`

If your Unity iOS build uses a different product/target name and the build fails with "Swift header not found", either:

- Rename your target to one of the above, or  
- Add a preprocessor define / custom build step to point to your `ProductName-Swift.h`.

## 3. Info.plist (Apple Search Ads)

For Apple Search Ads attribution, add to your app’s `Info.plist`:

```xml
<key>NSAdvertisingAttributionReportEndpoint</key>
<string>https://ios-appstack.com/</string>
```

## 4. Minimum iOS version

Set your Unity iOS build minimum version to **iOS 13.0** or higher (14.3+ recommended for Apple Search Ads).
