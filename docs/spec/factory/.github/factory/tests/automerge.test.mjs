import test from 'node:test';
import assert from 'node:assert/strict';
import { loadPolicy, makeHumanCheck } from '../lib.mjs';
import { approvingHuman } from '../human-approval.mjs';
import { armDecision, planGateConclusion, automerge } from '../automerge.mjs';

const policy = { ...loadPolicy(), humans: { team: 'maintainers', logins: ['alice'] } };
const isHuman = makeHumanCheck(policy);
const AUTHOR = 'yaf-factory[bot]';
const review = (login, type, state, commit_id) => ({ user: { login, type }, state, commit_id });
const approval = review('alice', 'User', 'APPROVED', 'h2');
// The approver is established as the command part does: from the reviews, with the policy's human check, on head h2.
const decide = (labels, reviews = [], extra = {}) => armDecision({
  labels, draft: false, kind: 'code', planGate: null, armed: false, paused: false, gatesConclusion: 'success',
  approver: approvingHuman(reviews, { author: AUTHOR, headSha: 'h2', isHuman }), ...extra,
});
const action = (...args) => decide(...args).action;

test('tier high is armed only with a human\'s approval of the head commit', () => {
  assert.equal(action(['tier:high'], [approval]), 'arm');
  assert.equal(action(['tier:high']), 'none');
  assert.match(decide(['tier:high']).reason, /no approving review by a human maintainer/);
});

test('a tier-high plan is not armed on its green human-approval check', () => {
  // Unflagged: factory/human-approval and factory/plan-gate are both green without anybody's review.
  assert.equal(action(['tier:high'], [], { kind: 'plan', planGate: 'success' }), 'none');
  assert.equal(action(['tier:high'], [approval], { kind: 'plan', planGate: 'success' }), 'arm');
});

test('a flagged tier-high plan needs the plan gate too', () => {
  assert.equal(action(['tier:high'], [approval], { kind: 'plan', planGate: 'failure' }), 'none');
  assert.equal(action(['tier:high'], [approval], { kind: 'plan', planGate: null }), 'none');
  assert.equal(action(['tier:high'], [approval], { kind: 'plan', planGate: 'failure', armed: true }), 'disarm');
  assert.equal(action(['tier:high'], [approval], { kind: 'code', planGate: 'failure' }), 'arm'); // the gate is a plan's
});

test('a stale approval, a bot\'s and the author\'s do not count', () => {
  assert.equal(action(['tier:high'], [review('alice', 'User', 'APPROVED', 'h1')]), 'none');
  assert.equal(action(['tier:high'], [review('yaf-reviewer[bot]', 'Bot', 'APPROVED', 'h2')]), 'none');
  assert.equal(action(['tier:high'], [review(AUTHOR, 'Bot', 'APPROVED', 'h2')]), 'none');
  assert.equal(action(['tier:high'], [review('mallory', 'User', 'APPROVED', 'h2')]), 'none'); // not a maintainer
  assert.equal(action(['tier:high'], [approval, review('alice', 'User', 'CHANGES_REQUESTED', 'h2')]), 'none');
  assert.equal(action(['tier:high'], [review('alice', 'User', 'APPROVED', 'h1')], { armed: true }), 'disarm');
});

test('the highest tier label wins', () => {
  assert.equal(action(['tier:low', 'tier:high']), 'none');
  assert.equal(action(['tier:high', 'tier:medium', 'tier:low'], [], { armed: true }), 'disarm');
  assert.equal(action(['tier:low', 'tier:medium']), 'arm');
});

test('an unapproved armed tier-high pull request is disarmed', () => {
  assert.equal(action(['tier:high'], [], { armed: true }), 'disarm'); // also one armed by hand
  assert.equal(action(['tier:high'], [], { armed: false }), 'none');
  assert.equal(action(['tier:high'], [approval], { armed: true }), 'none'); // armed and approved: left alone
});

