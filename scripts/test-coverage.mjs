import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, readdirSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const assemblies = ['LivePoll.Domain', 'LivePoll.Application'];
const minimumPercent = 80;

function assemblyName(packageName) {
  const fileName = packageName.split(/[/\\]/).pop() ?? packageName;
  return fileName.replace(/\.(dll|exe)$/i, '');
}

function decodeXml(value) {
  return value
    .replaceAll('&amp;', '&')
    .replaceAll('&quot;', '"')
    .replaceAll('&apos;', "'")
    .replaceAll('&lt;', '<')
    .replaceAll('&gt;', '>');
}

function attribute(text, name) {
  const match = new RegExp(`${name}\\s*=\\s*(?:"([^"]*)"|'([^']*)')`, 'i').exec(text);
  if (!match) {
    return undefined;
  }

  return decodeXml(match[1] ?? match[2]);
}

function findReports(directory, found) {
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      findReports(fullPath, found);
      continue;
    }

    if (entry.name === 'coverage.cobertura.xml') {
      found.push(fullPath);
    }
  }
}

export function readCobertura(file, linesByAssembly, seen) {
  const xml = readFileSync(file, 'utf8');
  const tokens = xml.matchAll(/<(\/?)(package|class|line)\b([^>]*)\/?>/g);
  let currentAssembly = null;
  let fileName = null;
  for (const token of tokens) {
    const closing = token[1] === '/';
    const kind = token[2];
    const attrs = token[3] ?? '';
    if (kind === 'package') {
      if (closing) {
        currentAssembly = null;
        fileName = null;
        continue;
      }

      currentAssembly = assemblyName(attribute(attrs, 'name') ?? '');
      if (linesByAssembly.has(currentAssembly)) {
        seen.add(currentAssembly);
      }

      fileName = null;
      continue;
    }

    if (!currentAssembly || !linesByAssembly.has(currentAssembly)) {
      continue;
    }

    if (kind === 'class') {
      fileName = closing ? null : (attribute(attrs, 'filename') ?? '');
      continue;
    }

    if (closing || fileName === null) {
      continue;
    }

    const number = attribute(attrs, 'number');
    if (!number) {
      continue;
    }

    const hits = Number(attribute(attrs, 'hits') ?? '0');
    const lines = linesByAssembly.get(currentAssembly);
    const key = `${fileName}\0${number}`;
    lines.set(key, Math.max(lines.get(key) ?? 0, Number.isFinite(hits) ? hits : 0));
  }
}

/**
 * @param {string[]} reportPaths
 * @returns {{ exitCode: number, percents: Map<string, number> }}
 */
export function evaluateCoverageReports(reportPaths) {
  const linesByAssembly = new Map(assemblies.map((name) => [name, new Map()]));
  const seen = new Set();
  for (const report of reportPaths) {
    readCobertura(report, linesByAssembly, seen);
  }

  const percents = new Map();
  let exitCode = 0;
  for (const name of assemblies) {
    if (!seen.has(name)) {
      console.error(`${name} line coverage is missing from the Coverlet report.`);
      exitCode = 1;
      continue;
    }

    const lines = linesByAssembly.get(name);
    const total = lines.size;
    if (total === 0) {
      console.error(`${name} has no coverable lines.`);
      exitCode = 1;
      continue;
    }

    let covered = 0;
    for (const hits of lines.values()) {
      if (hits > 0) {
        covered += 1;
      }
    }

    const percent = (covered * 100) / total;
    percents.set(name, percent);
    console.log(`${name} line coverage: ${percent.toFixed(2)}% (${covered}/${total})`);
    if (covered * 100 < total * minimumPercent) {
      exitCode = 1;
    }
  }

  return { exitCode, percents };
}

function runSelfTest() {
  const underPath = path.join(root, 'scripts', 'fixtures', 'coverage-under-threshold.cobertura.xml');
  const under = evaluateCoverageReports([underPath]);
  if (under.exitCode === 0) {
    console.error('self-test expected under-threshold fixture to fail the gate.');
    return 1;
  }

  const domainPercent = under.percents.get('LivePoll.Domain') ?? 0;
  const applicationPercent = under.percents.get('LivePoll.Application') ?? 0;
  if (domainPercent >= minimumPercent) {
    console.error('self-test expected Domain coverage under 80% in the fixture.');
    return 1;
  }

  if (applicationPercent < minimumPercent) {
    console.error('self-test expected Application fixture to stay above the gate on its own.');
    return 1;
  }

  return 0;
}

const coverageProjects = [
  path.join(root, 'tests/LivePoll.Domain.Tests/LivePoll.Domain.Tests.csproj'),
  path.join(root, 'tests/LivePoll.Application.Tests/LivePoll.Application.Tests.csproj'),
];

function runCoverletOnProject(projectPath, resultsDir) {
  return spawnSync(
    'dotnet',
    [
      'test',
      projectPath,
      '--collect:XPlat Code Coverage',
      '--results-directory',
      resultsDir,
      '--',
      'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura',
      'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include=[LivePoll.Domain]*,[LivePoll.Application]*',
    ],
    { cwd: root, stdio: 'inherit', windowsHide: true },
  );
}

function runDotnetCoverageGate() {
  const resultsDir = mkdtempSync(path.join(tmpdir(), 'livepoll-coverage-'));
  let exitCode = 0;
  try {
    for (const projectPath of coverageProjects) {
      const result = runCoverletOnProject(projectPath, resultsDir);
      if (result.error) {
        console.error(result.error.message);
        exitCode = 1;
        break;
      }

      if (result.status !== 0) {
        exitCode = result.status ?? 1;
        break;
      }
    }

    if (exitCode === 0) {
      const reports = [];
      findReports(resultsDir, reports);
      if (reports.length === 0) {
        console.error('Coverlet did not produce a coverage report.');
        exitCode = 1;
      } else {
        exitCode = evaluateCoverageReports(reports).exitCode;
      }
    }
  } finally {
    rmSync(resultsDir, { recursive: true, force: true });
  }

  return exitCode;
}

if (process.argv[2] === '--self-test') {
  process.exit(runSelfTest());
}

process.exit(runDotnetCoverageGate());
