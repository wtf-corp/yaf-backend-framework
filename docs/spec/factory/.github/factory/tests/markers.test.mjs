import test from 'node:test';
import assert from 'node:assert/strict';
import { loadPolicy } from '../lib.mjs';
import { renderSummary } from '../post-review.mjs';
import { marker, findMarker, localReview, reviewSummary, stackedOn } from '../markers.mjs';

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
  const again = [...comments, by(BOT, marker('local-review', A, 'ready round=1')), by(BOT, `<!-- factory:local-review ${A} -->`)];
  assert.deepEqual(localReview(again, A, BOT), { verdict: 'ready', round: 1, findings: '', highestRound: 1 });
  assert.equal(localReview(again, A.slice(0, 7), BOT).verdict, null); // a prefix is another commit
});

test('a quoted marker does not count', () => {
  const quoted = (text) => by(BOT, `Evidence, quoting the record:\n\n${text}`);
  const comments = [quoted(marker('local-review', A, 'ready round=5')), quoted(marker('awaiting-push', A)), by(BOT, ` ${marker('references-fix', A)}`)];
  assert.equal(findMarker(comments, 'local-review', A, BOT), null);
  assert.equal(findMarker(comments, 'awaiting-push', A, BOT), null);
  assert.equal(findMarker(comments, 'references-fix', A, BOT), null);
  assert.deepEqual(localReview(comments, A, BOT), { verdict: null, round: null, findings: '', highestRound: null });
  // findings that quote a marker stay findings
  const quote = marker('local-review', A, 'ready round=7');
  const real = [by(BOT, `${marker('local-review', A, 'changes-required round=0')}\n${quote}`)];
  assert.deepEqual(localReview(real, A, BOT), { verdict: 'changes-required', round: 0, findings: quote, highestRound: 0 });
  const summary = renderSummary('factory/review', null, { pass: false, title: 'quoted' }, 3);
  assert.equal(reviewSummary([by(REVIEWER, `The last summary was:\n${summary}`)], 'factory/review', AUTHORS), null);
  const lateRounds = '<!-- factory:review-summary factory/review -->\ntext\n<!-- factory:rounds=4 -->';
  assert.equal(reviewSummary([by(REVIEWER, lateRounds)], 'factory/review', AUTHORS).rounds, 0); // a rounds line further down is text
});

test('findMarker and localReview agree on what a local-review marker is', () => {
  for (const value of ['', ' READY round=0', ' ready round=0 or so', ' ready', ' ready round=x']) {
    const comments = [by(BOT, `<!-- factory:local-review ${A}${value} -->`)];
    assert.equal(findMarker(comments, 'local-review', A, BOT), null, value);
    assert.equal(localReview(comments, A, BOT).verdict, null, value);
  }
});

test('the newest marker and the newest summary count', () => {
  const comments = [by(BOT, `${marker('awaiting-push', A)}\nfirst`), by(BOT, `${marker('awaiting-push', A)}\nsecond`)];
  assert.equal(findMarker(comments, 'awaiting-push', A, BOT), comments[1]);
  const summary = (rounds) => by(REVIEWER, renderSummary('factory/review', null, { pass: false, title: 'changes required' }, rounds));
  assert.equal(reviewSummary([summary(1), summary(2)], 'factory/review', AUTHORS).rounds, 2);
});

test('marker refuses a commit or a local-review value of another form', () => {
  for (const sha of [A.slice(0, 7), 'A'.repeat(40), `${A} `, 'g'.repeat(40), '', undefined]) assert.throws(() => marker('awaiting-push', sha), /not a full commit sha/);
  for (const value of [undefined, 'ready', 'READY round=0', 'ready round=0 -->', 'ready round=x', 'approved round=0']) assert.throws(() => marker('local-review', A, value), /not a local-review verdict/);
  assert.equal(marker('local-review', A, 'changes-required round=2'), `<!-- factory:local-review ${A} changes-required round=2 -->`);
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

test('stackedOn_NewestFactoryMarker_GivesTheCommitAndThePullRequest', () => {
  const text = marker('stacked-on', A, 'pr=61');
  assert.equal(text, `<!-- factory:stacked-on ${A} pr=61 -->`);
  for (const value of [undefined, '', 'pr=', 'pr=x', '61', 'pr=61 -->']) assert.throws(() => marker('stacked-on', A, value), /not a pull request/);
  const comments = [by(BOT, marker('stacked-on', B, 'pr=60')), by(BOT, `${text}\nStacked on #61.`), by('someone', marker('stacked-on', B, 'pr=7'))];
  assert.deepEqual(stackedOn(comments, BOT), { sha: A, pr: 61 }); // a marker by someone else does not count
  assert.deepEqual(stackedOn(comments.slice(0, 1), BOT), { sha: B, pr: 60 });
  assert.equal(stackedOn([by('someone', text)], BOT), null);
  assert.equal(stackedOn([by(BOT, text)], undefined), null);
  // a quoted marker, or one of another form, is text
  for (const body of [`Evidence:\n${text}`, `<!-- factory:stacked-on ${A} -->`, `<!-- factory:stacked-on ${A.slice(0, 7)} pr=61 -->`, `<!-- factory:stacked-on ${A} pr=x -->`]) {
    assert.equal(stackedOn([by(BOT, body)], BOT), null, body);
  }
  assert.equal(stackedOn([], BOT), null);
});
