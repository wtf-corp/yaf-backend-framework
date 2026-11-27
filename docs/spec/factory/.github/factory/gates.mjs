// Entry point of factory-gates.yml. Computes every deterministic gate for one pull request and
// reports each as a commit status (issue #278). Run from a checkout of the BASE branch; the pull request head is
// checked out in a sub-directory and is only read, never executed.
//
//   node .github/factory/gates.mjs --pr-dir pr
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { loadPolicy, git, api, repoSlug, readEvent, setCheck, setStatus, writeSummary, setOutput, parseNumstat, parseDiff, makeHumanCheck, lastLabelActor, matchesAny } from './lib.mjs';
import { countSize, sizeVerdict } from './size.mjs';
import { actualDelta, compareDelta, deltaVerdict, isEmptyDelta } from './delta.mjs';
import { findGuardChanges, dropMovedTests, guardVerdict } from './guards.mjs';
import { classify, prKind, TIERS } from './classify.mjs';
import { parseBody, ticketVerdict, planOnBase } from './ticket.mjs';
import { parseDelta, planFlags, parseTickets, ticketProblems } from './plan.mjs';
import { approvingHuman, approvalTier, humanApprovalVerdict } from './human-approval.mjs';
import { editorconfigVerdict, packagesVerdict } from './declared.mjs';

/**
 * The guard files whose change the plan declares exactly (ADR-0034 Decisions 4–6). planText: the plan as it is on the
 * base branch, or null; never the pull request's copy (Decision 5). texts: Map<file, { base, head }> of the changed files.
 * → string[]
 */
export function declaredGuardFiles(planText, texts, policy) {
  let delta = null;
  try { delta = planText ? parseDelta(planText) : null; } catch { delta = null; }
  if (!delta) return [];
  const { files = [], neverLowered = [] } = policy.declaredGuards ?? {};
  const pins = delta.dependencies.filter((d) => d.includes('@'));
  return [...texts].filter(([file, { base, head }]) => {
    if (!files.includes(file)) return false;
    if (file === '.editorconfig') return delta.editorconfig.length > 0 && editorconfigVerdict(base, head, delta.editorconfig, neverLowered).declared;
    if (file === 'Directory.Packages.props') return pins.length > 0 && packagesVerdict(base, head, delta.dependencies).declared;
    return false;
  }).map(([file]) => file);
}

