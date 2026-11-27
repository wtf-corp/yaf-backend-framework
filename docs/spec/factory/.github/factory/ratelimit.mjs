// GitHub's rate limits, for the scripts that make many calls (ticket creation, the Project).
// The rules are GitHub's ("Rate limits for the REST API", "Best practices for using the REST API"):
//   - a primary or secondary limit answers 403 or 429;
//   - with `retry-after`, wait that many seconds;
//   - with `x-ratelimit-remaining: 0`, wait until `x-ratelimit-reset` (UTC epoch seconds);
//   - otherwise wait a minute, doubling on each further failure;
//   - many writes: at least one second apart.
// `gh api -i` writes the status line and the headers also when it fails, so a failed call can be
// read. `gh project` prints no headers: after its "API rate limit exceeded", `GET /rate_limit`
// (`resources.graphql`) tells whether to wait for the reset. Clock and sleep are parameters so
// that a test passes fakes.
import { run as runCommand } from './lib.mjs';

const MINUTE = 60_000;
const BUDGET = 30 * MINUTE;

function sleepSync(ms) {
  Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms);
}

/**
 * Reads the text `gh api -i` writes: `{ status, headers, body }`, header names in lower case, the
 * body parsed as JSON when it is JSON (null when empty). Null when the text has no status line.
 */
export function parseIncluded(text) {
  const lines = String(text ?? '').split(/\r?\n/);
  const status = /^HTTP\/\S+ (\d{3})\b/.exec(lines[0]);
  if (!status) return null;
  const headers = {};
  let i = 1;
  for (; i < lines.length && lines[i] !== ''; i++) {
    const colon = lines[i].indexOf(':');
    if (colon > 0) headers[lines[i].slice(0, colon).trim().toLowerCase()] = lines[i].slice(colon + 1).trim();
  }
  const raw = lines.slice(i + 1).join('\n').trim();
  let body = null;
  if (raw) {
    try { body = JSON.parse(raw); } catch { body = raw; }
  }
  return { status: Number(status[1]), headers, body };
}

function untilReset(resetSeconds, now) {
  return Math.max(0, Number(resetSeconds) * 1000 - now());
}

const backoff = (attempt) => MINUTE * 2 ** attempt;

/**
 * Milliseconds to wait before retrying a parsed response, or null when it is not a rate limit.
 * `attempt` counts the earlier retries of the same call (0 for the first).
 */
export function waitFor(response, { now = Date.now, attempt = 0 } = {}) {
  if (!response || (response.status !== 403 && response.status !== 429)) return null;
  const h = response.headers ?? {};
  if (h['retry-after'] !== undefined) return Number(h['retry-after']) * 1000;
  if (h['x-ratelimit-remaining'] === '0' && h['x-ratelimit-reset'] !== undefined) return untilReset(h['x-ratelimit-reset'], now);
  const message = typeof response.body === 'object' ? response.body?.message : response.body;
  if (response.status === 403 && !/rate limit/i.test(String(message ?? ''))) return null;
  return backoff(attempt);
}

function readRateLimit() {
  return JSON.parse(runCommand('gh', ['api', 'rate_limit']));
}

/**
 * Milliseconds to wait before retrying a call that threw, or null when the error is not a rate limit:
 * from the output of `gh api -i` when the error has it, else from the message ("API rate limit
 * exceeded", as `gh project` reports it) and `GET /rate_limit`.
 */
export function errorWait(error, attempt, { now = Date.now, rateLimit = readRateLimit } = {}) {
  const response = parseIncluded(error?.stdout);
  if (response) return waitFor(response, { now, attempt });
  if (!/rate limit/i.test(String(error?.message ?? ''))) return null;
  let graphql;
  try { graphql = rateLimit()?.resources?.graphql; } catch { graphql = null; }
  if (graphql?.remaining === 0) return untilReset(graphql.reset, now);
  return backoff(attempt);
}

/**
 * Calls `call` and retries it while it fails on a rate limit, waiting as `errorWait` says. When the
 * next wait would take the total past `budgetMs`, the last error is thrown.
 */
export function withRetry(call, { now = Date.now, sleep = sleepSync, budgetMs = BUDGET, rateLimit = readRateLimit } = {}) {
  let waited = 0;
  for (let attempt = 0; ; attempt++) {
    try {
      return call();
    } catch (e) {
      const ms = errorWait(e, attempt, { now, rateLimit });
      if (ms === null || waited + ms > budgetMs) throw e;
      sleep(ms);
      waited += ms;
    }
  }
}

/** Wraps a write so that two calls of it start at least `gapMs` after the previous one ended. */
export function paced(write, { now = Date.now, sleep = sleepSync, gapMs = 1000 } = {}) {
  let last = -Infinity;
  return (...args) => {
    const wait = last + gapMs - now();
    if (wait > 0) sleep(wait);
    try {
      return write(...args);
    } finally {
      last = now();
    }
  };
}

/** `gh api -i <args>` with the retry of `withRetry`; returns the parsed body. */
export function ghApi(args, { run = runCommand, ...options } = {}) {
  return withRetry(() => {
    const response = parseIncluded(run('gh', ['api', '-i', ...args]));
    if (!response) throw new Error(`gh api -i ${args.join(' ')} wrote no status line`);
    return response.body;
  }, options);
}
