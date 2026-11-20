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
const review = (login, state, commit_id = 'head') => ({ user: { login }, state, commit_id });

test('reviewers are requested when a human is needed and none has reviewed the head commit', () => {
  assert.deepEqual(ask(), ['alice', 'bob']);
  assert.deepEqual(ask({ reviews: [review('alice', 'CHANGES_REQUESTED', 'older')] }), ['alice', 'bob']);
  assert.deepEqual(ask({ reviews: [review('alice', 'COMMENTED'), review('bob', 'PENDING')] }), ['bob']);
  assert.deepEqual(ask({ needed: false }), []);
  assert.deepEqual(ask({ humans: { team: 'maintainers', logins: [] } }), []);
});

test('nobody is requested once a human approved', () => {
  assert.deepEqual(ask({ approver: 'alice', reviews: [review('alice', 'APPROVED')] }), []);
});

test('nobody is requested while a team\'s review is pending', () => {
  assert.deepEqual(ask({ requestedTeams: ['maintainers'] }), []);
});

test('the author is never requested', () => {
  assert.deepEqual(ask({ author: 'alice' }), ['bob']);
});

test('a reviewer already requested is left out', () => {
  assert.deepEqual(ask({ requestedUsers: ['bob'] }), ['alice']);
  assert.deepEqual(ask({ requestedUsers: ['alice', 'bob'] }), []);
});