if (path.basename(process.argv[1] ?? '') === 'gates.mjs') {
  const args = process.argv.slice(2);
  const prDir = args[args.indexOf('--pr-dir') + 1] ?? 'pr';
  const baseDir = process.cwd();
  const policy = loadPolicy();
  const repo = repoSlug();
  const event = readEvent();

  // Merge queue: the pull request already passed every gate; report the same names on the queue's commit.
  // The two reviews stay check runs (the ruleset takes them from the reviewer app); the gates are statuses.
  if (process.env.GITHUB_EVENT_NAME === 'merge_group') {
    for (const name of policy.checks) {
      if (name === 'factory/review' || name === 'factory/doc-review') setCheck(event.merge_group.head_sha, name, 'success', 'passed on the pull request');
      else setStatus(event.merge_group.head_sha, name, 'success', 'passed on the pull request');
    }
    process.exit(0);
  }

  const number = event.pull_request.number;
  const pr = api(`repos/${repo}/pulls/${number}`); // fresh: the event payload can be stale on label and review events
  const sha = pr.head.sha;
  const labels = pr.labels.map((l) => l.name);
  const isHuman = makeHumanCheck(policy);
  const timeline = api(`repos/${repo}/issues/${number}/timeline?per_page=100`, { paginate: true });
  const humanLabel = (name) => labels.includes(name) && isHuman(lastLabelActor(timeline, name));

  const range = `${pr.base.sha}...${sha}`;
  const numstat = parseNumstat(git(prDir, 'diff', '--numstat', '-M', range));
  const diff = parseDiff(git(prDir, 'diff', '-M', '--unified=0', range));
  const files = numstat.map((f) => f.file);
  const kind = prKind(files, policy);

  // size
  const size = countSize(numstat, policy);
  const sizeV = sizeVerdict(size.lines, policy, humanLabel('size-accepted'));

  // guards
  const guardFindings = dropMovedTests(findGuardChanges(diff), diff);

  // delta against the plan on the base branch
  const parsed = parseBody(pr.body ?? '');
  const planFile = parsed.plan?.file ?? null;
  const planHere = planFile && planOnBase(baseDir, planFile) ? readFileSync(path.join(baseDir, planFile), 'utf8') : null;
  let declared = null;
  let deltaError = null;
  try {
    declared = planHere ? parseDelta(planHere) : null;
  } catch (e) {
    deltaError = e.message;
  }
  const actual = actualDelta(diff, policy);
  const comparison = compareDelta(actual, declared);

  // guard changes declared by that same plan (ADR-0034): a pull request cannot declare its own
  const mergeBase = git(prDir, 'merge-base', pr.base.sha, sha).trim();
  const show = (rev, f) => { try { return git(prDir, 'show', `${rev}:${f}`); } catch { return ''; } };
  const guardTexts = new Map((policy.declaredGuards?.files ?? []).filter((f) => files.includes(f)).map((f) => [f, { base: show(mergeBase, f), head: show(sha, f) }]));
  const declaredFiles = declaredGuardFiles(planHere, guardTexts, policy);
  const guardV = guardVerdict(guardFindings, humanLabel('guard-change'), declaredFiles);

  // tier: a tier:* label counts as a raise only when someone other than this job applied it
  const raisedTo = TIERS.filter((t) => labels.includes(`tier:${t}`))
    .filter((t) => lastLabelActor(timeline, `tier:${t}`)?.login !== policy.bots.gates)
    .at(-1) ?? null;
  const tierResult = classify({
    files, diff, title: pr.title, body: pr.body ?? '', labels, raisedTo,
    sizeOverHard: sizeV.overHard,
    deltaEmpty: isEmptyDelta(actual),
    deltaClean: comparison.undeclared.length === 0 && comparison.removed.length === 0,
    guardFindings: guardFindings.filter((g) => !declaredFiles.includes(g.file)).length,
    declaredFiles,
    policy,
  });
  const tier = kind === 'spec' ? 'high' : tierResult.tier;

  // keep exactly one tier label, without touching a label a human or an agent set to raise the tier
  for (const t of TIERS) {
    const name = `tier:${t}`;
    const mine = lastLabelActor(timeline, name)?.login === policy.bots.gates;
    if (t !== tier && labels.includes(name) && mine) api(`repos/${repo}/issues/${number}/labels/${encodeURIComponent(name)}`, { method: 'DELETE' });
  }
  if (!labels.includes(`tier:${tier}`)) api(`repos/${repo}/issues/${number}/labels`, { method: 'POST', body: { labels: [`tier:${tier}`] } });

  setStatus(sha, 'factory/tier', 'success', `tier ${tier}`, `Floor: **${tierResult.floor}**. Kind: ${kind}.\n\n${tierResult.reasons.map((r) => `- ${r}`).join('\n')}`);

  // ticket
  let issue = null;
  if (kind === 'code' && parsed.closes.length === 1) {
    try { issue = api(`repos/${repo}/issues/${parsed.closes[0]}`); } catch { issue = null; }
  }
  const touchesPlan = kind === 'code' && files.some((f) => matchesAny(f, policy.docPaths.plan));
  const ticketV = ticketVerdict({ kind, tier, changedLines: size.lines, parsed, issue, planExistsOnBase: Boolean(planHere), touchesPlan });
  setStatus(sha, 'factory/ticket', ticketV.pass ? 'success' : 'failure', ticketV.title);

  // size, guards, delta
  const sizeSummary = `Counted ${size.counted.length} file(s), excluded ${size.excluded.length}.\n\n` + size.counted.map((f) => `- ${f.file}: +${f.added} −${f.deleted}`).join('\n');
  if (kind === 'code') setStatus(sha, 'factory/size', sizeV.pass ? 'success' : 'failure', sizeV.title, sizeSummary);
  else setStatus(sha, 'factory/size', 'success', `${kind} pull request: the size limit applies to code`, sizeSummary);

  setStatus(sha, 'factory/guards', guardV.pass ? 'success' : 'failure', guardV.title, guardFindings.map((f) => `- ${f.file}: ${f.what} — \`${f.line.slice(0, 160)}\``).join('\n'));

  const deltaV = deltaError ? { pass: false, title: `the plan's factory:delta block is invalid: ${deltaError}` } : deltaVerdict(tier, actual, comparison);
  const deltaSummary = [
    ...comparison.undeclared.map((d) => `- undeclared ${d.category}: \`${d.line.slice(0, 200)}\``),
    ...comparison.removed.map((d) => `- removed ${d.category}: \`${d.line.slice(0, 200)}\``),
  ].join('\n');
  setStatus(sha, 'factory/delta', deltaV.pass ? 'success' : 'failure', deltaV.title, deltaSummary);

  // plan gate
  if (kind === 'plan') {
    const reviews = api(`repos/${repo}/pulls/${number}/reviews?per_page=100`, { paginate: true });
    const approver = approvingHuman(reviews, { author: pr.user.login, headSha: sha, isHuman });
    const reasons = [];
    const problems = [];
    for (const f of files.filter((f) => f.endsWith('/plan.md'))) {
      const text = readFileSync(path.join(prDir, f), 'utf8'); // read as data
      try {
        const flags = planFlags(text, policy);
        reasons.push(...flags.reasons.map((r) => `${f}: ${r}`));
        problems.push(...ticketProblems(parseTickets(text), policy).map((p) => `${f}: ${p}`));
      } catch (e) {
        problems.push(`${f}: ${e.message}`);
      }
    }
    if (problems.length) setStatus(sha, 'factory/plan-gate', 'failure', `${problems.length} problem(s) in the ticket list`, problems.map((p) => `- ${p}`).join('\n'));
    else if (reasons.length === 0) setStatus(sha, 'factory/plan-gate', 'success', 'not flagged: merges after the plan review passes');
    else setStatus(sha, 'factory/plan-gate', approver ? 'success' : 'failure', approver ? `flagged, approved by ${approver}` : 'flagged: needs a human maintainer\'s approving review', reasons.map((r) => `- ${r}`).join('\n'));
  } else {
    setStatus(sha, 'factory/plan-gate', 'success', 'not a plan pull request');
  }

  // human approval (specs are covered by CODEOWNERS; the check still demands a maintainer because kind spec is tier high)
  const reviews = api(`repos/${repo}/pulls/${number}/reviews?per_page=100`, { paginate: true });
  const approvalV = humanApprovalVerdict(approvalTier(kind, tier), approvingHuman(reviews, { author: pr.user.login, headSha: sha, isHuman }));
  setStatus(sha, 'factory/human-approval', approvalV.pass ? 'success' : 'failure', kind === 'plan' && tier !== 'high' ?'plan pull request: see factory/plan-gate' : approvalV.title);

  // the two agent reviews report elsewhere; report "not applicable" for the one that does not run on this kind
  if (kind === 'code') setCheck(sha, 'factory/doc-review', 'success', 'not a spec or plan pull request');
  else setCheck(sha, 'factory/review', 'success', `${kind} pull request: reviewed by factory/doc-review`);

  setOutput('tier', tier);
  setOutput('kind', kind);
  writeSummary(`### Factory gates\n\n| | |\n|---|---|\n| Kind | ${kind} |\n| Tier | **${tier}** (floor ${tierResult.floor}) |\n| Size | ${sizeV.title} |\n| Ticket | ${ticketV.title} |\n| Delta | ${deltaV.title} |\n| Guards | ${guardV.title} |\n\n${tierResult.reasons.map((r) => `- ${r}`).join('\n')}`);
}
