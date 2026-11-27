// Ticket references in a plan (and its research) that do not match its Tickets table.
import { parseTickets } from './plan.mjs';

const KEY = /\bT\d+\b/g;

const cells = (row) => row.replace(/^\s*\|/, '').replace(/\|\s*$/, '').split(/(?<!\\)\|/).map((c) => c.trim());

/** The keys in the Ticket column of the table under `## Test strategy`. → string[] */
function strategyKeys(markdown) {
  const section = markdown.split(/^##\s+Test strategy\s*$/m)[1];
  if (!section) return [];
  const lines = section.split(/^##\s/m)[0].split('\n').filter((l) => l.trim().startsWith('|'));
  if (lines.length < 3) return [];
  const column = cells(lines[0]).findIndex((h) => /^(tickets?|keys?)$/i.test(h));
  if (column < 0) return [];
  return lines.slice(2).flatMap((l) => cells(l)[column]?.match(KEY) ?? []);
}

/** Each `T<n>` cited in the files, with where: key → ["plan.md:12", …]. A key's own Tickets row is no citation. */
function citations(files) {
  const found = new Map();
  for (const [name, text] of Object.entries(files)) {
    text.split('\n').forEach((line, i) => {
      const own = line.match(/^\s*\|\s*(T\d+)\s*\|/)?.[1];
      for (const key of new Set(line.match(KEY) ?? [])) {
        if (key === own) continue;
        if (!found.has(key)) found.set(key, []);
        found.get(key).push(`${name}:${i + 1}`);
      }
    });
  }
  return found;
}

const titles = (markdown) => new Map(parseTickets(markdown).map((t) => [t.key, t.title]));

/**
 * Problems: a key in the Test strategy table that is not in the Tickets table; a key of the base
 * version that is gone (keys are never renumbered after the merge). Notes: a `T<n>` in plan.md or
 * research.md that is not a key (it may be another plan's); a key whose title changed since the
 * previous commit or the base version, with each line that cites it. Code spans are read too.
 * `research`, `previous` and `base` are the texts, or undefined when there is none.
 * → { problems: string[], notes: string[] }
 */
export function planRefs(markdown, { research, previous, base } = {}) {
  const head = titles(markdown);
  const problems = [];
  const unknown = new Set();
  for (const key of new Set(strategyKeys(markdown))) {
    if (!head.has(key)) { problems.push(`${key}: in the Test strategy table but not in the Tickets table`); unknown.add(key); }
  }
  const baseTitles = base === undefined ? new Map() : titles(base);
  for (const key of baseTitles.keys()) {
    if (!head.has(key)) problems.push(`${key}: in the Tickets table of the base version but gone (merged keys are never renumbered)`);
  }

  const notes = [];
  const files = research === undefined ? { 'plan.md': markdown } : { 'plan.md': markdown, 'research.md': research };
  const cited = citations(files);
  for (const [key, where] of cited) {
    if (!head.has(key) && !unknown.has(key)) notes.push(`${key}: not a key of this plan; cited at ${where.join(', ')}`);
  }
  const previousTitles = previous === undefined ? new Map() : titles(previous);
  for (const [key, title] of head) {
    const [since, old] = previousTitles.has(key) && previousTitles.get(key) !== title ? ['the previous commit', previousTitles.get(key)]
      : baseTitles.has(key) && baseTitles.get(key) !== title ? ['the base version', baseTitles.get(key)] : [];
    if (!since) continue;
    const where = cited.get(key) ?? [];
    notes.push(`${key}: title changed since ${since} ("${old}" → "${title}"); cited at ${where.length ? where.join(', ') : 'no line'}`);
  }
  return { problems, notes };
}
