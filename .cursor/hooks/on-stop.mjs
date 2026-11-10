import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const learn =
  'In max 3 lines, list decisions I made in this chat that are not in project files yet, then suggest /learn. If none, reply only: Nothing to learn.';

function readInput() {
  const text = readFileSync(0, 'utf8').replace(/^\uFEFF/, '').trim();
  return text ? JSON.parse(text) : {};
}

function phaseIsGreen() {
  try {
    return readFileSync(path.join(root, '.cursor', 'tdd-phase'), 'utf8').trim() === 'green';
  } catch {
    return false;
  }
}

function testsDifferFromHead() {
  const diff = spawnSync('git', ['diff', '--quiet', 'HEAD', '--', 'tests/'], {
    cwd: root,
    stdio: 'ignore',
    windowsHide: true,
  });
  return diff.status !== 0;
}

function finish(payload) {
  process.stdout.write(`${JSON.stringify(payload)}\n`);
  process.exitCode = 0;
}

try {
  const input = readInput();

  if (Number(input.loop_count) > 0) {
    finish({});
  } else if (phaseIsGreen() && testsDifferFromHead()) {
    finish({
      followup_message:
        'TDD phase is green and tests/ has changes. Revert all changes under tests/.',
    });
  } else {
    finish({ followup_message: learn });
  }
} catch {
  finish({});
}
