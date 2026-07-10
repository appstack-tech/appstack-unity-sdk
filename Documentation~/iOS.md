# iOS Setup for Appstack Unity SDK

## 1. Resolve the Appstack iOS SDK

Install EDM4U and let it process `Editor/AppstackDependencies.xml`. The package
declares the `AppstackSDK` Swift package product from `ios-appstack-sdk` at
version `4.4.0-rc0`.

This SPM integration is the supported path for the first Unity SDK release and
requires Unity 6. Older-Unity fallback distribution is not part of this release.

## 2. Swift bridge

The package uses direct C-ABI exports from `AppstackUnityBridge.swift`; it does
not depend on Unity's generated Swift header name.

## 3. Apple Ads attribution

Call `AppstackSDK.EnableAppleAdsAttribution()` after configuration. Appstack
uses the AdServices token flow and does not require
`NSAdvertisingAttributionReportEndpoint`.

## 4. Minimum iOS version

Set your Unity iOS build minimum version (Player Settings → Target minimum iOS Version) to **iOS 15.0** or higher. The Appstack iOS SDK declares iOS 15 as its minimum platform; builds targeting lower versions will fail to compile the Swift bridge.
