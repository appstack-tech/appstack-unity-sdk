# Appstack Unity SDK release guide

This document is for release maintainers. Integrators should use
[README.md](README.md).

## Release artifact

The GitHub Actions workflow packages the root-level UPM contents. It:

- Reads the version from the root `package.json`.
- Fails when the Git tag and package version differ.
- Packages the root UPM contents rather than creating an `Assets/` tree.
- Includes `LICENSE.md`, public documentation, runtime, editor, tests, samples,
  and all corresponding `.meta` files.
- Excludes repository-only files and generated output. The workflow allowlist
  controls the GitHub ZIP, while `.npmignore` applies the same boundary to the
  tarball built by OpenUPM.
- Fails when the assembled package contains a `-SNAPSHOT` coordinate anywhere.
  A snapshot pin is mutable and expires, so a release must never ship one.
- Signs the package with `upm pack`, using a pinned, checksum-verified Unity UPM
  CLI, and verifies that the signed archive carries an attestation and the
  expected package name and version.
- Attaches both artifacts to the GitHub Release: the ZIP for manual installation
  and the signed `.tgz`.
- Builds the release body with `node scripts~/release-notes.mjs <version>`: the
  tagged `CHANGELOG.md` section first, then manual installation through Unity
  Package Manager. The step fails when the changelog has no section for the tag,
  so a release cannot publish install steps and no notes.
- Triggers an OpenUPM scan and waits until the tagged version is installable or
  OpenUPM reports a build failure.

Signing requires the `UPM_SERVICE_ACCOUNT_KEY_ID`, `UPM_SERVICE_ACCOUNT_KEY_SECRET`,
and `UPM_ORG_ID` repository secrets. The job fails before packing when any of
them is missing.

## Before tagging

1. Confirm the release uses the intended stable Android and iOS native SDK
   versions.
2. Update both native dependency pins, their validation fixtures, and the
   public platform documentation together. Both pins must name a released
   version; the workflow rejects a `-SNAPSHOT` coordinate.
3. Run `node scripts~/set-version.mjs <version>`. This updates the root
   `package.json` and regenerates `Runtime/AppstackVersion.cs` together.
4. Run `node scripts~/set-version.mjs --check <version>`. The editor tests and
   release workflow run equivalent validation; both native bridges receive the
   resulting `unity-<version>` value from C#.
5. Move relevant entries from `Unreleased` to a versioned changelog section. The
   release workflow reads that section as the release body and fails without it.
   Preview it with `node scripts~/release-notes.mjs <version>`.
6. Confirm every package asset has a committed, unique `.meta` file.

## Validation

Complete the device-build matrix in [DEVELOPMENT.md](DEVELOPMENT.md), including:

- Unity Test Runner editor tests.
- Android development and minified release builds on a physical device.
- iOS physical-device build or archive from a fresh Unity export.
- Configure, events, ID/status calls, concurrent attribution callbacks, main
  thread delivery, and UTF-8 attribution values on both platforms.

Run `node --test scripts~/release-notes.test.mjs`. It covers the changelog
section extractor, including the heading matches that must not resolve: a tag
whose version only prefixes a longer one, such as 1.2.1 against a 1.2.10
section.

Inspect the release archive before publishing. Adding it as a local package to a
clean Unity project must produce the same package contents and behavior as a
repository checkout.

## Testing a release candidate

A Unity candidate is tested straight from git or from a local tarball. It is not
published to OpenUPM and it does not get a version tag: OpenUPM stays
stable-only, and the release workflow's tag filter matches stable `X.Y.Z` only.

1. Put the candidate on a branch and set its version:

   ```bash
   git checkout -b release/1.7
   node scripts~/set-version.mjs 1.7.0-rc.1
   git commit -am "1.7.0-rc.1"
   git push -u origin release/1.7
   ```

   `set-version.mjs` accepts a SemVer prerelease, so `package.json` and
   `Runtime/AppstackVersion.cs` both carry `1.7.0-rc.1`, and the wrapper reports
   `unity-1.7.0-rc.1`.

2. Point the native pins at the candidate channels when the candidate includes
   unreleased native changes:

   - Android — in `Editor/AppstackDependencies.xml`, pin
     `tech.appstack.android-sdk:appstack-android-sdk:<X.Y.Z-SNAPSHOT>` and add
     `https://central.sonatype.com/repository/maven-snapshots/` to that
     dependency's `<repositories>`. See the Android SDK's `RELEASING.md`.
   - iOS — `Editor/AppstackIOSPostProcessBuild.cs` references the native package
     at an exact version. Testing an unreleased iOS candidate therefore means
     referencing the native SDK's `rc` channel (branch or revision) instead of a
     version. See the iOS SDK's `RELEASING.md`.

3. Consume it in a Unity project. Use a Git URL with a branch or commit:

   ```text
   https://github.com/appstack-tech/appstack-unity-sdk.git#release/1.7
   https://github.com/appstack-tech/appstack-unity-sdk.git#<commit-sha>
   ```

   or build a local tarball and reference it:

   ```bash
   npm pack   # -> com.appstack.unity-sdk-1.7.0-rc.1.tgz
   ```

   ```json
   "com.appstack.unity-sdk": "file:/path/to/com.appstack.unity-sdk-1.7.0-rc.1.tgz"
   ```

   A Git URL records the resolved commit in the project's lock file and is the
   reproducible form; pin a commit SHA rather than a moving branch when it
   matters.

4. Do not tag a candidate and do not run the publish step. Before releasing,
   restore both native pins to released versions before re-vendoring and
   tagging:

   - Android — set `Editor/AppstackDependencies.xml` back to the released
     `tech.appstack.android-sdk:appstack-android-sdk:X.Y.Z` and drop the
     snapshot repository.
   - iOS — set `PackageVersion` in `Editor/AppstackIOSPostProcessBuild.cs` back
     to the released version.
   - Set the package version to the stable value with
     `node scripts~/set-version.mjs X.Y.Z`, then update the validation fixtures
     and public docs (see [Before tagging](#before-tagging)) and commit. The
     release workflow rejects a `-SNAPSHOT` coordinate, and a stable tag must
     match `package.json`.

## Publish

1. Commit the generated version files and changelog changes.
2. Create a semantic version tag matching `package.json`, for example `1.0.0`.
3. Push the commit and tag.
4. Confirm the GitHub Actions release job succeeds. The job creates the GitHub
   Release, triggers OpenUPM, and waits for the registry version to become
   installable.
5. Confirm the release archive passes a clean-project import check and the
   GitHub release notes carry the version's changelog section above the install
   instructions.
6. Confirm OpenUPM reports the published version as signed. The publish step
   exposes a `signed` output but does not assert it, so an unsigned publish
   still leaves the job green.

The package is already registered with OpenUPM in Git tracking mode. OpenUPM
builds each version by running `npm pack` on the matching Git tag; it does not
consume the artifacts attached to the GitHub Release. The workflow uses
OpenUPM's OIDC-based action, so no OpenUPM token or repository secret is
required.

`.openupm/package-metadata.yml` is a reference copy. OpenUPM reads the
authoritative metadata from the `openupm/openupm` repository. Update listing
details such as topics, image, repository URL, tag filters, or tracking mode by
opening a pull request against that repository.

The release workflow intentionally accepts bare stable tags such as `1.0.1`.
Supporting a prerelease requires updating both `package.json` and the workflow
tag filter so the parsed tag version remains an exact match.
