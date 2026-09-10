#!/usr/bin/env node

// Run with `node --test scripts~/`.

import assert from 'node:assert/strict';
import test from 'node:test';

import { changelogSection } from './release-notes.mjs';

const changelog = `# Changelog

All notable changes to this project are documented in this file.

## [Unreleased]

## [1.2.10] - 2026-10-01

### Fixed

- Tenth patch entry.

## [1.3.0-beta.1] - 2026-09-10

### Added

- Prerelease entry.

## [1.3.0] - 2026-09-03

### Changed

- Third minor entry.

### Fixed

- Third minor fix.

## [1.2.1] - 2026-08-17

### Fixed

- First patch entry.
`;

test('reads the section for the requested version', () => {
  assert.equal(
    changelogSection(changelog, '1.3.0'),
    '### Changed\n\n- Third minor entry.\n\n### Fixed\n\n- Third minor fix.',
  );
});

test('keeps `###` subsections and stops at the next `##`', () => {
  const section = changelogSection(changelog, '1.2.10');
  assert.equal(section, '### Fixed\n\n- Tenth patch entry.');
  assert.doesNotMatch(section, /^## /m);
});

// 1.2.1 is a prefix of 1.2.10, and Keep a Changelog puts the newer section
// first, so a loose heading match published 1.2.10's notes under the 1.2.1 tag.
test('does not match a version that only prefixes a longer one', () => {
  assert.equal(changelogSection(changelog, '1.2.1'), '### Fixed\n\n- First patch entry.');
});

// Same prefix hole, reached through the prerelease suffix the CLI accepts.
test('does not match a prerelease heading for the release version', () => {
  assert.doesNotMatch(changelogSection(changelog, '1.3.0'), /Prerelease entry/);
  assert.equal(changelogSection(changelog, '1.3.0-beta.1'), '### Added\n\n- Prerelease entry.');
});

test('reads an unbracketed heading', () => {
  assert.equal(changelogSection('## 1.4.0 - 2026-09-08\n\n- Bare heading entry.\n', '1.4.0'), '- Bare heading entry.');
  assert.equal(changelogSection('## 1.4.0\n\n- No date.\n', '1.4.0'), '- No date.');
});

test('does not match an unbracketed heading that only prefixes a longer version', () => {
  assert.equal(changelogSection('## 1.2.10 - 2026-10-01\n\n- Tenth patch entry.\n', '1.2.1'), '');
});

test('reads the last section in the file', () => {
  assert.equal(changelogSection(changelog, '1.2.1'), '### Fixed\n\n- First patch entry.');
});

test('returns nothing when the version has no section', () => {
  assert.equal(changelogSection(changelog, '9.9.9'), '');
});
