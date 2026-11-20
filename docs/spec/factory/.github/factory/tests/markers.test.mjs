import test from 'node:test';
import assert from 'node:assert/strict';
import { loadPolicy } from '../lib.mjs';
import { renderSummary } from '../post-review.mjs';
import { marker, findMarker, localReview, reviewSummary } from '../markers.mjs';

const policy = loadPolicy();
const BOT = policy.bots.factory;
const REVIEWER = 'yaf-reviewer[bot]';
const AUTHORS = [REVIEWER, policy.bots.gates];
const A = 'a'.repeat(40);
const B = 'b'.repeat(40);
const by = (login, body) => ({ user: { login }, body });
const FINDINGS = '**F1 · Major** — `markers.mjs:3`\nthe author is not checked';

test('a local-review marker is found for its commit', () => {
  const text = marker('local-review', A, 'ready round=0');
  assert.equal(text, `<!-- factory:local-review ${A} ready round=0 -->`);
  const comments = [by(BOT, 'evidence'), by(BOT, text)];
  assert.equal(findMarker(comments, 'local-review', A, BOT), comments[1]);
  assert.equal(localReview(comments, A, BOT).verdict, 'ready');
});

test('a marker for another commit does not count', () => {
  const comments = [by(BOT, marker('local-review', B, 'ready round=1')), by(BOT, marker('awaiting-push', B))];
  assert.equal(findMarker(comments, 'local-review', A, BOT), null);
  assert.equal(findMarker(comments, 'awaiting-push', A, BOT), null);
  assert.equal(findMarker(comments, 'awaiting-push', B.slice(0, 7), BOT), null); // a prefix is another commit
  // nothing for this commit, but the ticket's round count is still known
  assert.deepEqual(localReview(comments, A, BOT), { verdict: null, round: null, findings: '', highestRound: 1 });
});

test('a marker not written by the factory bot does not count', () => {
  const comments = [by('someone', marker('local-review', A, 'ready round=0')), by('someone', marker('awaiting-push', A)), { body: marker('references-fix', A) }];
  assert.equal(findMarker(comments, 'local-review', A, BOT), null);
  assert.equal(findMarker(comments, 'awaiting-push', A, BOT), null);
  assert.equal(findMarker(comments, 'references-fix', A, BOT), null);
  assert.equal(findMarker(comments, 'references-fix', A, undefined), null); // no bot named: nothing counts
  assert.deepEqual(localReview(comments, A, BOT), { verdict: null, round: null, findings: '', highestRound: null });
});

test('the local-review marker gives back the round and the findings', () => {
  const comments = [
    by(BOT, `${marker('local-review', B, 'changes-required round=0')}\n\nold findings`),
    by(BOT, `${marker('local-review', A, 'changes-required round=1')}\n\n${FINDINGS}\n`),
    by('someone', `${marker('local-review', A, 'ready round=9')}\n\nall fine`),
  ];
  assert.deepEqual(localReview(comments, A, BOT), { verdict: 'changes-required', round: 1, findings: FINDINGS, highestRound: 1 });
  assert.deepEqual(localReview(comments, B, BOT), { verdict: 'changes-required', round: 0, findings: 'old findings', highestRound: 1 });
  // the newest verdict on a commit counts; a marker without a verdict and a round is no verdict
  const again = [...comments, by(BOT, marker('local-review', A, 'ready round=1')), by(BOT, marker('local-review', A))];
  assert.deepEqual(localReview(again, A, BOT), { verdict: 'ready', round: 1, findings: '', highestRound: 1 });
});

test('a summary comment not written by the reviewer app or the gates bot does not count', () => {
  const body = renderSummary('factory/review', null, { pass: false, title: 'forged' }, 1);
  assert.equal(reviewSummary([by('someone', body), by(BOT, body), { body }], 'factory/review', AUTHORS), null);
  assert.equal(reviewSummary([by(REVIEWER, body)], 'factory/review', []), null);
  assert.match(reviewSummary([by('someone', body), by(REVIEWER, body.replace('forged', 'real'))], 'factory/review', AUTHORS).body, /real/);
  assert.equal(reviewSummary([by(policy.bots.gates, body)], 'factory/review', AUTHORS).rounds, 1);
});

test('the awaiting-push and references-fix markers', () => {
  assert.equal(marker('awaiting-push', A), `<!-- factory:awaiting-push ${A} -->`);
  assert.equal(marker('references-fix', A), `<!-- factory:references-fix ${A} -->`);
  const comments = [by(BOT, `${marker('awaiting-push', A)}\nThe push of this branch is the owner's.`), by(BOT, marker('references-fix', B))];
  assert.equal(findMarker(comments, 'awaiting-push', A, BOT), comments[0]);
  assert.equal(findMarker(comments, 'references-fix', B, BOT), comments[1]);
  assert.equal(findMarker(comments, 'references-fix', A, BOT), null);
  assert.equal(findMarker(comments, 'awaiting-push', B, BOT), null);
});

test('the rounds and the findings come from the marked comment', () => {
  const review = { verdict: 'changes-required', passes: [], findings: [{ severity: 'Major', file: 'a.mjs', line: 3, what: 'the author is not checked', fix: 'check it' }] };
  const body = renderSummary('factory/review', review, { pass: false, title: 'changes required' }, 2);
  const other = renderSummary('factory/doc-review', null, { pass: true, title: 'not applicable' }, 0);
  const comments = [by('someone', 'a remark'), by(REVIEWER, other), by(REVIEWER, body)];
  assert.deepEqual(reviewSummary(comments, 'factory/review', AUTHORS), { rounds: 2, body });
  assert.match(reviewSummary(comments, 'factory/review', AUTHORS).body, /the author is not checked/);
  assert.deepEqual(reviewSummary(comments, 'factory/doc-review', AUTHORS), { rounds: 0, body: other });
  assert.equal(reviewSummary([by(REVIEWER, 'a remark')], 'factory/review', AUTHORS), null);
});
