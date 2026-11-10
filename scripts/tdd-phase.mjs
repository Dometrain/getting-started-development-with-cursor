import { writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const phases = new Set(['red', 'green', 'refactor', 'off']);
const phase = process.argv[2];

if (!phases.has(phase) || process.argv.length !== 3) {
  console.error('Usage: node scripts/tdd-phase.mjs <red|green|refactor|off>');
  process.exit(1);
}

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
writeFileSync(path.join(root, '.cursor', 'tdd-phase'), `${phase}\n`);
