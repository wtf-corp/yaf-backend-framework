// Compares what a pull request changes in the approved files with what the merged plan declared.
import { matchesAny } from './lib.mjs';

/** Lines of an approved file that carry no information. */
function meaningful(line) {
  const t = line.trim();
  return t !== '' && !t.startsWith('#') && t !== '#nullable enable';
}

function entryMatches(line, entry) {
  const l = line.trim();
  if (entry.endsWith('*')) return l.startsWith(entry.slice(0, -1));
  // Exact line, or the declared member followed by its signature (`Type.Member(…) -> T`, `Type.Prop.get -> T`).
  return l === entry || (l.startsWith(entry) && /^[\s(.<]/.test(l.slice(entry.length)));
}

/**
 * diff: Map<file, { added, removed }> (lib.parseDiff). → { category: { added: string[], removed: string[] } }
 * Categories come from policy.delta.sources.
 */
export function actualDelta(diff, policy) {
  const result = {};
  for (const [category, globs] of Object.entries(policy.delta.sources)) {
    const added = [];
    const removed = [];
    for (const [file, change] of diff) {
      if (!matchesAny(file, globs)) continue;
      const a = change.added.filter(meaningful).map((s) => s.trim());
      const r = change.removed.filter(meaningful).map((s) => s.trim());
      // A line that only moved is neither added nor removed.
      added.push(...a.filter((l) => !r.includes(l)));
      removed.push(...r.filter((l) => !a.includes(l)));
    }
    result[category] = { added, removed };
  }
  return result;
}

export function isEmptyDelta(actual) {
  return Object.values(actual).every((c) => c.added.length === 0 && c.removed.length === 0);
}

/**
 * declared: plan.parseDelta result (or null when the plan has no block).
 * A pull request implements one ticket, so its delta must be a SUBSET of the plan's declaration:
 * every added line needs a declared entry. A removed line is never covered (it is a breaking change).
 * → { undeclared: [{ category, line }], removed: [{ category, line }] }
 */
export function compareDelta(actual, declared) {
  const undeclared = [];
  const removed = [];
  for (const [category, change] of Object.entries(actual)) {
    const entries = declared?.[category] ?? [];
    for (const line of change.added) if (!entries.some((e) => entryMatches(line, e))) undeclared.push({ category, line });
    for (const line of change.removed) removed.push({ category, line });
  }
  return { undeclared, removed };
}

/**
 * The verdict of check `factory/delta` for a tier.
 * low: no delta at all. medium: delta is declared. high: reports only.
 */
export function deltaVerdict(tier, actual, comparison) {
  const empty = isEmptyDelta(actual);
  const clean = comparison.undeclared.length === 0 && comparison.removed.length === 0;
  if (tier === 'high') return { pass: true, title: empty ? 'no delta' : clean ? 'delta declared by the plan (tier high: informative)' : 'delta not covered by the plan (tier high: a human decides)' };
  if (tier === 'low') return { pass: empty, title: empty ? 'no delta in the approved files' : 'tier low must not change public API, error codes, contracts, configuration keys or telemetry names' };
  return { pass: clean, title: clean ? (empty ? 'no delta' : 'delta equals what the plan declared') : `${comparison.undeclared.length} undeclared, ${comparison.removed.length} removed` };
}
