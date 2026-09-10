#!/usr/bin/env node

// Composes a GitHub Release body for one version: the matching CHANGELOG.md
// section first, then the install instructions.
//
// The notes have to lead, because a release page is read on its own. Linking
// out to CHANGELOG.md left every Unity release showing install steps and
// nothing else, unlike the other Appstack SDKs. The install steps stay below
// them; the docs changelog generator drops those sections by heading.

import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const rootPath = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const changelogPath = path.join(rootPath, 'CHANGELOG.md');
const defaultRepository = 'appstack-tech/appstack-unity-sdk';
const semverPattern =
  /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-(?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*))*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$/;
const repositoryPattern = /^[0-9A-Za-z._-]+\/[0-9A-Za-z._-]+$/;

function fail(message) {
  console.error(`Release notes error: ${message}`);
  process.exit(1);
}

// Reads one `## [1.3.0] - 2026-09-03` section out of the Keep a Changelog file.
// A `###` subheading cannot end the section, so the Added/Changed/Fixed
// subsections come through verbatim and need no re-leveling.
//
// The version has to match a whole heading token, not a prefix of one: a
// bracketed version closes its bracket, and a bare one ends the word. Matching
// loosely published 1.2.10's notes under the 1.2.1 tag, because the newest
// section sits above the older one and won the match.
export function changelogSection(changelog, version) {
  const escapedVersion = version.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const sectionPattern = new RegExp(
    `^## (?:\\[${escapedVersion}\\]|${escapedVersion}(?=\\s|$))[^\\n]*\\n` +
      `([\\s\\S]*?)(?=^## |$(?![\\s\\S]))`,
    'm',
  );

  const match = changelog.match(sectionPattern);
  return match ? match[1].trim() : '';
}

function releaseNotes(version, repository, section) {
  const tagUrl = `https://github.com/${repository}/blob/${version}`;

  return `## Appstack Unity SDK ${version}

${section}

### Package authenticity

The OpenUPM artifact is signed by Appstack through Unity's package-signing service. Unity 6.3 and newer can verify its publisher and integrity during installation.

### Install through Unity Package Manager

1. Download and extract \`appstack-unity-sdk-${version}.zip\`.
2. In Unity, open **Window → Package Manager**.
3. Select **+ → Add package from disk** and choose the extracted root \`package.json\`.
4. Complete the [iOS](${tagUrl}/Documentation~/iOS.md) and [Android](${tagUrl}/Documentation~/Android.md) setup.
`;
}

// The extractor is exported for scripts~/release-notes.test.mjs, so the CLI
// runs only when this file is the entry point.
function main() {
  const args = process.argv.slice(2);
  const repositoryIndex = args.indexOf('--repo');
  let repository = defaultRepository;

  if (repositoryIndex !== -1) {
    repository = args[repositoryIndex + 1];
    args.splice(repositoryIndex, 2);

    if (repository === undefined || !repositoryPattern.test(repository)) {
      fail('--repo must be an "owner/name" repository slug.');
    }
  }

  if (args.length !== 1) {
    fail('usage: node scripts~/release-notes.mjs <version> [--repo owner/name]');
  }

  const version = args[0];
  if (!semverPattern.test(version)) {
    fail(`version must be a semantic version, received "${version}".`);
  }

  let changelog;
  try {
    changelog = fs.readFileSync(changelogPath, 'utf8');
  } catch (error) {
    fail(`unable to read CHANGELOG.md: ${error.message}`);
  }

  const section = changelogSection(changelog, version);
  if (!section) {
    fail(
      `CHANGELOG.md has no "## [${version}]" section. Move the release's entries ` +
        'out of Unreleased and commit the result before tagging.',
    );
  }

  process.stdout.write(releaseNotes(version, repository, section));
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main();
}
