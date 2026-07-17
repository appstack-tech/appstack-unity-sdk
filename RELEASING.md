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
- Describes manual installation through Unity Package Manager.

## Before tagging

1. Confirm the release uses the intended stable Android and iOS native SDK
   versions.
2. Update both native dependency pins, their validation fixtures, and the
   public platform documentation together.
3. Run `node scripts~/set-version.mjs <version>`. This updates the root
   `package.json` and regenerates `Runtime/AppstackVersion.cs` together.
4. Run `node scripts~/set-version.mjs --check <version>`. The editor tests and
   release workflow run equivalent validation; both native bridges receive the
   resulting `unity-<version>` value from C#.
5. Move relevant entries from `Unreleased` to a versioned changelog section.
6. Confirm every package asset has a committed, unique `.meta` file.

## Validation

Complete the device-build matrix in [DEVELOPMENT.md](DEVELOPMENT.md), including:

- Unity Test Runner editor tests.
- Android development and minified release builds on a physical device.
- iOS physical-device build or archive from a fresh Unity export.
- Configure, events, ID/status calls, concurrent attribution callbacks, main
  thread delivery, and UTF-8 attribution values on both platforms.

Inspect the release archive before publishing. Adding it as a local package to a
clean Unity project must produce the same package contents and behavior as a
repository checkout.

## Publish

1. Commit the generated version files and changelog changes.
2. Create a semantic version tag matching `package.json`, for example `1.0.0`.
3. Push the commit and tag.
4. Confirm the GitHub Actions release job succeeds and its archive passes a
   clean-project import check.
5. Confirm the GitHub release notes link to the versioned changelog.

OpenUPM builds from the same Git tag after one-time package registration. The
submission metadata is in `.openupm/package-metadata.yml`. Register
`com.appstack.unity-sdk` through the OpenUPM add form or its package registry
repository, then verify the published package metadata and installation.
