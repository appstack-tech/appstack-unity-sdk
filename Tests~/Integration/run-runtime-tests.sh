#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGE_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.5.3f1/Unity.app/Contents/MacOS/Unity}"
MODE="${1:-ios}"
BUNDLE_ID="tech.appstack.unity.runtimevalidation"

case "$MODE" in
  ios|android|android-build) ;;
  *)
    echo "Usage: $0 [ios|android|android-build]" >&2
    exit 2
    ;;
esac

if [[ ! -x "$UNITY_EDITOR" ]]; then
  echo "Unity Editor is not executable at $UNITY_EDITOR" >&2
  exit 1
fi

WORK_DIR="$(mktemp -d "${TMPDIR:-/tmp}/appstack-unity-runtime-validation.XXXXXX")"
PROJECT_DIR="$WORK_DIR/project"
LOG_DIR="$WORK_DIR/logs"
PORT_FILE="$WORK_DIR/mock-port.txt"
TLS_PORT_FILE="$WORK_DIR/mock-tls-port.txt"
TLS_CERT="$WORK_DIR/mock-ca.pem"
TLS_KEY="$WORK_DIR/mock-ca-key.pem"
REQUESTS_FILE="$WORK_DIR/mock-requests.jsonl"
RUNTIME_LOG="$LOG_DIR/runtime.log"
mkdir -p "$PROJECT_DIR" "$LOG_DIR"
cp -R "$SCRIPT_DIR/UnityProject/." "$PROJECT_DIR/"

escaped_package_root="${PACKAGE_ROOT//\\/\\\\}"
escaped_package_root="${escaped_package_root//&/\\&}"
escaped_package_root="${escaped_package_root//|/\\|}"
sed "s|__APPSTACK_PACKAGE_ROOT__|$escaped_package_root|g" \
  "$PROJECT_DIR/Packages/manifest.json.in" > "$PROJECT_DIR/Packages/manifest.json"

mock_arguments=(
  --port-file "$PORT_FILE"
  --requests-file "$REQUESTS_FILE"
)
if [[ "$MODE" != "ios" ]]; then
  openssl req -x509 -newkey rsa:2048 -sha256 -nodes -days 1 \
    -subj '/CN=127.0.0.1' \
    -addext 'subjectAltName=IP:127.0.0.1' \
    -keyout "$TLS_KEY" \
    -out "$TLS_CERT" \
    >/dev/null 2>&1
  mock_arguments+=(
    --tls-cert "$TLS_CERT"
    --tls-key "$TLS_KEY"
    --tls-port-file "$TLS_PORT_FILE"
  )
fi

python3 "$SCRIPT_DIR/mock_server.py" "${mock_arguments[@]}" \
  > "$LOG_DIR/mock-server.log" 2>&1 &
mock_pid=$!
runtime_pid=""
adb=""
serial=""
reversed_http_port=""
reversed_tls_port=""
android_installed=""

cleanup() {
  if [[ -n "$runtime_pid" ]]; then
    kill "$runtime_pid" 2>/dev/null || true
    wait "$runtime_pid" 2>/dev/null || true
  fi
  if [[ -n "$android_installed" ]]; then
    "$adb" -s "$serial" shell am force-stop "$android_installed" >/dev/null 2>&1 || true
    "$adb" -s "$serial" uninstall "$android_installed" >/dev/null 2>&1 || true
  fi
  if [[ -n "$reversed_http_port" ]]; then
    "$adb" -s "$serial" reverse --remove "tcp:$reversed_http_port" >/dev/null 2>&1 || true
  fi
  if [[ -n "$reversed_tls_port" ]]; then
    "$adb" -s "$serial" reverse --remove "tcp:$reversed_tls_port" >/dev/null 2>&1 || true
  fi
  kill "$mock_pid" 2>/dev/null || true
  wait "$mock_pid" 2>/dev/null || true
}
trap cleanup EXIT

for _ in {1..100}; do
  if [[ -s "$PORT_FILE" && ( "$MODE" == "ios" || -s "$TLS_PORT_FILE" ) ]]; then
    break
  fi
  sleep 0.1
