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

const spans = (text) => [...(text ?? '').matchAll(/`([^`]+)`/g)].map((m) => m[1]);
const escape = (s) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

/**
 * The names a ticket creates: the type of each new `.cs` file in its touches (file name without `{…}`
 * and extension), each "`X` does not exist" of its failing test (without `<…>` and `()`), and
 * `ADR-NNNN` for each new `adr/NNNN-…` file. A path that exists on the base creates nothing.
 */
function creates(ticket, exists) {
  const names = [];
  for (const p of ticket.touches.map(pathOf)) {
    if (isGlob(p) || exists(p)) continue;
    const adr = p.match(/(?:^|\/)adr\/(\d{4})-[^/]+\.md$/);
    if (adr) names.push(`ADR-${adr[1]}`);
    else if (p.endsWith('.cs')) names.push(p.split('/').pop().slice(0, -3).replace(/\{[^}]*\}/g, ''));
  }
  for (const m of (ticket.failingTest ?? '').matchAll(/`([^`]+)` does not exist/g)) {
    names.push(m[1].replace(/<[^>]*>/g, '').replace(/\(\)$/, ''));
  }
  return names.filter(Boolean);
}

/**
 * Tickets that name, in a code span of their Title, Failing test first or Verify cell, a name another
 * ticket creates (`creates`; the first creator counts) while that ticket is not among their ancestors.
 * A later creator is never an ancestor. A `docs` ticket gives a note, any other a problem. The
 * Requirements cell is not read: it cites the specification, it does not use code.
 * `exists(path)` tells whether a repository-relative path is on the base.
 * → { problems: string[], notes: string[] }
 */
export function uses(tickets, { exists }) {
  const creator = new Map();
  for (const t of tickets) for (const name of creates(t, exists)) if (!creator.has(name)) creator.set(name, t.key);
  const of = ancestors(tickets);
  const problems = [], notes = [];
  for (const t of tickets) {
    const text = [t.title, t.failingTest, t.verify].flatMap(spans).join('\n');
    for (const [name, by] of creator) {
      if (by === t.key || of.get(t.key)?.has(by)) continue;
      if (!new RegExp(`(?<![\\w])${escape(name)}(?![\\w])`).test(text)) continue;
      const later = tickets.findIndex((x) => x.key === by) > tickets.indexOf(t) ? ' later' : '';
      (t.type === 'docs' ? notes : problems).push(`${t.key}: names \`${name}\`, which ${by} creates${later}, but does not depend on ${by}`);
    }
  }
  return { problems, notes };
}
