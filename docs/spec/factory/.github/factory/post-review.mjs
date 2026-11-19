// Turns a reviewer's structured verdict into a check run and one summary comment.
// Vendor-neutral: any agent that produces JSON matching review.schema.json / doc-review.schema.json can feed it.
//   REVIEW_OUTPUT='<json>' node .github/factory/post-review.mjs --check factory/review
//   REVIEW_OUTPUT='<json>' node .github/factory/post-review.mjs --check factory/doc-review
import { api, repoSlug, readEvent, setCheck, loadPolicy } from './lib.mjs';

const MARKER = (check) => `<!-- factory:review-summary ${check} -->`;
const ROUNDS = /<!-- factory:rounds=(\d+) -->/;

/** → { pass, title } */
export function verdictOf(check, review) {
  if (!review) return { pass: false, title: 'the reviewer returned no structured verdict' };
  const count = (s) => (review.findings ?? []).filter((f) => f.severity === s).length;
  const blockers = count('Blocker');
  const majors = count('Major');
  const ok = check === 'factory/review' ? review.verdict === 'ready' : review.verdict === 'pass';
  const failedRow = (review.passes ?? review.checklist ?? []).some((r) => r.result === 'fail');
  const pass = ok && blockers === 0 && majors === 0 && !failedRow;
  return { pass, title: pass ? `${review.verdict}: no open Blocker or Major` : `changes required — Blocker ${blockers}, Major ${majors}, Minor ${count('Minor')}, Nit ${count('Nit')}` };
}

const esc = (s = '') => String(s).replaceAll('|', '\\|').replaceAll('\n', ' ');

export function renderSummary(check, review, verdict, rounds) {
  const lines = [MARKER(check), `<!-- factory:rounds=${rounds} -->`, `## ${check === 'factory/review' ? 'Pull-request review' : 'Document review'}: ${verdict.pass ? 'ready' : 'changes required'}`, '', verdict.title, ''];
  if (!review) return lines.join('\n');
  if (review.passes) lines.push('| Pass | Result | Notes |', '|---|---|---|', ...review.passes.map((p) => `| ${p.pass} | ${p.result} | ${esc(p.notes)} |`), '');
  if (review.checklist) lines.push('| Checklist item | Result | Note |', '|---|---|---|', ...review.checklist.map((c) => `| ${esc(c.item)} | ${c.result} | ${esc(c.note)} |`), '');
  if (review.coverage?.length) lines.push('### Requirement coverage', '', '| Requirement | Test | Evidence |', '|---|---|---|', ...review.coverage.map((c) => `| ${esc(c.requirement)} | ${esc(c.test)} | ${esc(c.evidence)} |`), '');
  if (review.findings?.length) {
    lines.push('### Findings', '');
    review.findings.forEach((f, i) => lines.push(`**F${i + 1} · ${f.severity}** — \`${f.file}:${f.line}\`  `, `${f.what}  `, ...(f.evidence ? [`*Evidence:* ${f.evidence}  `] : []), `*Suggested fix:* ${f.fix}`, ''));
  }
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
  const verdict = verdictOf(check, review);

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
