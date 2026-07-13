# Unity player integration fixture

This fixture is Phase D of the SDK validation matrix. It creates a clean
temporary Unity 6 project, imports this repository through a local `file:`
dependency, and compiles a scene that references every public SDK operation.
It never launches a player or sends requests to Appstack.

## Requirements

- Unity `6000.5.3f1` with Android and iOS build support. Override the editor
  location with `UNITY_EDITOR=/path/to/Unity` when necessary.
- EDM4U's Git package and the native Android/Swift dependencies must be
  resolvable. The runner leaves package and build caches in their standard
  locations.
- Xcode is required for the iOS compilation check.

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

## Validated contract

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
- the SPM URL and exact `4.4.0-rc0` requirement are present;
- `AppstackSDK` is linked to `UnityFramework` and the application target;
- the Swift language and runtime-embedding build settings are present;
- Xcode resolves the package, compiles the application, and produces
  `AppstackSDK.framework` in the built products.

The runner starts Unity with `-buildTarget Android` or `-buildTarget iOS` for
the corresponding build. This is required in batch mode so Unity loads the
package's platform-conditional postprocessor before `BuildPipeline.BuildPlayer`
runs.

## Not covered

Phase D proves package import, IL2CPP compilation, generated-project wiring,
R8 retention, and native binary linkage. Phase E remains responsible for
installing and launching players on devices or simulators, real native SDK
configuration, event delivery, lifecycle behavior, callback thread assertions,
and backend payload verification.
