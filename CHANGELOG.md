# Changelog

All notable changes to the Appstack Unity SDK will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

### Changed

- Restructured the repository as a root-level UPM package using
  `Runtime/`, `Editor/`, `Tests/`, `Samples~/`, and `Documentation~/`.
- Set the initial package floor to Unity 6 (`6000.0`) and retained the
  existing package identifier `com.appstack.unity-sdk`.
- Replaced the prototype assembly names with `Appstack.Unity`,
  `Appstack.Unity.Editor`, and `Appstack.Unity.Tests`.
- Standardized the native wrapper identifier as `unity-1.0.0`.
- Added the iOS `AppstackSDK` Swift package dependency at `4.4.0-rc0`.
- Reworked Android and iOS attribution callbacks around request IDs and the
  caller's captured synchronization context, while preserving the existing
  public `AppstackSDK` API.
- Aligned with the current native SDK APIs (iOS 4.4.0-rc0, Android 1.5.0-rc0):
  - `Configure(apiKey, logLevel?, customerUserId?)` — removed the `isDebug` and
    `endpointBaseUrl` parameters, which are deprecated no-ops in the native SDKs.
    Endpoint override is now an internal-only hook: the bridges apply
    `setProxyUrl` when the host app ships an `APPSTACK_DEV_PROXY_URL`
    Info.plist key (iOS) / manifest `<meta-data>` entry (Android).
  - Bridges now use the internal wrapper entry points
    (`configureWrapper` on Android, `@_spi(AppstackInternal)`
    `configure(..., wrapperVersion:)` on iOS) and report `unity-1.0.0` as the
    wrapper version.
  - Fixed the iOS log-level mapping to the cross-platform contract
    (0=DEBUG, 1=INFO, 2=WARN, 3=ERROR; WARN folds into ERROR on iOS).
  - Minimum iOS version is now 15.0 (required by the Appstack iOS SDK).
  - Android dependency bumped from 1.3.1 to 1.5.0-rc0 (EDM4U and manual Gradle docs).

### Added

- Added an Android post-generation hook that automatically merges the Unity
  bridge keep rules into the generated `unityLibrary/proguard-unity.txt` file.
- Initial Unity SDK aligned with Flutter and React Native strategy:
  - C# API: `AppstackSDK` (Configure, SendEvent, EnableAppleAdsAttribution, GetAppstackId, IsSdkDisabled, GetAttributionParams)
  - `EventType` enum matching other SDKs
  - iOS native plugin (Swift bridge + ObjC C exports) using AppstackSDK.xcframework
  - Android native plugin (Kotlin bridge) using Appstack Android SDK via Gradle/EDM4U
  - README, USAGE, and platform-specific setup guides
