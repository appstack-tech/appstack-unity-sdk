# iOS Setup for Appstack Unity SDK

## 1. Add the Appstack iOS SDK (XCFramework)

Unity’s standard iOS build **does not support Swift Package Manager (SPM)**. The generated Xcode project does not resolve SPM dependencies, so the iOS SDK must be integrated as a vendored **XCFramework** (or static/framework binary) in `Plugins/iOS/`. This matches how other Unity iOS plugins ship native SDKs.

The Unity plugin expects the **AppstackSDK.xcframework** to be present so the native bridge can call the iOS SDK.

**Required version: 4.4.0-rc0 or newer.** The bridge uses the current `configure(apiKey:logLevel:customerUserId:wrapperVersion:)` wrapper entry point (`@_spi(AppstackInternal)`); older xcframeworks (with the removed `isDebug`/`endpointBaseUrl` options) will not compile.

- **Option A:** Build the xcframework from [`appstack-ios-sdk`](https://github.com/appstack-tech/appstack-ios-sdk) at tag `4.4.0-rc0` and copy it into this folder.
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

Set your Unity iOS build minimum version (Player Settings → Target minimum iOS Version) to **iOS 15.0** or higher. The Appstack iOS SDK declares iOS 15 as its minimum platform; builds targeting lower versions will fail to compile the Swift bridge.
