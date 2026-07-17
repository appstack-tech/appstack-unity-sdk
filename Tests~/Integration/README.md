# Unity player integration fixture

This fixture provides two complementary integration checks. The generated-player
validation imports the package and verifies Android and iOS build outputs
without launching them. The runtime integration validation launches an
IL2CPP player against a local recording backend. Both checks create a clean
temporary Unity 6 project and import this repository through a local `file:`
dependency. Neither needs a production Appstack API key or sends requests to
Appstack.

## Requirements

- Unity `6000.5.3f1` with Android and iOS build support. Override the editor
  location with `UNITY_EDITOR=/path/to/Unity` when necessary.
- EDM4U's Git package and the native Android/Swift dependencies must be
  resolvable. The runner leaves package and build caches in their standard
  locations.
- Xcode is required for the iOS compilation check.
- Runtime validation on iOS requires an available iPhone simulator. Override
  its automatic selection with `IOS_SIMULATOR_UDID=<udid>`.
- Runtime validation on Android requires an attached emulator or device
  authorized by `adb`. Override its automatic selection with
  `ANDROID_SERIAL=<serial>`.

## Generated-player validation

Run the complete matrix:

```sh
Tests~/Integration/run-tests.sh
```

Run one part while iterating:

```sh
Tests~/Integration/run-tests.sh import
Tests~/Integration/run-tests.sh android
Tests~/Integration/run-tests.sh ios
```

The runner prints its temporary workspace and retains it for inspection. Unity
logs, Xcode logs, generated projects, APKs, and DerivedData live only there and
are never imported into the UPM package or release archive.

### Validated contract

The import check proves that a clean Unity 6 project can resolve the package,
compile its assemblies, and apply the committed plugin importer metadata. The
Java bridge must be Android-only and the Swift bridge iOS-only.

Android builds both a development and a release IL2CPP APK. Release
minification is enabled. The fixture verifies that the generated
`proguard-unity.txt` has one Appstack block, then inspects the release DEX to
prove that `AppstackUnityBridge` and `AttributionParamsCallback` survived R8.

iOS exports an IL2CPP Xcode project and verifies all of the following before
compiling it without code signing:

- the Swift bridge is copied and compiled in `UnityFramework`;
- the SPM URL and exact `4.4.0` requirement are present;
- `AppstackSDK` is linked to `UnityFramework` and the application target;
- the Swift language and runtime-embedding build settings are present;
- Xcode resolves the package, compiles the application, and produces
  `AppstackSDK.framework` in the built products.

The runner starts Unity with `-buildTarget Android` or `-buildTarget iOS` for
the corresponding build. This is required in batch mode so Unity loads the
package's platform-conditional postprocessor before `BuildPipeline.BuildPlayer`
runs.

### Not covered

This check proves package import, IL2CPP compilation, generated-project wiring,
R8 retention, and native binary linkage. It does not execute native SDK code.

## Runtime integration validation

Run on iOS Simulator:

```sh
Tests~/Integration/run-runtime-tests.sh ios
```

Run on an attached Android target:

```sh
Tests~/Integration/run-runtime-tests.sh android
```

Compile the Android runtime player and generated test manifest without a
target:

```sh
Tests~/Integration/run-runtime-tests.sh android-build
```

The runner starts a loopback recording backend on random ports and configures
the native SDK through the bridges' internal development-proxy mechanism.
Android requires an HTTPS attribution URL, so the runner generates a one-day
local certificate and trusts it through a network-security resource bundled
only in the temporary APK. Both its HTTP API port and HTTPS attribution port
use `adb reverse`. iOS Simulator reaches the host HTTP loopback address
directly. The proxy setting, certificate, and dummy API key exist only in the
temporary test player and cannot enter the UPM package or release archive.

Android app installation is removed during runner cleanup. The temporary host
workspace and its logs remain available at the printed path for inspection.

The machine-readable runtime probe and request validator prove that:

- the player is IL2CPP and loads the pinned native SDK through the production
  Unity bridge;
- native configuration fetches remote configuration and produces an Appstack
  ID without reporting the SDK disabled;
- native install attribution performs a local match request and preserves
  UTF-8 query parameters through the callback, using each native SDK's expected
  `match_url` form;
- three immediately successive attribution requests all complete once on the
  Unity main thread;
- after remote configuration is observably ready, a custom event and a
  standard event reach the native HTTP wire boundary;
- `customer_user_id`, `unity-1.0.0`, numbers, booleans, arrays, nested maps,
  and UTF-8 strings retain their expected wire representation.

The backend also records native lifecycle or install events, but their exact
count is deliberately not asserted because it is native-SDK state dependent.

### Not covered

The local backend validates the wrapper/native boundary and native networking,
not production service behavior. Simulator execution does not validate real
Apple Ads attribution. Physical-device lifecycle transitions, deferred links,
offline retry across process restarts, production TLS, and store-distributed
release signing remain separate system or manual tests.