done
if [[ ! -s "$PORT_FILE" ]]; then
  echo "Mock backend did not start" >&2
  exit 1
fi
if [[ "$MODE" != "ios" && ! -s "$TLS_PORT_FILE" ]]; then
  echo "Mock TLS attribution endpoint did not start" >&2
  exit 1
fi

port="$(cat "$PORT_FILE")"
tls_port=""
if [[ "$MODE" != "ios" ]]; then
  tls_port="$(cat "$TLS_PORT_FILE")"
fi
proxy_url="http://127.0.0.1:$port"
export APPSTACK_RUNTIME_PROXY_URL="$proxy_url"
export APPSTACK_RUNTIME_IOS_ARCH="$(uname -m)"
echo "Runtime integration validation workspace: $WORK_DIR"

wait_for_result() {
  for _ in {1..60}; do
    if grep -F "APPSTACK_RUNTIME_RESULT:" "$RUNTIME_LOG" >/dev/null 2>&1; then
      return 0
    fi
    sleep 1
  done
  echo "Runtime player did not emit a terminal result" >&2
  return 1
}

if [[ "$MODE" == "ios" ]]; then
  simulator="${IOS_SIMULATOR_UDID:-}"
  if [[ -z "$simulator" ]]; then
    simulator="$(xcrun simctl list devices available -j | python3 -c '
import json, sys
data = json.load(sys.stdin)
for devices in data["devices"].values():
    for device in devices:
        if device.get("isAvailable") and device["name"].startswith("iPhone"):
            print(device["udid"])
            raise SystemExit(0)
raise SystemExit(1)
')"
  fi

  state="$(xcrun simctl list devices -j | python3 -c '
import json, sys
target = sys.argv[1]
data = json.load(sys.stdin)
for devices in data["devices"].values():
    for device in devices:
        if device["udid"] == target:
            print(device["state"])
            raise SystemExit(0)
raise SystemExit(1)
' "$simulator")"
  if [[ "$state" != "Booted" ]]; then
    xcrun simctl boot "$simulator"
  fi
  xcrun simctl bootstatus "$simulator" -b

  "$UNITY_EDITOR" \
    -batchmode \
    -nographics \
    -quit \
    -projectPath "$PROJECT_DIR" \
    -buildTarget iOS \
    -executeMethod AppstackIntegrationBuild.ExportIOSSimulatorRuntimePlayer \
    -logFile "$LOG_DIR/unity-ios.log"

  ios_output="$PROJECT_DIR/Builds/RuntimeValidation/iOS"
  ios_container=(-project "$ios_output/Unity-iPhone.xcodeproj")
  if [[ -d "$ios_output/Unity-iPhone.xcworkspace" ]]; then
    ios_container=(-workspace "$ios_output/Unity-iPhone.xcworkspace")
  fi
  derived_data="$WORK_DIR/DerivedData"
  xcodebuild \
    "${ios_container[@]}" \
    -scheme Unity-iPhone \
    -configuration Debug \
    -sdk iphonesimulator \
    -destination 'generic/platform=iOS Simulator' \
    -derivedDataPath "$derived_data" \
    CODE_SIGNING_ALLOWED=NO \
    build > "$LOG_DIR/xcodebuild-ios.log"

  app_path="$(find "$derived_data/Build/Products" -maxdepth 3 -type d -name '*.app' -print -quit)"
  [[ -n "$app_path" ]] || { echo "Xcode produced no simulator app" >&2; exit 1; }
  xcrun simctl uninstall "$simulator" "$BUNDLE_ID" >/dev/null 2>&1 || true
  xcrun simctl install "$simulator" "$app_path"
  xcrun simctl launch --console-pty "$simulator" "$BUNDLE_ID" > "$RUNTIME_LOG" 2>&1 &
  runtime_pid=$!
  wait_for_result
  xcrun simctl terminate "$simulator" "$BUNDLE_ID" >/dev/null 2>&1 || true
