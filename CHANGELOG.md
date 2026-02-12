# Changelog

All notable changes to the Appstack Unity SDK will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

### Added

- Initial Unity SDK aligned with Flutter and React Native strategy:
  - C# API: `AppstackSDK` (Configure, SendEvent, EnableAppleAdsAttribution, GetAppstackId, IsSdkDisabled, GetAttributionParams)
  - `EventType` enum matching other SDKs
  - iOS native plugin (Swift bridge + ObjC C exports) using AppstackSDK.xcframework
  - Android native plugin (Kotlin bridge) using Appstack Android SDK via Gradle/EDM4U
  - README, USAGE, and platform-specific setup guides
