import { spawnSync } from 'node:child_process';
import { readdirSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const skippedDirectories = new Set([
  '.git',
  'artifacts',
  'bin',
  'coverage',
  'dist',
  'node_modules',
  'obj',
  'testresults',
]);

function findProjects(directory, projects) {
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    if (entry.isDirectory()) {
      if (!skippedDirectories.has(entry.name.toLowerCase())) {
        findProjects(path.join(directory, entry.name), projects);
      }
      continue;
    }

    if (entry.name.endsWith('.csproj')) {
      projects.push(path.join(directory, entry.name));
    }
  }
}

function parseReport(text) {
  const start = text.indexOf('{');
  const end = text.lastIndexOf('}');
  if (start < 0 || end < start) {
    throw new Error('dotnet list package did not return JSON.');
  }

  return JSON.parse(text.slice(start, end + 1));
}

function parseNuGetVersion(value) {
  const withoutMetadata = String(value).split('+', 1)[0];
  const dash = withoutMetadata.indexOf('-');
  const numeric = dash === -1 ? withoutMetadata : withoutMetadata.slice(0, dash);
  const prerelease = dash === -1
    ? null
    : withoutMetadata.slice(dash + 1).split('.').filter((part) => part.length > 0);
  const numbers = numeric.split('.').filter((part) => part.length > 0).map((part) => {
    if (!/^\d+$/.test(part)) {
      return 0;
    }

    const parsed = Number(part);
    return Number.isSafeInteger(parsed) ? parsed : 0;
  });
  return { numbers, prerelease };
}

function compareVersions(left, right) {
  const a = parseNuGetVersion(left);
  const b = parseNuGetVersion(right);
  const length = Math.max(a.numbers.length, b.numbers.length);
  for (let index = 0; index < length; index += 1) {
    const diff = (a.numbers[index] ?? 0) - (b.numbers[index] ?? 0);
    if (diff !== 0) {
      return diff;
    }
  }

  if (a.prerelease === null && b.prerelease === null) {
    return 0;
  }

  if (a.prerelease === null) {
    return 1;
  }

  if (b.prerelease === null) {
    return -1;
  }

  const count = Math.max(a.prerelease.length, b.prerelease.length);
  for (let index = 0; index < count; index += 1) {
    const leftPart = a.prerelease[index];
    const rightPart = b.prerelease[index];
    if (leftPart === undefined) {
      return -1;
    }

    if (rightPart === undefined) {
      return 1;
    }

    const leftNumeric = /^\d+$/.test(leftPart);
    const rightNumeric = /^\d+$/.test(rightPart);
    if (leftNumeric && rightNumeric) {
      const diff = Number(leftPart) - Number(rightPart);
      if (diff !== 0) {
        return diff;
      }

      continue;
    }

    if (leftNumeric !== rightNumeric) {
      return leftNumeric ? -1 : 1;
    }

    const leftText = leftPart.toLowerCase();
    const rightText = rightPart.toLowerCase();
    if (leftText < rightText) {
      return -1;
    }

    if (leftText > rightText) {
      return 1;
    }
  }

  return 0;
}

function listedPackages(report) {
  const found = [];
  for (const project of report.projects ?? []) {
    for (const framework of project.frameworks ?? []) {
      const packages = [
        ...(framework.topLevelPackages ?? []),
        ...(framework.transitivePackages ?? []),
      ];
      for (const entry of packages) {
        found.push({
          project: project.path ?? '',
          framework: framework.framework ?? '',
          id: entry.id,
          resolvedVersion: entry.resolvedVersion,
          latestVersion: entry.latestVersion,
        });
      }
    }
  }

  return found;
}

const projects = [];
findProjects(root, projects);
if (projects.length === 0) {
  console.log('No projects found.');
  process.exit(0);
}

const outdated = [];
for (const project of projects) {
  const result = spawnSync(
    'dotnet',
    ['list', project, 'package', '--outdated', '--format', 'json', '--output-version', '1'],
    {
      cwd: root,
      encoding: 'utf8',
      maxBuffer: 64 * 1024 * 1024,
      windowsHide: true,
    },
  );
  if (result.error) {
    console.error(result.error.message);
    process.exit(1);
  }

  if (result.status !== 0) {
    if (result.stdout) {
      console.error(result.stdout);
    }

    if (result.stderr) {
      console.error(result.stderr);
    }

    process.exit(result.status ?? 1);
  }

  let report;
  try {
    report = parseReport(result.stdout ?? '');
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    process.exit(1);
  }

  for (const entry of listedPackages(report)) {
    if (!entry.id || !entry.latestVersion || !entry.resolvedVersion) {
      continue;
    }

    if (compareVersions(entry.latestVersion, entry.resolvedVersion) > 0) {
      outdated.push(entry);
    }
  }
}

if (outdated.length === 0) {
  process.exit(0);
}

for (const entry of outdated) {
  const location = entry.framework ? `${entry.project} (${entry.framework})` : entry.project;
  console.error(`${location}: ${entry.id} ${entry.resolvedVersion} -> ${entry.latestVersion}`);
}

process.exit(1);
