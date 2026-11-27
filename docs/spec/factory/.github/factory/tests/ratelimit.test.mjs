import test from 'node:test';
import assert from 'node:assert/strict';
import { parseIncluded, waitFor, errorWait, withRetry, paced, ghApi } from '../ratelimit.mjs';

// What `gh api -i repos/<owner>/<repo>/issues/9999999` writes to standard output (research §3):
// the status line, the headers and the JSON body, also when it exits 1.
const notFound = [
  'HTTP/2.0 404 Not Found',
  'Content-Type: application/json; charset=utf-8\r',
  'X-Ratelimit-Limit: 5000\r',
  'X-Ratelimit-Remaining: 4990\r',
  'X-Ratelimit-Reset: 1795000000\r',
  'X-Ratelimit-Resource: core\r',
  'X-Ratelimit-Used: 10\r',
  '\r',
  '{"message":"Not Found","documentation_url":"https://docs.github.com/rest","status":"404"}',
].join('\n');

const response = (status, headers = {}, body = {}) => ({ status, headers, body });

/** A fake clock and sleep: sleeping moves the clock. */
function clock(start = 1_000_000) {
  let t = start;
  const sleeps = [];
  return { now: () => t, sleep: (ms) => { sleeps.push(ms); t += ms; }, tick: (ms) => { t += ms; }, sleeps };
}

/** An error as `execFileSync` throws it for a failed `gh api -i` call. */
function ghError(text, stderr = 'gh: error') {
  return Object.assign(new Error(`Command failed: gh api\n${stderr}`), { stdout: text, stderr });
}

function included(status, reason, headers, body) {
  const lines = [`HTTP/2.0 ${status} ${reason}`, ...Object.entries(headers).map(([k, v]) => `${k}: ${v}\r`), '\r', JSON.stringify(body)];
  return lines.join('\n');
}

test('parseIncluded_ErrorOutput_GivesStatusHeadersAndBody', () => {
  const r = parseIncluded(notFound);
  assert.equal(r.status, 404);
  assert.equal(r.headers['x-ratelimit-reset'], '1795000000');
  assert.equal(r.headers['x-ratelimit-resource'], 'core');
  assert.deepEqual(r.body, { message: 'Not Found', documentation_url: 'https://docs.github.com/rest', status: '404' });
});

test('parseIncluded_NoStatusLine_IsNull', () => {
  assert.equal(parseIncluded('gh: Not Found (HTTP 404)'), null);
  assert.equal(parseIncluded(undefined), null);
});

test('waitFor_RetryAfter_WaitsThoseSeconds', () => {
  const r = response(403, { 'retry-after': '7' }, { message: 'You have exceeded a secondary rate limit.' });
  assert.equal(waitFor(r, { now: () => 0, attempt: 0 }), 7000);
});

test('waitFor_RemainingZero_WaitsUntilTheReset', () => {
  const r = response(403, { 'x-ratelimit-remaining': '0', 'x-ratelimit-reset': '1030' }, { message: 'API rate limit exceeded for installation ID 1.' });
  assert.equal(waitFor(r, { now: () => 1_000_000, attempt: 0 }), 30_000);
  assert.equal(waitFor({ ...r, status: 429 }, { now: () => 1_000_000, attempt: 3 }), 30_000);
});

test('waitFor_SecondaryLimitWithoutHeaders_WaitsAMinuteThenDoubles', () => {
  const r = response(403, { 'x-ratelimit-remaining': '4000' }, { message: 'You have exceeded a secondary rate limit. Please wait a few minutes before you try again.' });
  assert.deepEqual([0, 1, 2].map((attempt) => waitFor(r, { now: () => 0, attempt })), [60_000, 120_000, 240_000]);
  assert.equal(waitFor(response(429), { now: () => 0, attempt: 0 }), 60_000);
});

test('waitFor_NotARateLimit_IsNull', () => {
  assert.equal(waitFor(parseIncluded(notFound), { now: () => 0, attempt: 0 }), null);
  assert.equal(waitFor(response(403, { 'x-ratelimit-remaining': '4000' }, { message: 'Resource not accessible by integration' }), { now: () => 0, attempt: 0 }), null);
});

test('errorWait_GraphQLRateLimit_ReadsTheRateLimitEndpoint', () => {
  const e = new Error('Command failed: gh project field-list 1\nGraphQL: API rate limit exceeded for installation ID 1.\n');
  const exhausted = () => ({ resources: { graphql: { remaining: 0, reset: 1030 } } });
  const left = () => ({ resources: { graphql: { remaining: 4000, reset: 1030 } } });
  assert.equal(errorWait(e, 0, { now: () => 1_000_000, rateLimit: exhausted }), 30_000);
  assert.equal(errorWait(e, 1, { now: () => 1_000_000, rateLimit: left }), 120_000);
  assert.equal(errorWait(new Error('GraphQL: Could not resolve to a ProjectV2'), 0, { now: () => 0, rateLimit: exhausted }), null);
});

test('withRetry_RateLimitOnce_WaitsAndRetries', () => {
  const c = clock();
  let calls = 0;
  const limited = ghError(included(403, 'Forbidden', { 'Retry-After': '7' }, { message: 'You have exceeded a secondary rate limit.' }));
  const result = withRetry(() => { if (calls++ === 0) throw limited; return 'ok'; }, { now: c.now, sleep: c.sleep });
  assert.equal(result, 'ok');
  assert.deepEqual(c.sleeps, [7000]);
});

test('withRetry_NotARateLimit_ThrowsAtOnce', () => {
  const c = clock();
  const e = ghError(notFound);
  assert.throws(() => withRetry(() => { throw e; }, { now: c.now, sleep: c.sleep }), (x) => x === e);
  assert.deepEqual(c.sleeps, []);
});

test('withRetry_BudgetUsedUp_ThrowsTheLastError', () => {
  const c = clock();
  const errors = [];
  const call = () => {
    const e = ghError(included(429, 'Too Many Requests', {}, { message: 'You have exceeded a secondary rate limit.' }));
    errors.push(e);
    throw e;
  };
  assert.throws(() => withRetry(call, { now: c.now, sleep: c.sleep, budgetMs: 5 * 60_000 }), (x) => x === errors.at(-1));
  assert.deepEqual(c.sleeps, [60_000, 120_000]); // the next wait, 240 s, would pass the 300 s budget
  assert.equal(errors.length, 3);
});

test('paced_TwoWrites_AreASecondApart', () => {
  const c = clock();
  const times = [];
  const write = paced((x) => { times.push(c.now()); return x * 2; }, { now: c.now, sleep: c.sleep });
  assert.equal(write(1), 2);
  c.tick(300);
  assert.equal(write(2), 4);
  assert.equal(times[1] - times[0], 1000);
  assert.deepEqual(c.sleeps, [700]);
  c.tick(5000);
  write(3);
  assert.deepEqual(c.sleeps, [700]); // a write long after the last one does not wait
});

test('ghApi_Success_ReturnsTheBodyOfTheIncludedOutput', () => {
  const seen = [];
  const run = (cmd, args) => { seen.push([cmd, ...args].join(' ')); return included(200, 'OK', { 'X-Ratelimit-Remaining': '4999' }, [{ number: 1 }]); };
  assert.deepEqual(ghApi(['repos/acme/app/issues?state=all&page=1'], { run }), [{ number: 1 }]);
  assert.deepEqual(seen, ['gh api -i repos/acme/app/issues?state=all&page=1']);
});
