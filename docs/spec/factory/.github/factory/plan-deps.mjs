// Checks of a plan's Tickets table that follow its dependency graph (plan 0015, idea 1).
// Input: the tickets of `parseTickets` (plan.mjs). Output: problems as strings, as `ticketProblems` gives them.
import path from 'node:path';
import { matchesAny } from './lib.mjs';

/**
 * Every ticket's ancestors through `Depends on`, transitively: Map key → Set of keys.
 * Unknown keys are kept as they are (`ticketProblems` reports them); a cycle stops the walk.
 */
export function ancestors(tickets) {
  const direct = new Map(tickets.map((t) => [t.key, t.dependsOn]));
  return new Map(tickets.map((t) => {
    const all = new Set();
    const queue = [...t.dependsOn];
    while (queue.length) {
      const d = queue.shift();
      if (all.has(d)) continue;
      all.add(d);
      queue.push(...(direct.get(d) ?? []));
    }
    return [t.key, all];
  }));
}

/** The path of a touches entry without a note after it: "`.editorconfig` (three sections)" → ".editorconfig". */
function pathOf(entry) {
  const span = entry.match(/^`([^`]+)`/);
  return (span ? span[1] : entry.split(/\s/)[0]).replaceAll('\\', '/').replace(/^\.\//, '');
}

const isGlob = (p) => /[*?]/.test(p);
const isFolder = (p) => p.endsWith('/') || (!isGlob(p) && path.posix.extname(p.split('/').pop()) === '');
const folder = (p) => (p.endsWith('/') ? p : `${p}/`);
const migrations = (p) => p.match(/^(.*?\/)?Migrations\//)?.[0] ?? null;

/** True when two touched paths can name the same file. */
function overlap(a, b) {
  if (a === b) return true;
  for (const [x, y] of [[a, b], [b, a]]) {
    if (isFolder(x) && y.startsWith(folder(x))) return true;
    if (isGlob(x) && !isGlob(y) && !isFolder(y) && matchesAny(y, [x])) return true;
  }
  const m = migrations(a);
  return m !== null && m === migrations(b);
}

/**
 * Pairs of tickets whose touches overlap while neither is an ancestor of the other: two writers
 * could then change one file at once. One problem per pair, naming the overlapping touches.
 * → string[]
 */
export function overlaps(tickets) {
  const of = ancestors(tickets);
  const problems = [];
  tickets.forEach((a, i) => {
    for (const b of tickets.slice(i + 1)) {
      if (of.get(a.key)?.has(b.key) || of.get(b.key)?.has(a.key)) continue;
      const shared = [];
      for (const x of a.touches.map(pathOf)) for (const y of b.touches.map(pathOf)) {
        if (overlap(x, y)) shared.push(x === y ? `\`${x}\`` : `\`${x}\` and \`${y}\``);
      }
      if (shared.length) problems.push(`${a.key} and ${b.key}: touches overlap with no dependency between them: ${shared.join(', ')}`);
    }
  });
  return problems;
}
