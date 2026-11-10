import { readFileSync } from 'node:fs';

function readInput() {
  const text = readFileSync(0, 'utf8').replace(/^\uFEFF/, '').trim();
  return text ? JSON.parse(text) : {};
}

function deniedRule(command) {
  const text = command.toLowerCase().replace(/[\s`]+/g, ' ').trim();

  if (/\bgit push\b/.test(text) && /(?:^|\s)--force(?![\w-])/.test(text)) {
    return 'git push --force';
  }

  if (/\brm -rf\b/.test(text)) {
    return 'rm -rf';
  }

  if (
    /\bremove-item\b/.test(text) &&
    /(?:^|\s)-recurse\b/.test(text) &&
    /(?:^|\s)-force\b/.test(text)
  ) {
    return 'Remove-Item with -Recurse and -Force';
  }

  if (/\bdocker system prune\b/.test(text)) {
    return 'docker system prune';
  }

  if (/\bdocker volume rm\b/.test(text)) {
    return 'docker volume rm';
  }

  return null;
}

function finish(payload, code) {
  process.stdout.write(`${JSON.stringify(payload)}\n`);
  process.exitCode = code;
}

try {
  const input = readInput();
  const command = typeof input.command === 'string' ? input.command : '';
  const rule = deniedRule(command);

  if (rule) {
    finish(
      {
        permission: 'deny',
        user_message: `Blocked ${rule}.`,
        agent_message: rule,
      },
      2,
    );
  } else {
    finish({ permission: 'allow' }, 0);
  }
} catch {
  finish({ permission: 'allow' }, 0);
}
