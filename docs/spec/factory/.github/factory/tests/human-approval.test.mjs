import test from 'node:test';
import assert from 'node:assert/strict';
import { approvalTier, humanApprovalVerdict, reviewersToRequest } from '../human-approval.mjs';

const verdict = (kind, tier, approver = null) => humanApprovalVerdict(approvalTier(kind, tier), approver);

test('a tier-high plan needs a human\'s approval', () => {
  assert.equal(approvalTier('plan', 'high'), 'high');
  assert.equal(verdict('plan', 'high').pass, false);
  assert.match(verdict('plan', 'high').title, /^tier high: waiting for an approving review/);
  assert.deepEqual(verdict('plan', 'high', 'alice'), { pass: true, title: 'approved by alice' });
});

test('a plan in tier low or medium does not', () => {
  for (const tier of ['low', 'medium']) {
    assert.equal(approvalTier('plan', tier), 'low');
    assert.equal(verdict('plan', tier).pass, true);
  }
});

test('code and spec are as before', () => {
  for (const kind of ['code', 'spec']) {
    for (const tier of ['low', 'medium', 'high']) assert.equal(approvalTier(kind, tier), tier);
    assert.equal(verdict(kind, 'high').pass, false);
    assert.equal(verdict(kind, 'high', 'alice').pass, true);
    assert.equal(verdict(kind, 'medium').pass, true);
  }
});

const humans = { team: 'maintainers', logins: ['alice', 'bob'] };
const ask = (over = {}) => reviewersToRequest({ needed: true, approver: null, author: 'yaf-factory[bot]', requestedUsers: [], requestedTeams: [], reviews: [], headSha: 'head', humans, ...over });
const review = (login, state, commit_id = 'head', type = 'User') => ({ user: { login, type }, state, commit_id });
const users = (reviewers) => ({ reviewers, teams: [] });
const nobody = users([]);
const noLogins = { team: 'maintainers', logins: [] };

test('reviewers are requested when a human is needed and none has reviewed the head commit', () => {
  assert.deepEqual(ask(), users(['alice', 'bob']));
  assert.deepEqual(ask({ reviews: [review('alice', 'CHANGES_REQUESTED', 'older')] }), users(['alice', 'bob']));
  assert.deepEqual(ask({ reviews: [review('alice', 'COMMENTED'), review('bob', 'PENDING')] }), users(['bob']));
  assert.deepEqual(ask({ needed: false }), nobody);
  assert.deepEqual(ask({ humans: { team: 'maintainers', logins: ['alice', 'bob', 'alice'] } }), users(['alice', 'bob']));
});

test('with no logins in the policy the team is requested, under the same conditions', () => {
  const team = { reviewers: [], teams: ['maintainers'] };
  assert.deepEqual(ask({ humans: noLogins }), team);
  assert.deepEqual(ask({ humans: noLogins, reviews: [review('carol', 'COMMENTED', 'older'), review('carol', 'PENDING')] }), team);
  assert.deepEqual(ask({ humans: noLogins, reviews: [review('yaf-factory[bot]', 'COMMENTED'), review('helper[bot]', 'COMMENTED', 'head', 'Bot')] }), team);
  assert.deepEqual(ask({ humans: noLogins, reviews: [review('carol', 'COMMENTED')] }), nobody);
  assert.deepEqual(ask({ humans: noLogins, needed: false }), nobody);
  assert.deepEqual(ask({ humans: noLogins, approver: 'carol' }), nobody);
  assert.deepEqual(ask({ humans: noLogins, requestedTeams: ['maintainers'] }), nobody);
});

test('nobody is requested once a human approved', () => {
  assert.deepEqual(ask({ approver: 'alice', reviews: [review('alice', 'APPROVED')] }), nobody);
});

test('nobody is requested while a team\'s review is pending', () => {
  assert.deepEqual(ask({ requestedTeams: ['maintainers'] }), nobody);
});

test('the author is never requested', () => {
  assert.deepEqual(ask({ author: 'alice' }), users(['bob']));
});

test('a reviewer already requested is left out', () => {
  assert.deepEqual(ask({ requestedUsers: ['bob'] }), users(['alice']));
  assert.deepEqual(ask({ requestedUsers: ['alice', 'bob'] }), nobody);
});
