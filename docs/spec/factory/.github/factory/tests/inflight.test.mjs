import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { loadPolicy } from '../lib.mjs';
import { marker } from '../markers.mjs';
import { nextAction, parseWorktrees, worktreeState, pullRequestRow, worktreeRow, inflight } from '../inflight.mjs';

const policy = loadPolicy();
const now = new Date('2026-11-20T12:00:00Z');
const minutesAgo = (n) => new Date(now.getTime() - n * 60_000).toISOString();
const GREEN = ['factory/tier', 'factory/ticket', 'factory/size', 'factory/delta', 'factory/guards', 'factory/plan-gate', 'factory/human-approval',
  'factory/review', 'factory/doc-review', 'references', 'factory-self-test', 'factory/test-evidence'];
const failed = (title = '', conclusion = 'failure') => ({ status: 'completed', conclusion, title });
const running = (minutes) => ({ status: 'in_progress', conclusion: null, startedAt: minutesAgo(minutes) });

/** A green, undrafted code pull request; `checks` overrides a check run by name, and null removes it. */
function pr(checks = {}, extra = {}) {
  const runs = GREEN.filter((name) => checks[name] !== null).map((name) => ({ name, status: 'completed', conclusion: 'success', title: '', ...checks[name] }));
  return { labels: ['factory', 'tier:medium'], draft: false, kind: 'code', approved: false, checks: runs, rounds: 0, referencesFixTried: false, worktree: null, ...extra };
}
const act = (checks, extra) => nextAction(pr(checks, extra), policy, now);

test('a failed review is a fix round with its number', () => {
  const a = act({ 'factory/review': failed('changes required') }, { rounds: 2 });
  assert.equal(a.action, 'fix');
  assert.equal(a.round, 2);
  assert.equal(a.cause, 'factory/review');
  assert.ok(a.reason);
});

test('needs-human is the owner\'s, before anything else', () => {
  const a = act({ 'factory/review': failed() }, { labels: ['factory', 'needs-human'], draft: true });
  assert.equal(a.action, 'human');
  assert.match(a.reason, /needs-human/);
});

test('a draft needs nothing', () => {
  assert.equal(act({ 'factory/review': null }, { draft: true }).action, 'none');
});

test('without a completed factory/tier the gates are waited for', () => {
  assert.equal(act({ 'factory/tier': null, references: failed() }).action, 'wait');
  assert.equal(act({ 'factory/tier': running(1), 'factory/review': null }).action, 'wait');
});

test('a failed references job is one fix round, then gate-failed', () => {
  const first = act({ references: failed() });
  assert.deepEqual([first.action, first.cause, first.round], ['fix', 'references', null]);
  const second = act({ references: failed() }, { referencesFixTried: true });
  assert.deepEqual([second.action, second.cause], ['gate-failed', 'references']);
  assert.equal(act({ references: failed() }, { kind: 'plan' }).action, 'gate-failed'); // no fix round on a plan
});

test('a failed gate is gate-failed', () => {
  for (const name of ['factory/tier', 'factory/ticket', 'factory/size', 'factory/delta', 'factory/guards']) {
    const a = act({ [name]: failed(), 'factory/review': failed() });
    assert.deepEqual([a.action, a.cause], ['gate-failed', name]);
  }
  assert.equal(act({ 'factory/plan-gate': failed('2 problem(s) in the ticket list') }, { kind: 'plan' }).action, 'gate-failed');
});

test('a failed self-test is gate-failed', () => {
  assert.deepEqual(act({ 'factory-self-test': failed() }).cause, 'factory-self-test');
  assert.equal(act({ 'factory-self-test': failed() }).action, 'gate-failed');
  assert.equal(act({ 'factory/test-evidence': failed() }).action, 'gate-failed');
});

test('a review in progress is waited for, a stale one is owed', () => {
  assert.equal(act({ 'factory/review': running(29) }).action, 'wait');
  assert.equal(act({ 'factory/doc-review': running(3) }, { kind: 'plan' }).action, 'wait');
  assert.equal(act({ 'factory/review': running(30) }).action, 'review');
  assert.equal(act({ 'factory/review': { status: 'in_progress', conclusion: null } }).action, 'review'); // no start time
});

test('a missing review check is owed', () => {
  assert.equal(act({ 'factory/review': null }).action, 'review');
  assert.equal(act({ 'factory/doc-review': null }, { kind: 'spec' }).action, 'review');
  assert.equal(act({ 'factory/review': null, 'factory/doc-review': running(2) }).action, 'wait'); // row 5 before row 6
});

test('a plan\'s or a spec\'s failed review is the owner\'s', () => {
  for (const kind of ['plan', 'spec']) {
    const a = act({ 'factory/doc-review': failed() }, { kind, rounds: 1 });
    assert.deepEqual([a.action, a.reason, a.round], ['human', 'a plan or spec fix is attended', null]);
  }
});

