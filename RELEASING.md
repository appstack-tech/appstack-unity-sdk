# Appstack Unity SDK release guide

This document is for release maintainers. Integrators should use
[README.md](README.md).

## Release artifact

The GitHub Actions workflow packages the root-level UPM contents. It:

- Runs on every push to `main`, and publishes only when the latest
  `CHANGELOG.md` section (`## [X.Y.Z] - YYYY-MM-DD`) names a version with no
  tag yet, newer than the latest release.
- Fails when `package.json` or `Runtime/AppstackVersion.cs` disagree with that
  version (`node scripts~/set-version.mjs --check X.Y.Z`).
- Runs the full CI workflow (`ci.yml`) on the merge commit before publishing.
- Packages the root UPM contents rather than creating an `Assets/` tree.
- Includes `LICENSE.md`, public documentation, runtime, editor, tests, samples,
  and all corresponding `.meta` files.
- Excludes repository-only files and generated output. The workflow allowlist
  controls the GitHub ZIP, while `.npmignore` applies the same boundary to the
  tarball built by OpenUPM.
- Assembles the package with `scripts~/assemble-package.sh`, which fails when
  the package contains a `-SNAPSHOT` coordinate anywhere or either native pin
  isn't a released `X.Y.Z` version. A snapshot pin is mutable and expires, so
  a release must never ship one.
- Signs the package with `upm pack`, using a pinned, checksum-verified Unity UPM
  CLI, and verifies that the signed archive carries an attestation and the
  expected package name and version.
- Creates the `X.Y.Z` tag and GitHub Release on the merge commit, with both
  artifacts attached: the ZIP for manual installation and the signed `.tgz`.
- Builds the release body with `node scripts~/release-notes.mjs <version>`: the
  version's `CHANGELOG.md` section first, then manual installation through Unity
  Package Manager.
- Triggers an OpenUPM scan and waits until the tagged version is installable or
  OpenUPM reports a build failure.

Signing requires the `UPM_SERVICE_ACCOUNT_KEY_ID`, `UPM_SERVICE_ACCOUNT_KEY_SECRET`,
and `UPM_ORG_ID` repository secrets. The job fails before packing when any of
them is missing.

## Branches and how a release happens

- **`develop`** is where work lands. Open PRs against it and squash-merge
  them. Nothing on `develop` is published.
- **`main`** only changes through release PRs. Merging one publishes.

CI (`ci.yml`) runs on every PR into either branch: the version and package
checks, the release-notes tests, and the Android and iOS bridge contracts
from `Tests~/Native`. The Unity Test Runner editor tests and the player builds
in `Tests~/Integration` need a Unity license, so they are not in CI; run them
locally before a release (see [Validation](#validation)).

To cut a stable release:

1. On a branch off `develop`:
   1. Confirm the release uses the intended stable Android and iOS native SDK
      versions. Update both native dependency pins, their validation fixtures,
      and the public platform documentation together. Both pins must name a
      released version.
   2. Run `node scripts~/set-version.mjs X.Y.Z`. It updates `package.json`,
      regenerates `Runtime/AppstackVersion.cs`, and promotes
      `## [Unreleased]` in `CHANGELOG.md` to `## [X.Y.Z] - <today>`. Preview
      the release body with `node scripts~/release-notes.mjs X.Y.Z`.
   3. Confirm every package asset has a committed, unique `.meta` file.

   Merge that into `develop`.
2. Open a PR from `develop` into `main`. The **Release version** check
   (`release-pr.yml`) names the version that merging will publish. It fails if
   the changelog has no new version, if the version copies disagree, or if a
   native pin is a candidate.
3. Get it approved by someone other than you, then merge it with **Create a
   merge commit**. Don't squash: a squash leaves `develop` and `main` with
   different commits for the same changes, and every later release PR then
   conflicts.
4. `release.yml` runs on the push to `main` and publishes (see
   [Release artifact](#release-artifact)).

A merge to `main` whose changelog has no new version publishes nothing. Such a
PR needs the `no-release` label to pass the Release version check.

**Never push a version tag by hand.** OpenUPM tracks this repository in Git
mode and builds a version from any matching tag it finds, whether or not a
workflow ran. Tags are created only by `release.yml`.

### Hotfixes

Branch `hotfix/<name>` off `main`, run `node scripts~/set-version.mjs X.Y.Z`
for the patch version, and open the PR into `main` (merge commit, as above).
After it publishes, open a PR from `main` into `develop` and squash it, so the
fix is on `develop` too.

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
stable-only, and the release workflow only publishes stable `X.Y.Z` versions
from `main`.

1. On a branch off `develop`, set the candidate version and merge it into
   `develop`:

   ```bash
   node scripts~/set-version.mjs 1.7.0-rc.1
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
   https://github.com/appstack-tech/appstack-unity-sdk.git#develop
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

4. Do not tag a candidate. Before the release PR, restore both native pins to
   released versions:

   - Android — set `Editor/AppstackDependencies.xml` back to the released
     `tech.appstack.android-sdk:appstack-android-sdk:X.Y.Z` and drop the
     snapshot repository.
   - iOS — set `PackageVersion` in `Editor/AppstackIOSPostProcessBuild.cs` back
     to the released version.
   - Set the package version to the stable value with
     `node scripts~/set-version.mjs X.Y.Z`, then update the validation fixtures
     and public docs (see step 1 of the release above) and commit. The Release
     version check rejects a candidate pin or a `-SNAPSHOT` coordinate.

## After a release

1. Confirm the Release workflow succeeds. It creates the GitHub Release,
   triggers OpenUPM, and waits for the registry version to become installable.
2. Confirm the release archive passes a clean-project import check and the
   GitHub release notes carry the version's changelog section above the install
   instructions.
3. Confirm OpenUPM reports the published version as signed. The publish step
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
