// Turns a reviewer's structured verdict into a check run and one summary comment.
// Vendor-neutral: any agent that produces JSON matching review.schema.json / doc-review.schema.json can feed it.
//   REVIEW_OUTPUT='<json>' node .github/factory/post-review.mjs --check factory/review
//   REVIEW_OUTPUT='<json>' node .github/factory/post-review.mjs --check factory/doc-review
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { api, repoSlug, readEvent, setCheck, loadPolicy } from './lib.mjs';
import { prKind } from './classify.mjs';
import { planFlags } from './plan.mjs';

const MARKER = (check) => `<!-- factory:review-summary ${check} -->`;
const ROUNDS = /<!-- factory:rounds=(\d+) -->/;

/**
 * True when a human must approve the pull request anyway: a spec (a code owner), or a plan pull request with a
 * flagged changed plan (factory/plan-gate). plans: the texts of the changed plan.md files, or null when unreadable.
 */
export function heldForHuman(kind, plans, policy) {
  if (kind === 'spec') return true;
  if (kind !== 'plan' || !plans) return false;
  try {
    return plans.map((text) => planFlags(text, policy).flagged).some(Boolean);
  } catch {
    return false; // a plan that cannot be parsed is not shown to be flagged: fail closed
  }
}

/**
 * held: whether the pull request is held for a human (heldForHuman). Only then, and only for factory/doc-review,
 * are findings marked `for: "owner"` left out of the counts; otherwise they count by their severity.
 * → { pass, title, owner } (owner: the findings left out)
 */
export function verdictOf(check, review, held = false) {
  if (!review) return { pass: false, title: 'the reviewer returned no structured verdict' };
  const marked = check === 'factory/doc-review' ? (review.findings ?? []).filter((f) => f.for === 'owner') : [];
  const owner = held ? marked : [];
  const count = (s) => (review.findings ?? []).filter((f) => f.severity === s && !owner.includes(f)).length;
  const blockers = count('Blocker');
  const majors = count('Major');
  const ok = check === 'factory/review' ? review.verdict === 'ready' : review.verdict === 'pass';
  const failedRow = (review.passes ?? review.checklist ?? []).some((r) => r.result === 'fail');
  const pass = ok && blockers === 0 && majors === 0 && !failedRow;
  const title = pass
    ? `${review.verdict}: no open Blocker or Major${owner.length ? `; ${owner.length} for the owner` : ''}`
    : `changes required — Blocker ${blockers}, Major ${majors}, Minor ${count('Minor')}, Nit ${count('Nit')}${marked.length > owner.length ? '; a finding for the owner on a plan that is not flagged' : ''}`;
  return { pass, title, owner };
}

const esc = (s = '') => String(s).replaceAll('|', '\\|').replaceAll('\n', ' ');

export function renderSummary(check, review, verdict, rounds) {
  const lines = [MARKER(check), `<!-- factory:rounds=${rounds} -->`, `## ${check === 'factory/review' ? 'Pull-request review' : 'Document review'}: ${verdict.pass ? 'ready' : 'changes required'}`, '', verdict.title, ''];
  if (!review) return lines.join('\n');
  if (review.passes) lines.push('| Pass | Result | Notes |', '|---|---|---|', ...review.passes.map((p) => `| ${p.pass} | ${p.result} | ${esc(p.notes)} |`), '');
  if (review.checklist) lines.push('| Checklist item | Result | Note |', '|---|---|---|', ...review.checklist.map((c) => `| ${esc(c.item)} | ${c.result} | ${esc(c.note)} |`), '');
  if (review.coverage?.length) lines.push('### Requirement coverage', '', '| Requirement | Test | Evidence |', '|---|---|---|', ...review.coverage.map((c) => `| ${esc(c.requirement)} | ${esc(c.test)} | ${esc(c.evidence)} |`), '');
  const owner = verdict.owner ?? [];
  const finding = (f) => [`**F${review.findings.indexOf(f) + 1} · ${f.severity}** — \`${f.file}:${f.line}\`  `, `${f.what}  `, ...(f.evidence ? [`*Evidence:* ${f.evidence}  `] : []), `*Suggested fix:* ${f.fix}`, ''];
  const open = (review.findings ?? []).filter((f) => !owner.includes(f));
  if (open.length) lines.push('### Findings', '', ...open.flatMap(finding));
  if (owner.length) lines.push('### For the owner', '', 'Questions only the owner can decide. They do not fail this check: the owner answers them in the approving review this pull request waits for (`factory/plan-gate` for a plan, a code owner\'s for a spec).', '', ...owner.flatMap(finding));
  if (review.sizeJustification) lines.push(`**Size:** ${review.sizeJustification}`, '');
  if (review.questions?.length) lines.push('### Questions', '', ...review.questions.map((q) => `- ${q}`), '');
  lines.push('_The reviewer read the change; it did not execute it. Executed evidence is in the build, test and `factory/test-evidence` jobs._');
  return lines.join('\n');
}

if (process.argv[1]?.endsWith('post-review.mjs')) {
  const args = process.argv.slice(2);
  const check = args[args.indexOf('--check') + 1];
  const policy = loadPolicy();
  const repo = repoSlug();
  const pr = readEvent().pull_request;
  let review = null;
  try { review = JSON.parse(process.env.REVIEW_OUTPUT || 'null'); } catch { review = null; }
  // An owner finding is set aside only on a pull request a human must approve anyway, decided from the changed
  // files as gates.mjs does for the plan gate: the head is in pr/ and is read as data. Unreadable: not held.
  let held = false;
  if (check === 'factory/doc-review') {
    try {
      const files = api(`repos/${repo}/pulls/${pr.number}/files?per_page=100`, { paginate: true }).map((f) => f.filename);
      const plans = files.filter((f) => f.endsWith('/plan.md')).map((f) => readFileSync(path.join('pr', f), 'utf8'));
      held = heldForHuman(prKind(files, policy), plans, policy);
    } catch {
      held = false;
    }
  }
  const verdict = verdictOf(check, review, held);

  const comments = api(`repos/${repo}/issues/${pr.number}/comments?per_page=100`, { paginate: true });
  const existing = comments.find((c) => c.body?.includes(MARKER(check)));
  const before = Number(existing?.body.match(ROUNDS)?.[1] ?? 0);
  const rounds = verdict.pass ? before : before + 1;
  const body = renderSummary(check, review, verdict, rounds);
  if (existing) api(`repos/${repo}/issues/comments/${existing.id}`, { method: 'PATCH', body: { body } });
  else api(`repos/${repo}/issues/${pr.number}/comments`, { method: 'POST', body: { body } });

  // The first failed review plus `fixRounds` fix attempts; after that a human decides.
  if (!verdict.pass && rounds > policy.limits.fixRounds + 1) {
    api(`repos/${repo}/issues/${pr.number}/labels`, { method: 'POST', body: { labels: ['needs-human'] } });
    console.log(`fix rounds exhausted (${rounds - 1} of ${policy.limits.fixRounds}): needs-human applied`);
  }
  setCheck(pr.head.sha, check, verdict.pass ? 'success' : 'failure', verdict.title, body);
}