test('a worktree ahead of the pull request waits for the push', () => {
  const review = { 'factory/review': failed() };
  assert.equal(act(review, { rounds: 1, worktree: { locked: false, ahead: true } }).action, 'wait');
  assert.equal(act(review, { rounds: 1, worktree: { locked: true, ahead: false } }).action, 'wait');
  assert.equal(act(review, { rounds: 1, worktree: { locked: false, ahead: false } }).action, 'fix');
});

test('an unapproved tier-high plan is the owner\'s', () => {
  const plan = { kind: 'plan', labels: ['factory', 'tier:low', 'tier:high'] };
  assert.equal(act({}, plan).action, 'human');
  assert.equal(act({}, { ...plan, approved: true }).action, 'wait');
  assert.equal(act({ 'factory/human-approval': failed('tier high: waiting for an approving review') }, { approved: true }).action, 'human');
});

test('a flagged plan gate is the owner\'s', () => {
  const a = act({ 'factory/plan-gate': failed('flagged: needs a human maintainer\'s approving review') }, { kind: 'plan' });
  assert.equal(a.action, 'human');
  assert.equal(act({ 'factory/plan-gate': failed('flagged: needs a human maintainer\'s approving review'), 'factory/doc-review': null }, { kind: 'plan' }).action, 'review');
});

test('a green pull request waits for GitHub to merge', () => {
  assert.deepEqual(act(), { action: 'wait', reason: 'the checks are green: GitHub merges', round: null, cause: null });
});

test('a references fix in the making is waited for', () => {
  for (const worktree of [{ locked: true, ahead: false }, { locked: false, ahead: true }]) {
    assert.equal(act({ references: failed() }, { referencesFixTried: true, worktree }).action, 'wait');
    assert.equal(act({ references: failed() }, { worktree }).action, 'wait');
    assert.equal(act({ 'factory-self-test': failed() }, { worktree }).action, 'gate-failed');
  }
  assert.equal(act({ references: failed() }, { referencesFixTried: true, worktree: { locked: false, ahead: false } }).action, 'gate-failed');
});

test('used-up fix rounds are the owner\'s', () => {
  const review = { 'factory/review': failed() };
  assert.equal(policy.limits.fixRounds, 2);
  assert.deepEqual([act(review, { rounds: 2 }).action, act(review, { rounds: 2 }).round], ['fix', 2]);
  assert.deepEqual([act(review, { rounds: 3 }).action, act(review, { rounds: 3 }).reason], ['human', 'fix rounds used up']);
  assert.equal(act(review, { rounds: 3, worktree: { locked: false, ahead: true } }).action, 'human');
});

test('a failed review with no counted summary is the owner\'s', () => {
  for (const rounds of [undefined, null, NaN, 0, 1.5, '2']) {
    const a = act({ 'factory/review': failed() }, { rounds });
    assert.deepEqual([a.action, a.reason, a.round], ['human', 'no counted review summary', null]);
  }
  assert.equal(act({ 'factory/review': failed() }, { rounds: 1 }).action, 'fix');
});

test('a check that has not reported is not called green', () => {
  assert.deepEqual([act({ 'factory/human-approval': null }).action, act({ 'factory/human-approval': null }).reason], ['wait', 'not reported yet: factory/human-approval']);
  assert.equal(act({ 'factory/plan-gate': running(1) }).reason, 'not reported yet: factory/plan-gate');
});

test('a review that ended without a verdict is owed, not a fix', () => {
  for (const conclusion of ['cancelled', 'timed_out', 'action_required']) {
    const a = act({ 'factory/review': failed('', conclusion) }, { rounds: 1 });
    assert.equal(a.action, 'review');
    assert.match(a.reason, new RegExp(conclusion));
  }
  assert.notEqual(act({ 'factory/review': { status: 'in_progress', conclusion: null } }).reason, act({ 'factory/review': running(31) }).reason);
});

test('a cancelled or timed-out gate or job is failed, not passed', () => {
  for (const conclusion of ['cancelled', 'timed_out']) {
    assert.equal(act({ 'factory/guards': failed('', conclusion) }).action, 'gate-failed');
    assert.equal(act({ 'factory-self-test': failed('', conclusion) }).action, 'gate-failed');
    assert.equal(act({ 'factory/human-approval': failed('', conclusion) }).action, 'human');
  }
  assert.equal(act({ 'factory/test-evidence': failed('', 'skipped') }).action, 'wait');
});

