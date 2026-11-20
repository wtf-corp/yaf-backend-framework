import test from 'node:test';
import assert from 'node:assert/strict';
import { loadPolicy } from '../lib.mjs';
import { nextAction } from '../inflight.mjs';

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