test('needs-human and drafts are never armed', () => {
  assert.equal(action(['tier:low', 'needs-human']), 'none');
  assert.equal(action(['tier:high', 'needs-human'], [approval]), 'none');
  assert.equal(action(['tier:high', 'needs-human'], [approval], { armed: true }), 'disarm');
  assert.equal(action(['tier:medium', 'needs-human'], [], { armed: true }), 'disarm');
  assert.equal(action(['needs-human'], [], { armed: true }), 'disarm');
  assert.equal(action(['tier:low'], [], { draft: true }), 'none');
  assert.equal(action(['tier:high'], [approval], { draft: true }), 'none');
});

test('tier low and medium are armed without an approval; no tier label is none', () => {
  assert.equal(action(['tier:low']), 'arm');
  assert.equal(action(['factory', 'tier:medium']), 'arm');
  assert.equal(action(['tier:low'], [], { armed: true }), 'none');
  assert.equal(action(['factory']), 'none');
  assert.equal(action([], [approval]), 'none');
});

test('paused: disarm still happens, arm does not', () => {
  assert.equal(action(['tier:low'], [], { paused: true }), 'none');
  assert.equal(action(['tier:high'], [approval], { paused: true }), 'none');
  assert.match(decide(['tier:high'], [approval], { paused: true }).reason, /paused/);
  assert.equal(action(['tier:high'], [], { paused: true, armed: true }), 'disarm');
  // the same for a gates run that did not succeed
  assert.equal(action(['tier:medium'], [], { gatesConclusion: 'failure' }), 'none');
  assert.equal(action(['tier:high'], [approval], { gatesConclusion: 'cancelled' }), 'none');
  assert.equal(action(['tier:high'], [], { gatesConclusion: 'failure', armed: true }), 'disarm');
  assert.equal(action(['needs-human', 'tier:low'], [], { gatesConclusion: undefined, paused: true, armed: true }), 'disarm');
});

const run = (slug, conclusion, started_at, name = 'factory/plan-gate', id = 1) => ({ id, name, conclusion, started_at, app: { slug } });

test('a plan gate from another app does not count', () => {
  const gates = policy.bots.gates; // github-actions[bot] → app slug github-actions
  assert.equal(planGateConclusion([run('github-actions', 'failure', '2026-11-20T10:00:00Z'), run('yaf-reviewer', 'success', '2026-11-20T11:00:00Z')], gates), 'failure');
  assert.equal(planGateConclusion([run('yaf-reviewer', 'success', '2026-11-20T11:00:00Z')], gates), null);
  assert.equal(planGateConclusion([run('github-actions', 'success', '2026-11-20T11:00:00Z', 'factory/tier')], gates), null);
  // the newest run of the gates' app counts, whatever the order; one still running has no conclusion
  assert.equal(planGateConclusion([run('github-actions', 'success', '2026-11-20T11:00:00Z'), run('github-actions', 'failure', '2026-11-20T10:00:00Z')], gates), 'success');
  assert.equal(planGateConclusion([run('github-actions', 'success', '2026-11-20T10:00:00Z'), run('github-actions', null, '2026-11-20T11:00:00Z')], gates), null);
  // the same start time: the highest id is the newest
  const tie = (conclusion, id) => run('github-actions', conclusion, '2026-11-20T11:00:00Z', 'factory/plan-gate', id);
  assert.equal(planGateConclusion([tie('failure', 9), tie('success', 8)], gates), 'failure');
  assert.equal(planGateConclusion([tie('success', 8), tie('failure', 9)], gates), 'failure');
});