test('the rows apply in the table\'s order', () => {
  assert.equal(act({ 'factory/tier': null }, { draft: true }).action, 'none'); // row 2 before row 3
  assert.equal(act({ 'factory/size': failed(), 'factory/review': running(2) }).action, 'gate-failed'); // row 4b before row 5
  const high = { labels: ['factory', 'tier:high'], rounds: 1 };
  assert.equal(act({ 'factory/review': failed(), 'factory/human-approval': failed() }, high).action, 'fix'); // row 7 before row 8
  assert.equal(act({ 'factory/review': null }, high).action, 'review'); // row 6 before row 8
});

test('green is not claimed while a gate or a job has not reported', () => {
  for (const name of ['factory/size', 'factory/guards', 'references', 'factory-self-test', 'factory/test-evidence']) {
    for (const check of [null, running(1)]) {
      assert.deepEqual([act({ [name]: check }).action, act({ [name]: check }).reason], ['wait', `not reported yet: ${name}`]);
    }
  }
});

const HEAD = 'a'.repeat(40);
const PORCELAIN = `worktree C:/dev/yaf
HEAD ${'b'.repeat(40)}
branch refs/heads/main

worktree C:/dev/yaf/.claude/worktrees/agent-a1
HEAD ${HEAD}
branch refs/heads/ci/38-worktree-state
locked claude agent agent-a1 (pid 6872)

worktree C:/dev/yaf/.claude/worktrees/agent-a2
HEAD ${'c'.repeat(40)}
detached
locked

worktree C:/dev/yaf/.claude/worktrees/agent-a3
HEAD ${'d'.repeat(40)}
branch refs/heads/worktree-agent-a3
`;

test('parseWorktrees reads path, branch, head and lock', () => {
  const expected = [
    { path: 'C:/dev/yaf', branch: 'main', head: 'b'.repeat(40), locked: false },
    { path: 'C:/dev/yaf/.claude/worktrees/agent-a1', branch: 'ci/38-worktree-state', head: HEAD, locked: true },
    { path: 'C:/dev/yaf/.claude/worktrees/agent-a2', branch: null, head: 'c'.repeat(40), locked: true },
    { path: 'C:/dev/yaf/.claude/worktrees/agent-a3', branch: 'worktree-agent-a3', head: 'd'.repeat(40), locked: false },
  ];
  assert.deepEqual(parseWorktrees(PORCELAIN), expected);
  assert.deepEqual(parseWorktrees(PORCELAIN.replaceAll('\n', '\r\n')), expected);
  assert.deepEqual(parseWorktrees(''), []);
});

const verdict = (v, round, findings = '') => ({ verdict: v, round, findings, highestRound: round });
const NO_REVIEW = { verdict: null, round: null, findings: '', highestRound: null };
const OPEN = { labels: ['factory'], merged: false, open: true };
/** A ticket's worktree that is reviewed `ready`, pushed and has an open pull request; `extra` overrides an input. */
const state = (extra = {}) => worktreeState({ worktree: { head: HEAD, locked: false }, ticketLabels: ['factory'], pr: OPEN,
  review: verdict('ready', 0), awaitingPush: null, originHead: HEAD, ...extra }, policy);

test('a ticket or pull request with needs-human is a human\'s', () => {
  const rest = { worktree: { head: HEAD, locked: true }, review: verdict('changes-required', 1) };
  assert.equal(state({ ticketLabels: ['factory', 'needs-human'], ...rest }).state, 'human');
  assert.equal(state({ pr: { ...OPEN, labels: ['needs-human'] }, ...rest }).state, 'human');
  assert.equal(state({ pr: { labels: ['needs-human'], merged: true, open: false } }).state, 'human');
});

test('a locked worktree is being worked in', () => {
  const locked = { head: HEAD, locked: true };
  assert.equal(state({ worktree: locked, pr: { ...OPEN, merged: true, open: false } }).state, 'working');
  assert.equal(state({ worktree: locked, review: NO_REVIEW, pr: null, originHead: null }).state, 'working');
});

test('the worktree of a merged pull request is removed', () => {
  assert.equal(state({ pr: { labels: [], merged: true, open: false }, review: NO_REVIEW, originHead: null }).state, 'remove');
});

test('a head without a local-review marker is owed the local review', () => {
  assert.deepEqual(state({ review: NO_REVIEW, pr: null, originHead: null }), { state: 'local-review', round: null, findings: null, exhausted: false, notified: false });
  assert.equal(state({ review: { ...NO_REVIEW, highestRound: 1 } }).state, 'local-review'); // a verdict on another commit only
});

test('a changes-required verdict is a local fix with the marker\'s round and findings', () => {
  const s = state({ review: verdict('changes-required', 1, 'Major: the cap fails open'), pr: null, originHead: null });
  assert.deepEqual(s, { state: 'local-fix', round: 1, findings: 'Major: the cap fails open', exhausted: false, notified: false });
  assert.equal(state({ review: verdict('changes-required', 1) }).state, 'local-fix'); // pushed or not
});

