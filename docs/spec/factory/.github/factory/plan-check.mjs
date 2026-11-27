// Every mechanical check of a plan in one call (plan 0015, idea 1): factory/plan-gate fails on its problems,
// its notes are reported only. The planner runs the same check before pushing:
//
//   node .github/factory/plan-check.mjs specs/NNNN-slug/plan.md [--base <rev>]    (default base: origin/main)
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { loadPolicy, git, run } from './lib.mjs';
import { parseDelta, parseTickets, ticketProblems } from './plan.mjs';
import { overlaps, uses } from './plan-deps.mjs';
import { deltaForms } from './plan-forms.mjs';
import { tierFloor } from './plan-tier.mjs';
import { planRefs } from './plan-refs.mjs';

/**
 * markdown: the plan. policy: the factory policy. exists(path): whether a repository-relative path is on the base.
 * research, previous, base: the texts of research.md next to the plan, of the plan at the branch's previous commit and
 * on the base branch, or undefined when there is none.
 * → { problems: string[], notes: string[] }
 */
export function planCheck(markdown, { policy, exists, research, previous, base }) {
  const problems = [], notes = [];
  const add = (found) => { problems.push(...found.problems); notes.push(...found.notes); };
  let delta = null, tickets = [];
  try { delta = parseDelta(markdown); } catch (e) { problems.push(e.message); }
  try { tickets = parseTickets(markdown); } catch (e) { problems.push(e.message); return { problems, notes }; }
  problems.push(...ticketProblems(tickets, policy), ...overlaps(tickets));
  add(uses(tickets, { exists }));
  problems.push(...deltaForms(delta));
  add(tierFloor(tickets, { policy, delta, exists }));
  try { add(planRefs(markdown, { research, previous, base })); } catch (e) { problems.push(e.message); }
  return { problems, notes };
}

/** The two lists as Markdown bullets under their counts. */
export function formatCheck({ problems, notes }) {
  return [`${problems.length} problem(s)`, ...problems.map((p) => `- ${p}`), '', `${notes.length} note(s)`, ...notes.map((n) => `- ${n}`)].join('\n');
}

if (path.basename(process.argv[1] ?? '') === 'plan-check.mjs') {
  const args = process.argv.slice(2);
  const at = args.indexOf('--base');
  const rev = at >= 0 ? args[at + 1] : 'origin/main';
  const file = args.find((a, i) => a !== '--base' && (at < 0 || i !== at + 1));
  if (!file || !rev) {
    console.error('usage: node plan-check.mjs <plan.md> [--base <rev>]');
    process.exit(2);
  }
  const attempt = (f) => { try { return f(); } catch { return undefined; } };
  const top = attempt(() => git('.', 'rev-parse', '--show-toplevel').trim());
  const tracked = (top && attempt(() => git(top, 'ls-tree', '-r', '--name-only', rev).split('\n'))) ?? [];
  if (tracked.length === 0) console.error(`warning: cannot list the files of ${rev}; every touch counts as new`);
  const files = new Set(tracked);
  const exists = (p) => files.has(p) || tracked.some((f) => f.startsWith(p.endsWith('/') ? p : `${p}/`));
  const rel = top ? path.relative(path.resolve(top), path.resolve(file)).replaceAll('\\', '/') : '..';
  const inside = !rel.startsWith('..') && !path.isAbsolute(rel);
  const show = (r) => (inside ? attempt(() => run('git', ['-C', top, 'show', `${r}:${rel}`], { stdio: ['ignore', 'pipe', 'ignore'] })) : undefined);
  const researchFile = path.join(path.dirname(file), 'research.md');
  const result = planCheck(readFileSync(file, 'utf8'), {
    policy: loadPolicy(), exists,
    research: existsSync(researchFile) ? readFileSync(researchFile, 'utf8') : undefined,
    previous: show('HEAD'), base: show(rev),
  });
  console.log(formatCheck(result));
  process.exit(result.problems.length ? 1 : 0);
}
