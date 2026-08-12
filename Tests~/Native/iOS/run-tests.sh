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
    # Resolve the binary the way SPM does, from the binaryTarget the tag declares.
    # The distribution repo also still carries a committed AppstackSDK.xcframework
    # directory, but it is vestigial: it is byte-identical across 4.4.0 through
    # 4.5.0 and does not track the tag. Reading it made this fixture silently
    # compile against stale bits while reporting the pinned version, so resolve
    # the declared URL and enforce the declared checksum instead.
    PACKAGE_MANIFEST="$TEMP_DIR/distribution-Package.swift"
    git -C "$SDK_INPUT" show "${EXPECTED_SDK_VERSION}:Package.swift" > "$PACKAGE_MANIFEST"

    BINARY_URL="$(sed -n 's/.*url:[[:space:]]*"\([^"]*\)".*/\1/p' "$PACKAGE_MANIFEST" | head -1)"
    DECLARED_CHECKSUM="$(sed -n 's/.*checksum:[[:space:]]*"\([^"]*\)".*/\1/p' "$PACKAGE_MANIFEST" | head -1)"

    if [[ -z "$BINARY_URL" || -z "$DECLARED_CHECKSUM" ]]; then
        echo "Could not read a binaryTarget url and checksum from Package.swift at $EXPECTED_SDK_VERSION."
        exit 5
    fi

    # Anchor the whole origin, not just the path: a substring match would accept
    # any host that happens to carry /download/<version>/, and the checksum comes
    # from the same manifest, so it proves integrity against whatever that
    # manifest claims rather than provenance. This origin is the same one
    # Editor/AppstackIOSPostProcessBuild.cs pins, so asserting it here keeps the
    # fixture checking the artifact a Unity build would actually resolve.
    EXPECTED_RELEASE_PREFIX="https://github.com/appstack-tech/ios-appstack-sdk/releases/download/${EXPECTED_SDK_VERSION}/"
    if [[ "$BINARY_URL" != "$EXPECTED_RELEASE_PREFIX"* ]]; then
        echo "Package.swift at $EXPECTED_SDK_VERSION does not point at the expected release."
        echo "  expected prefix: $EXPECTED_RELEASE_PREFIX"
        echo "  declared url:    $BINARY_URL"
        exit 6
    fi

    # Bounded and retried: this fixture reaches the network, so a stalled or
    # flaky download must fail on its own rather than hang until the outer CI
    # timeout.
    ARCHIVE="$TEMP_DIR/AppstackSDK.xcframework.zip"
    if ! curl -fsSL --connect-timeout 15 --max-time 300 --retry 3 --retry-delay 2 \
        -o "$ARCHIVE" "$BINARY_URL"; then
        echo "Failed to download the pinned XCFramework from $BINARY_URL"
        exit 7
    fi

    ACTUAL_CHECKSUM="$(shasum -a 256 "$ARCHIVE" | awk '{print $1}')"
    if [[ "$ACTUAL_CHECKSUM" != "$DECLARED_CHECKSUM" ]]; then
        echo "Checksum mismatch for $BINARY_URL"
        echo "  declared: $DECLARED_CHECKSUM"
        echo "  actual:   $ACTUAL_CHECKSUM"
        exit 8
    fi

    unzip -q "$ARCHIVE" -d "$EXACT_SDK_DIR"
    XCFRAMEWORK="$EXACT_SDK_DIR/AppstackSDK.xcframework"
    # Only this branch resolves and checksums the pinned artifact, so only this
    # branch can claim a version. An XCFramework carries no trustworthy marketing
    # version (CFBundleShortVersionString is 1.0), so a caller-supplied binary
    # cannot be checked against the pin.
    VERIFIED_AGAINST="AppstackSDK $EXPECTED_SDK_VERSION (checksum-verified release artifact)"
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
    AppstackUnitySetCustomerUserId
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