// The command part, with gh injected: answers by endpoint, records every call.
// listed: further pull requests the commit's list returns. fetchedHead: the head the single read returns (a push in between).
function fakeGh({ labels, reviews = [], files = ['src/Core/A.cs'], checkRuns = [], autoMerge = null, head = 'h2', fetchedHead = head, listed = [] }) {
  const pr = { number: 7, state: 'open', draft: false, head: { sha: head }, user: { login: AUTHOR }, labels: labels.map((name) => ({ name })), auto_merge: autoMerge };
  const calls = [];
  const gh = (...args) => {
    calls.push(args.join(' '));
    if (args[0] !== 'api') return '';
    const endpoint = args[1];
    if (endpoint.endsWith('/commits/h2/pulls')) return JSON.stringify([[pr, ...listed]]);
    if (endpoint.endsWith('/pulls/7')) return JSON.stringify({ ...pr, head: { sha: fetchedHead } });
    const other = listed.find((p) => endpoint.endsWith(`/pulls/${p.number}`));
    if (other) return JSON.stringify(other);
    if (endpoint.includes('/reviews')) return JSON.stringify([reviews]);
    if (endpoint.includes('/files')) return JSON.stringify([files.map((filename) => ({ filename }))]);
    if (endpoint.includes('/commits/h2/check-runs')) return JSON.stringify([{ check_runs: checkRuns }]);
    throw new Error(`unexpected: ${endpoint}`);
  };
  return { gh, merges: () => calls.filter((c) => c.startsWith('pr merge')) };
}
const command = (state, extra = {}) => {
  const fake = fakeGh(state);
  const decisions = automerge({ repo: 'o/r', headSha: 'h2', paused: false, gatesConclusion: 'success', policy, gh: fake.gh, log: () => {}, ...extra });
  return { decisions, merges: fake.merges() };
};

test('the command arms the head commit it decided on, disarms, or calls nothing', () => {
  const ARM = 'pr merge 7 --repo o/r --auto --squash --match-head-commit h2';
  assert.deepEqual(command({ labels: ['tier:high'], reviews: [approval] }).merges, [ARM]);
  assert.deepEqual(command({ labels: ['tier:low'] }).merges, [ARM]);
  assert.deepEqual(command({ labels: ['tier:high'] }).merges, []);
  assert.deepEqual(command({ labels: ['tier:high'], reviews: [review('alice', 'User', 'APPROVED', 'h1')], autoMerge: { merge_method: 'squash' } }, { paused: true, gatesConclusion: 'failure' }).merges, ['pr merge 7 --repo o/r --disable-auto']);
  assert.deepEqual(command({ labels: ['tier:high'], reviews: [approval] }, { paused: true }).merges, []);
  // a plan: the gate must be the gates app's
  const plan = { labels: ['tier:high'], reviews: [approval], files: ['specs/0009-x/plan.md'] };
  assert.deepEqual(command({ ...plan, checkRuns: [run('yaf-reviewer', 'success', '2026-11-20T11:00:00Z')] }).merges, []);
  assert.deepEqual(command({ ...plan, checkRuns: [run('github-actions', 'success', '2026-11-20T11:00:00Z')] }).merges, [ARM]);
  // no open pull request with that head: nothing is read further and nothing is called
  assert.deepEqual(command({ labels: ['tier:low'], head: 'h3' }).decisions, []);
});

test('a push between the list and the read is not armed', () => {
  const moved = command({ labels: ['tier:high'], reviews: [approval], fetchedHead: 'h3' });
  assert.deepEqual(moved.merges, []);
  assert.deepEqual(moved.decisions.map((d) => d.action), ['none']);
  assert.match(moved.decisions[0].reason, /no longer h2/);
  assert.deepEqual(command({ labels: ['tier:low'], fetchedHead: 'h3' }).merges, []);
});

test('every open pull request on the head commit is decided on its own', () => {
  // #7 is tier low; #8 has the same head (say, to next), is tier high, unapproved and armed by hand
  const second = { number: 8, state: 'open', draft: false, head: { sha: 'h2' }, user: { login: AUTHOR }, labels: [{ name: 'tier:high' }], auto_merge: { merge_method: 'squash' } };
  const both = command({ labels: ['tier:low'], listed: [second, { ...second, number: 9, state: 'closed' }] });
  assert.deepEqual(both.decisions.map((d) => [d.number, d.action]), [[7, 'arm'], [8, 'disarm']]);
  assert.deepEqual(both.merges, ['pr merge 7 --repo o/r --auto --squash --match-head-commit h2', 'pr merge 8 --repo o/r --disable-auto']);
});