test('the local loop ends at the limit', () => {
  assert.equal(policy.limits.fixRounds, 2);
  const at = (round) => state({ review: verdict('changes-required', round) });
  assert.deepEqual([at(0).state, at(0).round, at(0).exhausted], ['local-fix', 0, false]); // the first review
  assert.equal(at(1).exhausted, false);
  assert.deepEqual([at(2).state, at(2).round, at(2).exhausted], ['local-fix', 2, true]);
  assert.equal(at(3).exhausted, true);
  assert.equal(state({ review: verdict('ready', 2) }).exhausted, false);
});

test('push-owed is notified only with the marker for that head', () => {
  const unpushed = { pr: null, originHead: null };
  assert.deepEqual(state(unpushed), { state: 'push-owed', round: null, findings: null, exhausted: false, notified: false });
  assert.equal(state({ ...unpushed, awaitingPush: { user: { login: 'yaf-factory[bot]' }, body: `<!-- factory:awaiting-push ${HEAD} -->` } }).notified, true);
  const behind = state({ originHead: 'e'.repeat(40) }); // origin holds an older commit of the branch
  assert.deepEqual([behind.state, behind.notified], ['push-owed', false]);
});

test('a pushed head without an open pull request is owed one', () => {
  assert.equal(state({ pr: null }).state, 'open-pr');
  assert.equal(state({ pr: { labels: [], merged: false, open: false } }).state, 'open-pr'); // closed without a merge
  assert.equal(state({ pr: null, awaitingPush: { body: 'stale' } }).notified, false); // notified belongs to push-owed
});

test('a pushed head with an open pull request is in flight', () => {
  assert.deepEqual(state(), { state: 'in-flight', round: null, findings: null, exhausted: false, notified: false });
});

const by = (login, body) => ({ user: { login }, body });
const SUMMARY = '<!-- factory:review-summary factory/review -->\n<!-- factory:rounds=2 -->\n### Review\n**F1 · Major** — `inflight.mjs:3`\nthe cap fails open';
/** A check run as the REST API gives it, from the app the ruleset accepts that name from unless `slug` says otherwise. */
const checkRun = (name, conclusion, startedAt, id, title = '', slug = /review$/.test(name) ? 'yaf-reviewer' : 'github-actions') => (
  { id, name, status: conclusion ? 'completed' : 'in_progress', conclusion, started_at: startedAt, output: { title }, app: { slug } });
const RUNS = GREEN.map((name, i) => checkRun(name, 'success', minutesAgo(20), i + 1));
const REVIEW_FAILED = [...RUNS, checkRun('factory/review', 'failure', minutesAgo(10), 100, 'changes required')];
const PULL = { number: 61, state: 'open', draft: false, merged_at: null, body: 'Closes #39\nPlan: specs/0003-unattended-orchestration/plan.md#T8', user: { login: policy.bots.factory },
  labels: [{ name: 'factory' }, { name: 'tier:medium' }], head: { ref: 'ci/39-inflight-list', sha: HEAD } };
/** The row of pull request #61, whose review failed in round 2; `extra` overrides what was fetched. */
const row = (extra = {}, p = policy) => pullRequestRow({ pr: PULL, files: ['docs/spec/factory/.github/factory/inflight.mjs'], reviews: [], checkRuns: REVIEW_FAILED,
  comments: [by('yaf-reviewer[bot]', SUMMARY)], ticketComments: [], worktree: null, isHuman: (user) => user.login === 'mika666', ...extra }, p, now);

