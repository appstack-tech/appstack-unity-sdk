#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPOSITORY_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
BRIDGE_SOURCE="$REPOSITORY_ROOT/Runtime/Plugins/iOS/AppstackUnityBridge.swift"
SDK_INPUT="${APPSTACK_IOS_DISTRIBUTION_REPO:-${1:-}}"

if [[ -z "$SDK_INPUT" ]]; then
    echo "Set APPSTACK_IOS_DISTRIBUTION_REPO or pass the ios-appstack-sdk checkout path."
    exit 2
fi

TEMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/appstack-unity-ios-contract.XXXXXX")"
trap 'rm -rf "$TEMP_DIR"' EXIT

PACKAGE_DIR="$TEMP_DIR/package"
mkdir -p "$PACKAGE_DIR"
cp -R "$SCRIPT_DIR/Package.swift" "$SCRIPT_DIR/Stubs" "$SCRIPT_DIR/Tests" "$PACKAGE_DIR/"
mkdir -p "$PACKAGE_DIR/Sources/AppstackUnityBridge"
cp "$BRIDGE_SOURCE" "$PACKAGE_DIR/Sources/AppstackUnityBridge/AppstackUnityBridge.swift"

swift test --package-path "$PACKAGE_DIR"

# Must match the pin in Editor/AppstackIOSPostProcessBuild.cs.
EXPECTED_SDK_VERSION="4.5.0"

EXACT_SDK_DIR="$TEMP_DIR/exact-sdk"
mkdir -p "$EXACT_SDK_DIR"
if [[ -d "$SDK_INPUT/.git" || -f "$SDK_INPUT/.git" ]]; then
    git -C "$SDK_INPUT" archive "$EXPECTED_SDK_VERSION" AppstackSDK.xcframework \
        | tar -x -C "$EXACT_SDK_DIR"
    XCFRAMEWORK="$EXACT_SDK_DIR/AppstackSDK.xcframework"
    # Only this branch pins a tag, so only this branch can claim a version. An
    # XCFramework carries no trustworthy marketing version (CFBundleShortVersionString
    # is 1.0), so a caller-supplied binary cannot be checked against the pin.
    VERIFIED_AGAINST="AppstackSDK $EXPECTED_SDK_VERSION"
elif [[ "$SDK_INPUT" == *.xcframework ]]; then
    XCFRAMEWORK="$SDK_INPUT"
    VERIFIED_AGAINST="the supplied XCFramework at $SDK_INPUT (version not verified)"
else
    XCFRAMEWORK="$SDK_INPUT/AppstackSDK.xcframework"
    VERIFIED_AGAINST="the XCFramework in $SDK_INPUT (version not verified)"
fi

SIMULATOR_FRAMEWORK="$XCFRAMEWORK/ios-arm64_x86_64-simulator/AppstackSDK.framework"
if [[ ! -d "$SIMULATOR_FRAMEWORK" ]]; then
    echo "Missing simulator framework at $SIMULATOR_FRAMEWORK"
    exit 3
fi

SIMULATOR_SDK="$(xcrun --sdk iphonesimulator --show-sdk-path)"
OBJECT_FILE="$TEMP_DIR/AppstackUnityBridge.o"
xcrun swiftc \
    -parse-as-library \
    -target arm64-apple-ios15.0-simulator \
    -sdk "$SIMULATOR_SDK" \
    -F "$(dirname "$SIMULATOR_FRAMEWORK")" \
    -c "$BRIDGE_SOURCE" \
    -o "$OBJECT_FILE"

EXPECTED_SYMBOLS=(
    AppstackUnityConfigure
    AppstackUnitySendEvent
    AppstackUnityEnableAppleAdsAttribution
    AppstackUnityGetAppstackId
    AppstackUnityIsSdkDisabled
    AppstackUnityGetAttributionParams
    AppstackUnityFreeCString
)

SYMBOLS="$(nm -g "$OBJECT_FILE")"
for symbol in "${EXPECTED_SYMBOLS[@]}"; do
    if ! grep -q "_$symbol" <<<"$SYMBOLS"; then
        echo "Missing expected C symbol: $symbol"
        exit 4
    fi
done

echo "Verified iOS bridge against $VERIFIED_AGAINST and all expected C symbols."
