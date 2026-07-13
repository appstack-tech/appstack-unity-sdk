# Appstack Unity SDK development guide

This document is for package maintainers and contributors. Integrators should
start with [README.md](README.md).

## Package layout

The repository root is the Unity Package Manager package root:

- `Runtime/` contains the public C# API and platform bridges.
- `Runtime/Plugins/Android/` contains the Java bridge and keep-rule source.
- `Runtime/Plugins/iOS/` contains the Swift C-ABI bridge.
- `Editor/` contains dependency declarations and generated-project hooks.
- `Tests/Editor/` contains Unity Test Framework editor tests.
- `Tests~/Native/` contains native bridge contract fixtures excluded from Unity
  import and release archives.
- `Tests~/Integration/` contains the clean Unity player build fixture, also
  excluded from Unity import and release archives.
- `Samples~/` and `Documentation~/` contain integrator-facing package content.

Keep all Unity `.meta` files committed. Generate a new unique GUID for every new
asset; never copy a GUID from an existing metadata file.

## Supported package contract

The initial public API is defined by `Runtime/AppstackSDK.cs`:

- `Configure(apiKey, logLevel, customerUserId)`
- `SendEvent(eventType, eventName, parameters)`
- `EnableAppleAdsAttribution()`
- `GetAppstackId()`
- `IsSdkDisabled()`
- `GetAttributionParams(onSuccess, onError)`

Do not expose additional native APIs without an explicit cross-wrapper contract
decision. In particular, native testing helpers and development proxy controls
must remain internal.

Cross-platform behavior that must remain aligned:

- Wrapper version: `AppstackVersion.WrapperVersion`, generated as
  `unity-<package.json version>` by `scripts~/set-version.mjs`. C# passes this
  value to both native bridges, and an editor test verifies its package-version
  component against `package.json`.
- Log levels: `0=DEBUG`, `1=INFO`, `2=WARN`, `3=ERROR`; iOS maps warning to
  error.
- Only `CUSTOM` events forward a caller-supplied event name.
- Native event-type parsing is case-insensitive and locale-independent.
- Attribution callbacks support concurrent requests and return to the captured
  synchronization context when available.
- Strings crossing the iOS C boundary are UTF-8 and must be released through
  the matching bridge function.

## Native dependencies

Dependency versions are declared in the platform-specific editor integration:

- Android: `tech.appstack.android-sdk:appstack-android-sdk:1.5.0-rc1` in
  `Editor/AppstackDependencies.xml`
- iOS: `AppstackSDK` Swift package product at `4.4.0-rc0` in
  `Editor/AppstackIOSPostProcessBuild.cs`

When native stable versions are published, update the corresponding editor
integration, public setup documentation, changelog, and validation matrix
together.

## Android architecture

`Runtime/AppstackAndroidBridge.cs` caches the Java bridge class and exposes a
single C# boundary for native operations. The C# caller obtains
`UnityPlayer.currentActivity.applicationContext` for configuration; the Java
bridge has no direct `UnityPlayer` dependency.

`AppstackUnityBridge.java` calls the Android wrapper entry point
`configureWrapper` and adapts the SDK's suspend attribution API through a Kotlin
`Continuation`. This Java-source bridge is intentional for the first release.
Moving it into an AAR published from the Android SDK repository is deferred.

R8 reaches the bridge through JNI, so its class and callback interface need keep
rules. `Runtime/Plugins/Android/proguard-user.txt` is the source of those rules.
Unity does not consume that file directly from a UPM package. Instead,
`AppstackAndroidPostGenerateGradleProject` merges the rules into the generated
`unityLibrary/proguard-unity.txt` file.

The merge must remain idempotent, preserve existing line endings and UTF-8 BOM
state, avoid joining another plugin's final line, and run late enough to reduce
the chance of a later writer replacing the file.

## iOS architecture

`AppstackUnityBridge.swift` exports wrapper-neutral C symbols with `@_cdecl` and
imports the native SDK through `@_spi(AppstackInternal)`. It configures the
native SDK with the Unity wrapper version and owns any C strings returned to C#
until `AppstackUnityFreeCString` releases them.

`AppstackIOSPostProcessBuild` adds the exact-version Swift package reference,
links the product to `UnityFramework` so the bridge compiles, and links it to
the application target so Xcode embeds and signs the dynamic framework. It also
sets the Swift language version and enables Swift standard-library embedding on
the application target.

The `4.4.0-rc0` Swift package uses its binary XCFramework. Its private Swift
interfaces expose the `AppstackInternal` SPI used by this bridge. The native
contract fixture compiles the production bridge against the exact tagged binary
and must pass before adopting any future binary SDK tag.

Do not manually add Apple system frameworks in the Unity postprocessor; the
native Swift package owns its linker requirements.

## Internal development proxy

For internal testing only, the bridges read `APPSTACK_DEV_PROXY_URL` from:

- iOS: the application `Info.plist`.
- Android: application manifest `<meta-data>`.

The value is routed through native internal APIs. It is intentionally absent
from the public Unity API and integrator documentation.

## Testing

Run the editor tests through the Unity Test Runner, then validate from a clean
Unity 6 project using a local `file:` package dependency.

Run the deterministic native bridge contracts as described in
`Tests~/Native/README.md`. They compile the production Java bridge against the
published Android artifact, exercise it against recording JVM stubs, compile
the production Swift bridge against the exact tagged XCFramework, run its Swift
contract tests, and verify its exported C symbols.

Run the Phase D player-build fixture as described in
`Tests~/Integration/README.md`. It imports this repository into a clean Unity 6
project, builds development and minified release Android players, exports and
compiles an iOS player, and inspects the generated outputs for the required
bridge, keep-rule, SPM, target-linking, and framework-embedding contracts.

Android validation:

1. Resolve dependencies and compile a development player.
2. Exercise configure, standard and custom events, ID/status access, and
   multiple simultaneous attribution requests.
3. Verify callbacks called from the main thread return to the main thread.
4. Build a minified release player and confirm the generated ProGuard file has
   exactly one Appstack block and the JNI bridge remains callable.

iOS validation:

1. Export a fresh Xcode project and confirm the Swift package resolves.
2. Confirm the `AppstackSDK` product and Swift bridge compile in
   `UnityFramework`.
3. Confirm the built `.app` contains a signed
   `Frameworks/AppstackSDK.framework`.
4. Build or archive for a physical device.
5. Exercise the same public API operations and verify attribution callback
   threading and UTF-8 values.

Remove any ad-hoc compilation harnesses created under `/tmp` after local checks.

## Deferred native work

- Move the Android Java bridge into a separately published wrapper AAR with
  consumer keep rules.
- Revisit how the Android application context is acquired in the native SDK.
- Add and verify the iOS native SDK privacy manifest.
- Rebuild the iOS binary distribution with its current SPI declarations before
  adopting a binary Swift package target.
- Add wrapper-contract tests to the native repositories and align sibling
  wrappers opportunistically.
