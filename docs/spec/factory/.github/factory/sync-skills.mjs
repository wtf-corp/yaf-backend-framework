// Claude Code reads skills from .claude/skills/, the repository keeps them tool-neutral in .agents/skills/.
// This mirrors one into the other.
//   node .github/factory/sync-skills.mjs            write the mirror
//   node .github/factory/sync-skills.mjs --check    exit 1 when the mirror differs
//   --root <dir>                                    repository root to work on (default: current directory)
import { readdirSync, readFileSync, writeFileSync, mkdirSync, rmSync, existsSync, statSync } from 'node:fs';
import path from 'node:path';

export function listFiles(dir, base = dir) {
  if (!existsSync(dir)) return [];
  return readdirSync(dir, { withFileTypes: true }).flatMap((e) => {
    const full = path.join(dir, e.name);
    return e.isDirectory() ? listFiles(full, base) : [path.relative(base, full).replaceAll('\\', '/')];
  }).sort();
}

/** → { missing, changed, extra } relative paths */
export function compare(sourceDir, mirrorDir) {
  const source = listFiles(sourceDir);
  const mirror = listFiles(mirrorDir);
  const missing = source.filter((f) => !mirror.includes(f));
  const extra = mirror.filter((f) => !source.includes(f));
  const changed = source.filter((f) => mirror.includes(f) && !readFileSync(path.join(sourceDir, f)).equals(readFileSync(path.join(mirrorDir, f))));
  return { missing, changed, extra };
}

if (process.argv[1]?.endsWith('sync-skills.mjs')) {
  const args = process.argv.slice(2);
  const root = args.includes('--root') ? args[args.indexOf('--root') + 1] : '.';
  const source = path.join(root, '.agents', 'skills');
  const mirror = path.join(root, '.claude', 'skills');
  if (!existsSync(source) || !statSync(source).isDirectory()) { console.log('.agents/skills not found: nothing to mirror'); process.exit(0); }
  const diff = compare(source, mirror);
  const total = diff.missing.length + diff.changed.length + diff.extra.length;
  if (args.includes('--check')) {
    if (total === 0) { console.log('.claude/skills mirrors .agents/skills'); process.exit(0); }
    for (const f of diff.missing) console.error(`missing in .claude/skills: ${f}`);
    for (const f of diff.changed) console.error(`differs: ${f}`);
    for (const f of diff.extra) console.error(`only in .claude/skills: ${f}`);
    console.error('Run: node .github/factory/sync-skills.mjs');
    process.exit(1);
  }
  for (const f of [...diff.missing, ...diff.changed]) {
    mkdirSync(path.dirname(path.join(mirror, f)), { recursive: true });
    writeFileSync(path.join(mirror, f), readFileSync(path.join(source, f)));
  }
  for (const f of diff.extra) rmSync(path.join(mirror, f));
  console.log(`mirrored ${diff.missing.length + diff.changed.length} file(s), removed ${diff.extra.length}`);
}
