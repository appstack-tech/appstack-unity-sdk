# Changelog

All notable changes to the Appstack Unity SDK are documented in this file.

The format is based on [Keep a
Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

### Added

- Initial Unity Package Manager distribution as `com.appstack.unity-sdk` for
  Unity 6 (`6000.0`) or newer.
- Support for iOS 15.0+ through Appstack iOS SDK `4.4.0` and Android API level
  21+ through Appstack Android SDK `1.5.0`.
- SDK configuration with an API key, log level, and optional customer user ID.
- Standard and custom event tracking with optional event parameters.
- Apple Ads attribution on iOS.
- Appstack ID, SDK status, and asynchronous attribution-parameter retrieval.
- Concurrent attribution requests with callbacks returned to the captured
  synchronization context when available.
- Automatic iOS Swift Package Manager integration through the Unity Xcode
  postprocessor, including dynamic-framework embedding in the application.
- Automatic Android native dependency resolution through EDM4U.
- Automatic Android R8/ProGuard configuration with no custom keep-rules step.
- Basic Integration sample for manual SDK configuration and event tracking.
