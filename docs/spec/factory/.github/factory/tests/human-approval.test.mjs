import test from 'node:test';
import assert from 'node:assert/strict';
import { approvalTier, humanApprovalVerdict } from '../human-approval.mjs';

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