test('a pull request\'s row carries its action, round and findings', () => {
  assert.deepEqual(row(), { number: 61, branch: 'ci/39-inflight-list', issue: 39, kind: 'code', action: 'fix', reason: 'factory/review failed',
    round: 2, cause: 'factory/review', fixRounds: 2, findings: SUMMARY });
  // the newest check run of a name counts, and its start time is read
  const rerun = (minutes) => row({ checkRuns: [checkRun('factory/review', null, minutesAgo(minutes), 101), ...REVIEW_FAILED] });
  assert.deepEqual([rerun(5).action, rerun(5).findings], ['wait', null]);
  assert.equal(rerun(15).action, 'fix'); // started before the failed one
  assert.equal(row({ checkRuns: [...RUNS.slice(0, 7), checkRun('factory/review', null, minutesAgo(31), 101), ...RUNS.slice(8)] }).action, 'review');
  assert.equal(row({ checkRuns: [...REVIEW_FAILED, checkRun('factory/review', 'success', minutesAgo(10), 99)] }).action, 'fix'); // the same start: the highest id
  // a newer run of the name from another app is not read: a review from the reviewer app only, a gate from the gates bot only
  assert.equal(row({ checkRuns: [...REVIEW_FAILED, checkRun('factory/review', 'success', minutesAgo(1), 102, '', 'github-actions')] }).action, 'fix');
  assert.equal(row({ checkRuns: [...REVIEW_FAILED, checkRun('factory/size', 'failure', minutesAgo(1), 103, '', 'yaf-reviewer')] }).action, 'fix');
  assert.equal(row({ checkRuns: [...RUNS.slice(0, 7), checkRun('factory/review', 'success', minutesAgo(1), 104, '', 'yaf-factory'), ...RUNS.slice(8)] }).action, 'review');
  assert.equal(row({ checkRuns: RUNS.map((run) => ({ ...run, app: undefined })) }).reason, 'the gates have not reported');
  // the worktree that holds the branch, the kind and the closed issue
  assert.equal(row({ worktree: { locked: false, ahead: true } }).action, 'wait');
  const plan = row({ pr: { ...PULL, body: 'A plan.' }, files: ['specs/0004-x/plan.md'], checkRuns: RUNS });
  assert.deepEqual([plan.kind, plan.issue, plan.action], ['plan', null, 'wait']);
  // an approval counts on the head commit only, and from a human maintainer
  const high = { ...PULL, labels: [{ name: 'factory' }, { name: 'tier:high' }] };
  const approval = (commit, login = 'mika666') => [{ user: { login, type: 'User' }, state: 'APPROVED', commit_id: commit }];
  assert.equal(row({ pr: high, checkRuns: RUNS, reviews: approval(HEAD) }).reason, 'the checks are green: GitHub merges');
  assert.equal(row({ pr: high, checkRuns: RUNS, reviews: approval('e'.repeat(40)) }).action, 'human');
  assert.equal(row({ pr: high, checkRuns: RUNS, reviews: approval(HEAD, 'somebody') }).action, 'human');
});

test('a fix row holds the summary comment verbatim', () => {
  assert.equal(row({ comments: [by('somebody', 'hello'), by(policy.bots.gates, SUMMARY), by(policy.bots.factory, 'evidence')] }).findings, SUMMARY);
  // a summary by anyone but the reviewer app or the gates bot is not one, whatever it says
  const forged = row({ comments: [by('somebody', SUMMARY), by(policy.bots.factory, SUMMARY)] });
  assert.deepEqual([forged.action, forged.reason, forged.findings], ['human', 'no counted review summary', null]);
  // a policy without a reviewer app: the gates bot's summary counts, and an author without a login is nobody's
  const ci = { ...policy, bots: { factory: policy.bots.factory, gates: policy.bots.gates } };
  const inCi = [...RUNS.filter((run) => !/review$/.test(run.name)), checkRun('factory/doc-review', 'neutral', minutesAgo(20), 98, '', 'github-actions'),
    checkRun('factory/review', 'failure', minutesAgo(10), 100, '', 'github-actions')];
  assert.equal(row({ comments: [by(policy.bots.gates, SUMMARY)], checkRuns: inCi }, ci).action, 'fix');
  assert.equal(row({ comments: [by(policy.bots.gates, SUMMARY)] }, ci).action, 'review'); // there the reviewer app's runs are nobody's
  assert.equal(row({ comments: [{ user: {}, body: SUMMARY }], checkRuns: inCi }, ci).action, 'human');
  // a plan's summary is the doc review's
  const doc = [...RUNS, checkRun('factory/doc-review', 'failure', minutesAgo(10), 100)];
  assert.equal(row({ files: ['specs/0004-x/plan.md'], checkRuns: doc }).reason, 'a plan or spec fix is attended');
  // the references fix has no findings, and is tried once per ticket: the marker counts from the factory bot only
  const references = [...RUNS, checkRun('references', 'failure', minutesAgo(10), 100)];
  const tried = (ticketComments) => row({ checkRuns: references, ticketComments });
  assert.deepEqual([tried([]).action, tried([]).cause, tried([]).findings], ['fix', 'references', null]);
  assert.equal(tried([by(policy.bots.factory, marker('references-fix', 'e'.repeat(40)))]).action, 'gate-failed');
  assert.equal(tried([by('somebody', marker('references-fix', HEAD))]).action, 'fix');
  assert.equal(tried([by(policy.bots.factory, `Evidence:\n${marker('references-fix', HEAD)}`)]).action, 'fix'); // a quoted marker is text
  // no single closed issue: no ticket to mark the try on, so no fix is handed out
  for (const body of ['Trivial because: a typo', 'Closes #39\nCloses #40']) {
    const loose = row({ pr: { ...PULL, body }, checkRuns: references });
    assert.deepEqual([loose.issue, loose.action, loose.cause], [null, 'gate-failed', 'references']);
  }
});

