#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
GRADLE_COMMAND="${GRADLEW:-gradle}"

"$GRADLE_COMMAND" \
    -p "$SCRIPT_DIR" \
    :contract-tests:test \
    :real-artifact:assembleDebug
