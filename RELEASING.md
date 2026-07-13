# Appstack Unity SDK release guide

This document is for release maintainers. Integrators should use
[README.md](README.md).

## Current release blocker

The existing `.github/workflows/release.yml` still packages the removed
`Assets/AppstackSDK` layout. It must be updated for the root-level UPM package
before creating a release tag.

The corrected workflow must:

- Read the version from the root `package.json`.
- Fail when the Git tag and package version differ.
- Package the root UPM contents rather than creating an `Assets/` tree.
- Include `LICENSE.md`, public documentation, runtime, editor, tests, samples,
  and all corresponding `.meta` files.
- Exclude repository-only files and generated output.
- Describe manual installation through Unity Package Manager rather than
  instructing users to unzip into `Assets/AppstackSDK`.

## Before tagging

1. Decide whether the release will use the current native RC dependencies or
   stable native versions.
2. Update native dependency pins in `Editor/AppstackDependencies.xml` and the
   public platform documentation together.
3. Set the release version in the root `package.json`.
4. Keep the native wrapper identifier in sync with the package version. For
   version `1.0.0`, both bridges must report `unity-1.0.0`.
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

1. Commit the version and changelog changes.
2. Create a semantic version tag matching `package.json`, for example `1.0.0`.
3. Push the commit and tag.
4. Confirm the GitHub Actions release job succeeds and its archive passes a
   clean-project import check.
5. Confirm the GitHub release notes link to the versioned changelog.

OpenUPM builds from the same Git tag after one-time package registration. The
submission metadata is in `.openupm/package-metadata.yml`. Register
`com.appstack.unity-sdk` through the OpenUPM add form or its package registry
repository, then verify the published package metadata and installation.
