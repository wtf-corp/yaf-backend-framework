import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { loadPolicy } from '../lib.mjs';
import { verdictOf, renderSummary, heldForHuman, humanNeeded } from '../post-review.mjs';

const policy = loadPolicy();
const DOC = 'factory/doc-review';
const finding = (severity, extra = {}) => ({ severity, file: 'specs/0009-x/plan.md', line: 3, what: `${severity} thing`, fix: 'decide', ...extra });
const forOwner = (severity) => finding(severity, { for: 'owner', what: `${severity} question` });
const doc = (findings, extra = {}) => ({ verdict: 'pass', checklist: [{ item: '10', result: 'pass', note: '' }], findings, questions: [], ...extra });

const unflaggedPlan = '# Plan\n\n```factory:delta\n{ "publicApi": [], "schema": [], "dependencies": [] }\n```\n\n## Deviations from the specification\n\nNone.\n';
const flaggedPlan = unflaggedPlan.replace('None.', '1. Keys are files on disk.');

test('a document review whose only Major findings are for the owner passes', () => {
  const v = verdictOf(DOC, doc([forOwner('Major'), forOwner('Major'), forOwner('Blocker'), finding('Minor')]), true);
  assert.equal(v.pass, true);
  assert.equal(v.title, 'pass: no open Blocker or Major; 3 for the owner');
});

test('an owner finding fails when the plan is not flagged', () => {
  const review = doc([forOwner('Major')]);
  const v = verdictOf(DOC, review, false);
  assert.equal(v.pass, false);
  assert.match(v.title, /Major 1/);
  assert.match(v.title, /a finding for the owner on a plan that is not flagged/);
  assert.equal(verdictOf(DOC, review).pass, false); // the default is "not held"
});

test('held for a human: a spec is, a plan when a changed plan is flagged, unreadable files are not', () => {
  assert.equal(heldForHuman('spec', [], policy), true);
  assert.equal(heldForHuman('plan', [flaggedPlan], policy), true);
  assert.equal(heldForHuman('plan', [unflaggedPlan, flaggedPlan], policy), true);
  assert.equal(heldForHuman('plan', [unflaggedPlan], policy), false);
  assert.equal(heldForHuman('plan', [], policy), false);
  assert.equal(heldForHuman('plan', null, policy), false);
  assert.equal(heldForHuman('plan', [flaggedPlan, '```factory:delta\nnot json\n```\n'], policy), false);
  assert.equal(heldForHuman('code', [flaggedPlan], policy), false);
});

test('a request is needed only after a passed review of a tier-high or held pull request', () => {
  assert.equal(humanNeeded(true, ['factory', 'tier:high'], false), true);
  assert.equal(humanNeeded(true, ['tier:medium'], true), true);
  assert.equal(humanNeeded(true, ['tier:medium'], false), false);
  assert.equal(humanNeeded(true, [], false), false);
  assert.equal(humanNeeded(false, ['tier:high'], false), false);
  assert.equal(humanNeeded(false, ['tier:medium'], true), false);
});

test('a Major without the marker still fails', () => {
  assert.equal(verdictOf(DOC, doc([finding('Major')]), true).pass, false);
  assert.equal(verdictOf(DOC, doc([finding('Blocker')]), true).pass, false);
  const beside = verdictOf(DOC, doc([finding('Major'), forOwner('Major')]), true);
  assert.equal(beside.pass, false);
  assert.match(beside.title, /Major 1,/);
  assert.equal(verdictOf(DOC, doc([forOwner('Major')], { checklist: [{ item: '10', result: 'fail', note: '' }] }), true).pass, false);
  assert.equal(verdictOf(DOC, doc([forOwner('Major')], { verdict: 'changes-required' }), true).pass, false);
});

test('factory/review ignores the marker', () => {
  const review = { verdict: 'ready', passes: [{ pass: 'correctness', result: 'pass', notes: '' }], findings: [forOwner('Major')] };
  const v = verdictOf('factory/review', review, true);
  assert.equal(v.pass, false);
  assert.match(v.title, /Major 1/);
  assert.equal(verdictOf('factory/review', { ...review, findings: [finding('Major')] }, true).pass, false);
  assert.equal(verdictOf('factory/review', { ...review, findings: [forOwner('Minor')] }, true).pass, true);
});

test('the summary lists owner findings apart', () => {
  const review = doc([finding('Minor'), forOwner('Major')]);
  const [findings, owner] = renderSummary(DOC, review, verdictOf(DOC, review, true), 0).split('### For the owner');
  assert.match(findings, /### Findings/);
  assert.match(findings, /Minor thing/);
  assert.doesNotMatch(findings, /Major question/);
  assert.match(owner, /Major question/);
  assert.match(owner, /factory\/plan-gate/);

  // not held: the finding counts, so it stays under "Findings"
  const notHeld = renderSummary(DOC, review, verdictOf(DOC, review, false), 1);
  assert.doesNotMatch(notHeld, /### For the owner/);
  assert.match(notHeld, /Major question/);
});

test('the schema allows the marker', () => {
  const schema = JSON.parse(readFileSync(path.join(import.meta.dirname, '..', 'doc-review.schema.json'), 'utf8'));
  const item = schema.properties.findings.items;
  assert.deepEqual(item.properties.for, { type: 'string', enum: ['owner'] });
  assert.equal(item.required.includes('for'), false);
  assert.equal(item.additionalProperties, false);
});
