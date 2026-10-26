#!/usr/bin/env bash
# Assembles the root UPM package for one version into dist/ and zips it:
#
#   scripts~/assemble-package.sh <version> [--candidate]
#
# Writes dist/appstack-unity-sdk-<version>/ and appstack-unity-sdk-<version>.zip
# at the repository root, and prints both paths as `package_root=` and
# `package_name=` lines (append them to $GITHUB_ENV in a workflow).
#
# Without --candidate it also enforces the release rules on the native pins:
# both must name a released X.Y.Z version, and nothing in the package may name
# a -SNAPSHOT coordinate. release.yml and release-pr.yml run it that way. CI runs
# it with --candidate, because develop may pin native release candidates.
set -euo pipefail

VERSION="${1:?usage: assemble-package.sh <version> [--candidate]}"
CANDIDATE=false
if [[ "${2:-}" == "--candidate" ]]; then
  CANDIDATE=true
elif [[ -n "${2:-}" ]]; then
  echo "::error::Unknown option: $2"
  exit 1
fi

cd "$(dirname "$0")/.."

PACKAGE_NAME="appstack-unity-sdk-${VERSION}"
PACKAGE_ROOT="dist/${PACKAGE_NAME}"
PACKAGE_ENTRIES=(
  package.json package.json.meta
  README.md README.md.meta
  CHANGELOG.md CHANGELOG.md.meta
  LICENSE.md LICENSE.md.meta
  Runtime Runtime.meta
  Editor Editor.meta
  Tests Tests.meta
  "Samples~" "Documentation~"
)

rm -rf "$PACKAGE_ROOT" "${PACKAGE_NAME}.zip"
mkdir -p "$PACKAGE_ROOT"
for entry in "${PACKAGE_ENTRIES[@]}"; do
  if [[ ! -e "$entry" ]]; then
    echo "::error::Required package entry is missing: $entry"
    exit 1
  fi
  cp -R "$entry" "$PACKAGE_ROOT/"
done

if [[ "$CANDIDATE" == "false" ]]; then
  # A -SNAPSHOT coordinate is mutable and expires, so a release must never
  # ship one: the published package would change under integrators, or stop
  # resolving altogether. Developing against a release candidate is fine —
  # the pin just has to be a plain release version by the time it reaches main.
  # Scanning the assembled package covers the dependency declaration and any
  # setup documentation that names it, without a path list to keep in sync.
  if grep -rn -- '-SNAPSHOT' "$PACKAGE_ROOT"; then
    echo "::error::The package would ship a -SNAPSHOT native dependency (see matches above). Move the pin to a released version first."
    exit 1
  fi

  # The same rule for the pins themselves, which also catches an iOS candidate
  # pin (an -rc version) that the SNAPSHOT scan can't see.
  ANDROID_PIN="$(sed -n 's/.*spec="tech\.appstack\.android-sdk:appstack-android-sdk:\([^"]*\)".*/\1/p' Editor/AppstackDependencies.xml)"
  IOS_PIN="$(sed -n 's/.*private const string PackageVersion = "\([^"]*\)".*/\1/p' Editor/AppstackIOSPostProcessBuild.cs)"
  for pin in "Android:$ANDROID_PIN" "iOS:$IOS_PIN"; do
    if [[ ! "${pin#*:}" =~ ^[0-9]+[.][0-9]+[.][0-9]+$ ]]; then
      echo "::error::The ${pin%%:*} native pin must be a released X.Y.Z version, found '${pin#*:}'."
      exit 1
    fi
  done
  echo "Native pins: Android $ANDROID_PIN, iOS $IOS_PIN."
fi

(cd dist && zip -q -r "../${PACKAGE_NAME}.zip" "$PACKAGE_NAME")
echo "package_name=${PACKAGE_NAME}.zip"
echo "package_root=${PACKAGE_ROOT}"
