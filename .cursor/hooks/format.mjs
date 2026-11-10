import { spawnSync } from 'node:child_process';
import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');

function readInput() {
  const text = readFileSync(0, 'utf8').replace(/^\uFEFF/, '').trim();
  return text ? JSON.parse(text) : {};
}

function hasSolution(directory) {
  return readdirSync(directory).some((name) => {
    const lower = name.toLowerCase();
    return lower.endsWith('.sln') || lower.endsWith('.slnx');
  });
}

function includePath(filePath) {
  const absolute = path.resolve(root, filePath);
  const relative = path.relative(root, absolute);
  const outside = relative === '..' || relative.startsWith(`..${path.sep}`) || path.isAbsolute(relative);
  return outside ? absolute : relative;
}

try {
  const input = readInput();
  const filePath = typeof input.file_path === 'string' ? input.file_path : '';

  if (path.extname(filePath).toLowerCase() === '.cs' && hasSolution(root)) {
    spawnSync(
      'dotnet',
      ['format', '--include', includePath(filePath), '--verbosity', 'quiet'],
      {
        cwd: root,
        stdio: 'ignore',
        windowsHide: true,
      },
    );
  }
} catch {
  // The edit already happened; formatting must not fail the hook.
}

process.stdout.write('{}\n');
process.exitCode = 0;