else
  android_root="$(cd "$(dirname "$UNITY_EDITOR")/../../../PlaybackEngines/AndroidPlayer" && pwd)"
  adb="$android_root/SDK/platform-tools/adb"
  if [[ "$MODE" == "android" ]]; then
    serial="${ANDROID_SERIAL:-}"
    if [[ -z "$serial" ]]; then
      serial="$("$adb" devices | awk 'NR > 1 && $2 == "device" { print $1; exit }')"
    fi
    if [[ -z "$serial" ]]; then
      echo "No Android device or emulator is available; set ANDROID_SERIAL when one is attached." >&2
      exit 1
    fi
  fi

  android_library="$PROJECT_DIR/Assets/Plugins/Android/AppstackRuntimeValidation.androidlib"
  mkdir -p "$android_library/res/raw" "$android_library/res/xml"
  cp "$TLS_CERT" "$android_library/res/raw/appstack_runtime_validation_ca.pem"
  cp "$SCRIPT_DIR/network_security_config.xml" \
    "$android_library/res/xml/appstack_runtime_validation_network_security_config.xml"
  sed "s|__APPSTACK_RUNTIME_PROXY_URL__|$proxy_url|g" \
    "$SCRIPT_DIR/AndroidManifest.xml.in" > "$android_library/AndroidManifest.xml"

  "$UNITY_EDITOR" \
    -batchmode \
    -nographics \
    -quit \
    -projectPath "$PROJECT_DIR" \
    -buildTarget Android \
    -executeMethod AppstackIntegrationBuild.BuildAndroidRuntimePlayer \
    -logFile "$LOG_DIR/unity-android.log"

  apk="$PROJECT_DIR/Builds/RuntimeValidation/Android/appstack-runtime-validation.apk"
  apkanalyzer="$android_root/SDK/cmdline-tools/16.0/bin/apkanalyzer"
  apk_manifest="$LOG_DIR/android-manifest.xml"
  "$apkanalyzer" manifest print "$apk" > "$apk_manifest"
  grep -F 'android:usesCleartextTraffic="true"' "$apk_manifest" >/dev/null
  grep -F 'android:networkSecurityConfig=' "$apk_manifest" >/dev/null
  grep -F 'android:name="APPSTACK_DEV_PROXY_URL"' "$apk_manifest" >/dev/null
  grep -F "android:value=\"$proxy_url\"" "$apk_manifest" >/dev/null

  if [[ "$MODE" == "android-build" ]]; then
    echo "Android runtime player build passed. Artifacts remain at $WORK_DIR"
    exit 0
  fi

  "$adb" -s "$serial" reverse "tcp:$port" "tcp:$port"
  reversed_http_port="$port"
  "$adb" -s "$serial" reverse "tcp:$tls_port" "tcp:$tls_port"
  reversed_tls_port="$tls_port"
  "$adb" -s "$serial" uninstall "$BUNDLE_ID" >/dev/null 2>&1 || true
  "$adb" -s "$serial" install "$apk" >/dev/null
  android_installed="$BUNDLE_ID"
  "$adb" -s "$serial" logcat -c
  "$adb" -s "$serial" logcat -v threadtime > "$RUNTIME_LOG" 2>&1 &
  runtime_pid=$!
  "$adb" -s "$serial" shell monkey -p "$BUNDLE_ID" 1 >/dev/null
  wait_for_result
  "$adb" -s "$serial" shell am force-stop "$BUNDLE_ID"
  "$adb" -s "$serial" reverse --remove "tcp:$reversed_http_port" >/dev/null 2>&1 || true
  "$adb" -s "$serial" reverse --remove "tcp:$reversed_tls_port" >/dev/null 2>&1 || true
  reversed_http_port=""
  reversed_tls_port=""
fi

python3 "$SCRIPT_DIR/validate_runtime.py" \
  --runtime-log "$RUNTIME_LOG" \
  --requests-file "$REQUESTS_FILE"
echo "$MODE runtime integration validation passed. Artifacts remain at $WORK_DIR"