/** A commit status as the REST API gives it, from the gates bot unless `login` says otherwise. */
const status = (context, state, createdAt = minutesAgo(20), id = 1, description = '', login = policy.bots.gates) => (
  { id, context, state, description, created_at: createdAt, creator: { login } });
const GATE_NAMES = ['factory/tier', 'factory/ticket', 'factory/size', 'factory/delta', 'factory/guards', 'factory/plan-gate', 'factory/human-approval'];
const JOB_RUNS = RUNS.filter((run) => !GATE_NAMES.includes(run.name));
/** The row of #61 with green reviews and jobs as check runs and the seven gates as statuses; `gates` overrides a status by name. */
const statusRow = (gates = {}, extra = {}) => row({ checkRuns: JOB_RUNS, comments: [],
  statuses: GATE_NAMES.map((name, i) => gates[name] ?? status(name, 'success', minutesAgo(20), i + 1)), ...extra });
const GREEN_REASON = 'the checks are green: GitHub merges';

test('the gates are read from the gates bot\'s statuses too', () => {
  assert.equal(statusRow().reason, GREEN_REASON);
  const others = statusRow({}, { statuses: GATE_NAMES.map((name) => status(name, 'success', minutesAgo(20), 1, '', 'somebody')) });
  assert.equal(others.reason, 'the gates have not reported');
  // pending is in progress: not reported; failure and error are failed
  assert.equal(statusRow({ 'factory/tier': status('factory/tier', 'pending') }).reason, 'the gates have not reported');
  assert.equal(statusRow({ 'factory/delta': status('factory/delta', 'pending') }).reason, 'not reported yet: factory/delta');
  assert.deepEqual([statusRow({ 'factory/size': status('factory/size', 'failure') }).action, statusRow({ 'factory/size': status('factory/size', 'failure') }).reason], ['gate-failed', 'failed: factory/size']);
  assert.equal(statusRow({ 'factory/guards': status('factory/guards', 'error') }).reason, 'failed: factory/guards');
  // a review comes from the reviewer app, never from a status
  assert.equal(statusRow({}, { checkRuns: JOB_RUNS.filter((run) => run.name !== 'factory/review'), statuses: [...GATE_NAMES.map((name) => status(name, 'success')), status('factory/review', 'success')] }).action, 'review');
});

test('a flagged plan gate status is the owner\'s, another failed one is gate-failed', () => {
  const plan = { pr: { ...PULL, body: 'A plan.' }, files: ['specs/0004-x/plan.md'] };
  const flagged = { 'factory/plan-gate': status('factory/plan-gate', 'failure', minutesAgo(20), 6, 'flagged: needs a human maintainer\'s approving review') };
  assert.deepEqual([statusRow(flagged, plan).action, statusRow(flagged, plan).reason], ['human', 'a flagged plan: waiting for a human maintainer\'s approval']);
  const high = { pr: { ...plan.pr, labels: [{ name: 'factory' }, { name: 'tier:high' }] }, files: plan.files };
  assert.equal(statusRow(flagged, high).reason, 'tier high: waiting for a human maintainer\'s approval of the head commit');
  assert.equal(statusRow({ 'factory/plan-gate': status('factory/plan-gate', 'failure', minutesAgo(20), 6, 'the plan has no Tickets table') }, plan).reason, 'failed: factory/plan-gate');
});

test('a check run and a status of one name must both pass; the newest status of a context counts', () => {
  const size = (conclusion) => checkRun('factory/size', conclusion, minutesAgo(20), 50);
  assert.equal(statusRow({ 'factory/size': status('factory/size', 'failure') }, { checkRuns: [...JOB_RUNS, size('success')] }).reason, 'failed: factory/size');
  assert.equal(statusRow({}, { checkRuns: [...JOB_RUNS, size('failure')] }).reason, 'failed: factory/size');
  assert.equal(statusRow({}, { checkRuns: [...JOB_RUNS, size('success')] }).reason, GREEN_REASON);
  // failed first, then passed (a flagged plan after its approval, or a re-run), in either order of the list
  const gate = (state, createdAt, id) => status('factory/plan-gate', state, createdAt, id);
  const rest = GATE_NAMES.filter((name) => name !== 'factory/plan-gate').map((name) => status(name, 'success'));
  const gates = (...plans) => statusRow({}, { statuses: [...rest, ...plans] }).reason;
  assert.equal(gates(gate('failure', minutesAgo(20), 10), gate('success', minutesAgo(5), 11)), GREEN_REASON);
  assert.equal(gates(gate('success', minutesAgo(5), 11), gate('failure', minutesAgo(20), 10)), GREEN_REASON);
  // the same creation time: the highest id is the newest
  assert.equal(gates(gate('failure', minutesAgo(5), 12), gate('success', minutesAgo(5), 11)), 'failed: factory/plan-gate');
  assert.equal(gates(gate('success', minutesAgo(5), 11), gate('failure', minutesAgo(5), 12)), 'failed: factory/plan-gate');
});

