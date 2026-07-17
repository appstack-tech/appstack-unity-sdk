#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGE_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.5.3f1/Unity.app/Contents/MacOS/Unity}"
MODE="${1:-all}"

if [[ ! -x "$UNITY_EDITOR" ]]; then
  echo "Unity Editor is not executable at $UNITY_EDITOR" >&2
  exit 1
fi

case "$MODE" in
  all|import|android|ios) ;;
  *)
    echo "Usage: $0 [all|import|android|ios]" >&2
    exit 2
    ;;
esac

WORK_DIR="$(mktemp -d "${TMPDIR:-/tmp}/appstack-unity-player-validation.XXXXXX")"
PROJECT_DIR="$WORK_DIR/project"
LOG_DIR="$WORK_DIR/logs"
mkdir -p "$PROJECT_DIR" "$LOG_DIR"
cp -R "$SCRIPT_DIR/UnityProject/." "$PROJECT_DIR/"

escaped_package_root="${PACKAGE_ROOT//\\/\\\\}"
escaped_package_root="${escaped_package_root//&/\\&}"
escaped_package_root="${escaped_package_root//|/\\|}"
sed "s|__APPSTACK_PACKAGE_ROOT__|$escaped_package_root|g" \
  "$PROJECT_DIR/Packages/manifest.json.in" > "$PROJECT_DIR/Packages/manifest.json"

run_unity() {
  local method="$1"
  local log_name="$2"
  local build_target="${3:-}"
  local unity_arguments=(
    -batchmode
    -nographics
    -quit
    -projectPath "$PROJECT_DIR"
  )
  if [[ -n "$build_target" ]]; then
    unity_arguments+=(-buildTarget "$build_target")
  fi
  unity_arguments+=(
    -executeMethod "$method"
    -logFile "$LOG_DIR/$log_name.log"
  )
  "$UNITY_EDITOR" "${unity_arguments[@]}"
}

echo "Generated-player validation workspace: $WORK_DIR"

if [[ "$MODE" == "all" || "$MODE" == "import" ]]; then
  run_unity AppstackIntegrationBuild.ValidateImport import
fi

if [[ "$MODE" == "all" || "$MODE" == "android" ]]; then
  run_unity AppstackIntegrationBuild.BuildAndroidPlayers android Android

  development_apk="$PROJECT_DIR/Builds/PlayerValidation/Android/appstack-player-validation-development.apk"
  release_apk="$PROJECT_DIR/Builds/PlayerValidation/Android/appstack-player-validation-release.apk"
  [[ -f "$development_apk" ]] || { echo "Missing development APK" >&2; exit 1; }
  [[ -f "$release_apk" ]] || { echo "Missing release APK" >&2; exit 1; }

  android_root="$(cd "$(dirname "$UNITY_EDITOR")/../../../PlaybackEngines/AndroidPlayer" && pwd)"
  apkanalyzer="$android_root/SDK/cmdline-tools/16.0/bin/apkanalyzer"
  if [[ ! -x "$apkanalyzer" ]]; then
    echo "apkanalyzer is not executable at $apkanalyzer" >&2
    exit 1
  fi

  dex_packages="$WORK_DIR/release-dex-packages.txt"
  "$apkanalyzer" dex packages "$release_apk" > "$dex_packages"
  grep -F "com.appstack.unity.AppstackUnityBridge" "$dex_packages" >/dev/null
  grep -F 'com.appstack.unity.AppstackUnityBridge$AttributionParamsCallback' \
    "$dex_packages" >/dev/null
  echo "Android development/release builds and minified JNI retention passed."
fi

if [[ "$MODE" == "all" || "$MODE" == "ios" ]]; then
  run_unity AppstackIntegrationBuild.ExportIOSPlayer ios iOS

  ios_output="$PROJECT_DIR/Builds/PlayerValidation/iOS"
  ios_container=(-project "$ios_output/Unity-iPhone.xcodeproj")
  if [[ -d "$ios_output/Unity-iPhone.xcworkspace" ]]; then
    ios_container=(-workspace "$ios_output/Unity-iPhone.xcworkspace")
  fi
  derived_data="$WORK_DIR/DerivedData"
  xcodebuild \
    "${ios_container[@]}" \
    -scheme Unity-iPhone \
    -configuration Release \
    -sdk iphoneos \
    -destination 'generic/platform=iOS' \
    -derivedDataPath "$derived_data" \
    CODE_SIGNING_ALLOWED=NO \
    build > "$LOG_DIR/xcodebuild.log"

  appstack_framework="$(find "$derived_data/Build/Products" \
    -path '*.app/Frameworks/AppstackSDK.framework/AppstackSDK' -print -quit)"
  [[ -n "$appstack_framework" ]] || {
    echo "Built application does not embed AppstackSDK.framework" >&2
    exit 1
  }
  echo "iOS export, package resolution, compilation, and framework embedding passed."
fi

echo "Generated-player validation passed. Logs and generated players remain at $WORK_DIR"
