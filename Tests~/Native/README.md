# Native bridge contract tests

These fixtures compile and exercise the production Java and Swift bridge sources.
They live under `Tests~/` so Unity does not import them or include them in the UPM
package artifact.

## Android

The Android fixture has two modules:

- `real-artifact` compiles the production Java bridge against
  `tech.appstack.android-sdk:appstack-android-sdk:1.5.0`.
- `contract-tests` compiles the same bridge against recording stubs and tests
  configuration, proxy metadata, log/event mapping, JSON conversion, getters,
  and immediate, suspended, and failed coroutine completion.

Run it with Gradle 8.13 or a compatible wrapper:

```sh
GRADLEW=/path/to/gradlew Tests~/Native/Android/run-tests.sh
```

The Android 35 SDK, JDK 17, Google Maven, and Maven Central must be available.

## iOS

The iOS runner copies the production Swift bridge into a temporary Swift package.
It first runs deterministic XCTest cases against a recording `AppstackSDK` module,
then extracts the exact `4.4.0` XCFramework from an `ios-appstack-sdk` checkout,
compiles the production bridge for an iOS 15 simulator target, and checks every
expected C ABI symbol with `nm`.

```sh
APPSTACK_IOS_DISTRIBUTION_REPO=/path/to/ios-appstack-sdk \
  Tests~/Native/iOS/run-tests.sh
```

Xcode with an iOS Simulator SDK and Swift 5.9 or newer is required.
