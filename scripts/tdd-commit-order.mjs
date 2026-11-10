import { execFileSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

const domainProdPattern = /^src\/LivePoll\.Domain\/.*\.cs$/;
const domainTestPattern = /^tests\/LivePoll\.Domain\.Tests\//;
const appProdPattern = /^src\/LivePoll\.Application\/.*\.cs$/;
const appTestPattern = /^tests\/LivePoll\.Application\.Tests\//;

function classifyFiles(files) {
  let domainProd = false;
  let domainTest = false;
  let appProd = false;
  let appTest = false;
  for (const file of files) {
    if (domainProdPattern.test(file)) {
      domainProd = true;
    }
    if (domainTestPattern.test(file)) {
      domainTest = true;
    }
    if (appProdPattern.test(file)) {
      appProd = true;
    }
    if (appTestPattern.test(file)) {
      appTest = true;
    }
  }

  return { domainProd, domainTest, appProd, appTest };
}

/**
 * @param {Iterable<{ domainProd: boolean, domainTest: boolean, appProd: boolean, appTest: boolean }>} commits
 * @returns {string | undefined} violation message
 */
export function findTddViolation(commits) {
  let pendingDomainTest = false;
  let pendingAppTest = false;

  for (const commit of commits) {
    const { domainProd, domainTest, appProd, appTest } = commit;

    if (domainProd && domainTest) {
      return 'Domain production and test changes must not land in the same commit.';
    }

    if (appProd && appTest) {
      return 'Application production and test changes must not land in the same commit.';
    }

    if (domainTest && !domainProd) {
      pendingDomainTest = true;
    }

    if (appTest && !appProd) {
      pendingAppTest = true;
    }

    if (domainProd && !pendingDomainTest) {
      return 'A Domain production change landed before a prior commit added the failing test.';
    }

    if (appProd && !pendingAppTest) {
      return 'An Application production change landed before a prior commit added the failing test.';
    }

    if (domainProd) {
      pendingDomainTest = false;
    }

    if (appProd) {
      pendingAppTest = false;
    }
  }

  return undefined;
}

function listCommitFileChanges(repositoryRoot) {
  const text = execFileSync(
    'git',
    ['log', '--reverse', '--format=%H', '--name-only'],
    { cwd: repositoryRoot, encoding: 'utf8', maxBuffer: 10 * 1024 * 1024 },
  );
  const commits = [];
  let current = null;
  for (const line of text.split(/\r?\n/)) {
    if (line.length === 0) {
      continue;
    }

    if (/^[0-9a-f]{40}$/i.test(line)) {
      if (current) {
        commits.push(current);
      }

      current = { hash: line, files: [] };
      continue;
    }

    if (current) {
      current.files.push(line.replace(/\\/g, '/'));
    }
  }

  if (current) {
    commits.push(current);
  }

  return commits;
}

function checkRepository(repositoryRoot) {
  const commits = listCommitFileChanges(repositoryRoot);
  const violation = findTddViolation(commits.map((commit) => classifyFiles(commit.files)));
  if (violation) {
    console.error(violation);
    return 1;
  }

  return 0;
}

function runSelfTest() {
  const ok = findTddViolation([
    { domainProd: false, domainTest: true, appProd: false, appTest: false },
    { domainProd: true, domainTest: false, appProd: false, appTest: false },
  ]);
  if (ok) {
    console.error(`self-test expected valid order to pass: ${ok}`);
    return 1;
  }

  const sameCommit = findTddViolation([
    { domainProd: true, domainTest: true, appProd: false, appTest: false },
  ]);
  if (!sameCommit?.includes('same commit')) {
    console.error('self-test expected same-commit Domain change to fail.');
    return 1;
  }

  const prodFirst = findTddViolation([
    { domainProd: true, domainTest: false, appProd: false, appTest: false },
    { domainProd: false, domainTest: true, appProd: false, appTest: false },
  ]);
  if (!prodFirst?.includes('before a prior commit')) {
    console.error('self-test expected production-before-test to fail.');
    return 1;
  }

  return 0;
}

const mode = process.argv[2];
if (mode === '--self-test') {
  process.exit(runSelfTest());
}

process.exit(checkRepository(root));
