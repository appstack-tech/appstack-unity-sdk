# Changelog

All notable changes to the Appstack Unity SDK are documented in this file.

The format is based on [Keep a
Changelog](https://keepachangelog.com/en/1.0.0/).

## [1.2.0] - 2026-08-13

### Added

- Optional scene-independent auto-initialization configured through **Edit →
  Project Settings → Appstack**, with separate development and production keys
  for iOS and Android, per-platform enablement, automatic or pinned environment
  selection, explicit production-key fallback for development builds, and
  target-specific pre-build validation.
- Idempotent Unity-side configuration: the first successful automatic or
  manual configuration wins, identical repeats are silent, conflicting repeats
  warn without exposing credentials, and failed attempts remain retryable.

## [1.1.0] - 2026-08-12

### Added

- `SetCustomerUserId(customerUserId)` and `ClearCustomerUserId()` for setting or
  clearing the customer user ID after `Configure()`, bridging the native
  iOS/Android setter of the same name. Use them when a login reveals the ID: a
  repeat `Configure()` is a no-op and ignores its `customerUserId`. `null`, an
  empty string, and whitespace all clear the stored ID, so
  `ClearCustomerUserId()` is the explicit spelling of `SetCustomerUserId(null)`
  rather than separate behavior. This differs from `Configure()`, where an empty
  `customerUserId` means "not provided" because it never clears.

### Changed

- Pinned native SDKs are now Appstack iOS SDK `4.5.0` (from `4.4.0`) and
  Appstack Android SDK `1.7.0` (from `1.5.0`). Platform floors are unchanged:
  iOS 15.0+ and Android API level 21+.
- `SendEvent(EventType.INSTALL)` is a no-op. `EventType.INSTALL` is emitted
  automatically by the native SDKs on first launch, and both new pinned versions
  discard a manually sent `INSTALL`; the previous pins accepted it.

## [1.0.0] - 2026-07-17

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