const WORKTREE = { path:'C:/dev/yaf/.claude/worktrees/agent-a1', branch: 'ci/39-inflight-list', head: HEAD, locked: false };
const READY = by(policy.bots.factory, marker('local-review', HEAD, 'ready round=1'));
const wrow = (extra = {}) => worktreeRow({ worktree: WORKTREE, ticketLabels: ['factory'], ticketComments: [READY], pr: PULL, originHead: HEAD, ...extra }, policy);

test('a worktree row holds its state', () => {
  assert.deepEqual(wrow(), { path: WORKTREE.path, branch: 'ci/39-inflight-list', head: HEAD, issue: 39, pullRequest: 61,
    state: 'in-flight', round: null, findings: null, exhausted: false, notified: false });
  assert.equal(wrow({ pr: { ...PULL, state: 'closed', merged_at: '2026-11-20T10:00:00Z' } }).state, 'remove');
  assert.equal(wrow({ pr: { ...PULL, labels: [{ name: 'needs-human' }] } }).state, 'human');
  assert.deepEqual([wrow({ pr: null }).state, wrow({ pr: null }).pullRequest], ['open-pr', null]);
  // the markers are read for the worktree's head, from the factory bot's comments
  assert.equal(wrow({ ticketComments: [by('somebody', READY.body)] }).state, 'local-review');
  assert.equal(wrow({ ticketComments: [by(policy.bots.factory, marker('local-review', 'e'.repeat(40), 'ready round=0'))] }).state, 'local-review');
  const fix = wrow({ ticketComments: [by(policy.bots.factory, `${marker('local-review', HEAD, 'changes-required round=2')}\nMajor: no test`)] });
  assert.deepEqual([fix.state, fix.round, fix.findings, fix.exhausted], ['local-fix', 2, 'Major: no test', true]);
  // push-owed: the owner was told only by the awaiting-push marker for this head
  const owed = (...comments) => wrow({ pr: null, originHead: null, ticketComments: [READY, ...comments] });
  assert.deepEqual([owed().state, owed().notified], ['push-owed', false]);
  assert.equal(owed(by(policy.bots.factory, marker('awaiting-push', 'e'.repeat(40)))).notified, false); // told about another commit
  assert.equal(owed(by('somebody', marker('awaiting-push', HEAD))).notified, false);
  assert.equal(owed(by(policy.bots.factory, marker('awaiting-push', HEAD))).notified, true);
});

test('the policy names the reviewer app', () => {
  assert.equal(policy.bots.reviewer, 'yaf-reviewer[bot]');
  const bots = JSON.parse(readFileSync(path.join(import.meta.dirname, '..', 'policy.schema.json'), 'utf8')).properties.bots;
  assert.equal(bots.additionalProperties, false);
  assert.deepEqual(Object.keys(policy.bots).filter((key) => !(key in bots.properties)), []); // the policy is valid
  assert.equal(bots.properties.reviewer.type, 'string');
  assert.deepEqual(bots.required, ['factory', 'gates']); // and so is one without a reviewer app
});

test('the command lists the pull requests and the ticket worktrees, and writes nothing', () => {
  const repo = 'acme/yaf';
  const OLD = 'e'.repeat(40);
  const PUSHED = 'f'.repeat(40);
  const at = (name, head, branch) => `worktree C:/dev/yaf/.claude/worktrees/${name}\nHEAD ${head}\nbranch refs/heads/${branch}\n\n`;
  const porcelain = `worktree C:/dev/yaf\nHEAD ${'b'.repeat(40)}\nbranch refs/heads/main\n\n` + at('agent-a1', HEAD, 'ci/39-inflight-list') + at('agent-a3', 'd'.repeat(40), 'worktree-agent-a3')
    + at('agent-a4', 'c'.repeat(40), 'ci/38-worktree-state') + at('agent-a5', PUSHED, 'fix/40-typo') + at('agent-a6', 'd'.repeat(40), 'ci/999-gone');
  const open = [{ ...PULL, head: { ref: PULL.head.ref, sha: OLD } },
    { ...PULL, number: 62, body: 'Closes #40', labels: [], user: { login: 'somebody' }, head: { ref: 'fix/40-typo', sha: PUSHED } }, // not the factory's
    { ...PULL, number: 64, head: { ref: 'ci/50-broken', sha: OLD } }];
  const closed = { ...PULL, number: 58, state: 'closed', head: { ref: 'ci/38-worktree-state', sha: OLD } };
  const answers = {
    [`repos/${repo}/pulls?state=open&per_page=100`]: [open],
    [`repos/${repo}/pulls/61/files?per_page=100`]: [[{ filename: 'docs/spec/factory/.github/factory/inflight.mjs' }]],
    [`repos/${repo}/pulls/61/reviews?per_page=100`]: [[]],
    [`repos/${repo}/commits/${OLD}/check-runs?filter=all&per_page=100`]: [{ check_runs: REVIEW_FAILED }],
    [`repos/${repo}/commits/${OLD}/statuses?per_page=100`]: [[]],
    [`repos/${repo}/issues/61/comments?per_page=100`]: [[by('yaf-reviewer[bot]', SUMMARY)]],
    [`repos/${repo}/issues/39/comments?per_page=100`]: [[READY]],
    [`repos/${repo}/issues/39`]: { number: 39, labels: [{ name: 'factory' }] },
    [`repos/${repo}/pulls?state=all&head=acme:ci/39-inflight-list&per_page=100`]: [[open[0]]],
    [`repos/${repo}/issues/38/comments?per_page=100`]: [[]],
    [`repos/${repo}/issues/38`]: { number: 38, labels: [] },
    [`repos/${repo}/pulls?state=all&head=acme:ci/38-worktree-state&per_page=100`]: [[closed, { ...closed, number: 57, merged_at: '2026-11-20T07:31:37Z' }]],
    [`repos/${repo}/issues/40/comments?per_page=100`]: [[by(policy.bots.factory, marker('local-review', PUSHED, 'ready round=0'))]],
    [`repos/${repo}/issues/40`]: { number: 40, labels: [{ name: 'factory' }] },
    [`repos/${repo}/pulls?state=all&head=acme:fix/40-typo&per_page=100`]: [[open[1]]],
    [`repos/${repo}/pulls?state=all&head=acme:ci/999-gone&per_page=100`]: [[]],
  };
  const calls = [];
  const missing = [];
  const gh = (...args) => {
    calls.push(['gh', ...args]);
    if (args[1] in answers) return JSON.stringify(answers[args[1]]);
    missing.push(args[1]);
    throw new Error('gh: Not Found (HTTP 404)');
  };
  const git = (...args) => {
    calls.push(['git', ...args]);
    if (args[0] === 'worktree') return porcelain;
    if (args[0] === 'ls-remote') return `${OLD}\trefs/heads/ci/39-inflight-list\n${PUSHED}\trefs/heads/fix/40-typo\n${'b'.repeat(40)}\trefs/heads/main\n`;
    if (args.join(' ') === `merge-base --is-ancestor ${OLD} ${HEAD}`) return '';
    throw new Error(`not expected: git ${args.join(' ')}`);
  };
  const out = inflight({ repo, policy, gh, git, isHuman: () => false, now });
  // #61: the fix is committed in the worktree and not pushed, so the pull request waits and the worktree owes the push.
  // A pull request or a worktree whose calls fail is a row of its own and stops nothing.
  assert.deepEqual(out.pullRequests, [{ ...row({ pr: open[0], ticketComments: [READY], worktree: { locked: false, ahead: true } }), action: 'wait', reason: 'a fix is committed and not pushed' },
    { number: 64, branch: 'ci/50-broken', action: 'failed', reason: 'gh: Not Found (HTTP 404)' }]);
  assert.deepEqual(out.worktrees.slice(0, 3).map((w) => [w.branch, w.issue, w.pullRequest, w.state]),
    [['ci/39-inflight-list', 39, 61, 'push-owed'], ['ci/38-worktree-state', 38, 57, 'remove'], ['fix/40-typo', 40, 62, 'in-flight']]);
  assert.deepEqual(out.worktrees[3], { path: 'C:/dev/yaf/.claude/worktrees/agent-a6', branch: 'ci/999-gone', state: 'failed', reason: 'gh: Not Found (HTTP 404)' });
  assert.equal(out.worktrees.length, 4);
  assert.deepEqual(missing, [`repos/${repo}/pulls/64/files?per_page=100`, `repos/${repo}/issues/999`]); // nothing else was asked for and not answered
  for (const [tool, ...args] of calls) {
    if (tool === 'gh') assert.ok(args[0] === 'api' && !args.some((a) => ['--method', '-X', '--input', '-f', '-F'].includes(a)), `a write: gh ${args.join(' ')}`);
    else assert.ok(['worktree list --porcelain', 'ls-remote --heads origin'].includes(args.join(' ')) || args[0] === 'merge-base', `not a read: git ${args.join(' ')}`);
  }
});
