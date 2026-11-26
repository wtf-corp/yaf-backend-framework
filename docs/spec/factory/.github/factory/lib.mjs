// Shared helpers for the factory scripts. Node 22+, no dependencies.
// The scripts are always run from a checkout of the BASE branch, so the policy
// next to this file is the base branch's policy, never the pull request's.
import { readFileSync, appendFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import path from 'node:path';

const here = import.meta.dirname;

export function loadPolicy(file = path.join(here, 'policy.json')) {
  return JSON.parse(readFileSync(file, 'utf8'));
}

/** True when the repository-relative path matches at least one glob. */
export function matchesAny(file, globs = []) {
  const p = file.replaceAll('\\', '/');
  return globs.some((g) => path.posix.matchesGlob(p, g));
}

export function run(cmd, args, options = {}) {
  return execFileSync(cmd, args, { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024, ...options });
}

export function git(dir, ...args) {
  return run('git', ['-C', dir, ...args]);
}

/** Calls the GitHub REST API through gh. `body` is sent as JSON when given. */
export function api(endpoint, { method = 'GET', body, paginate = false } = {}) {
  const args = ['api', endpoint, '--method', method, '-H', 'Accept: application/vnd.github+json'];
  if (paginate) args.push('--paginate', '--slurp');
  if (body !== undefined) args.push('--input', '-');
  const out = run('gh', args, body !== undefined ? { input: JSON.stringify(body) } : {});
  if (!out.trim()) return null;
  const parsed = JSON.parse(out);
  return paginate ? parsed.flat() : parsed;
}

export function repoSlug() {
  const slug = process.env.GITHUB_REPOSITORY;
  if (!slug) throw new Error('GITHUB_REPOSITORY is not set');
  return slug;
}

export function readEvent() {
  return JSON.parse(readFileSync(process.env.GITHUB_EVENT_PATH, 'utf8'));
}

/**
 * Reports a check run on a commit. Required status checks are matched by this name.
 * conclusion: success | failure | neutral
 */
export function setCheck(sha, name, conclusion, title, summary = '') {
  api(`repos/${repoSlug()}/check-runs`, {
    method: 'POST',
    body: {
      name,
      head_sha: sha,
      status: 'completed',
      conclusion,
      output: { title: title.slice(0, 250), summary: summary.slice(0, 60000) || title },
    },
  });
  console.log(`${conclusion === 'success' ? 'PASS' : conclusion === 'failure' ? 'FAIL' : 'NOTE'} ${name}: ${title}`);
}

/**
 * The body of a commit status. conclusion: success | failure. The REST page states no limit for `description`;
 * it is cut to 140 characters as a precaution. `target_url` is left out when the run is unknown.
 */
export function statusBody(name, conclusion, title, targetUrl) {
  const body = { context: name, state: conclusion, description: title.slice(0, 140) };
  if (targetUrl) body.target_url = targetUrl;
  return body;
}

/**
 * Reports a commit status on a commit, for the gate checks. A status belongs to no check suite, so a later
 * workflow run on the same commit cannot hide it (issue #278); the latest status of a context counts.
 * The markdown summary, which a status cannot hold, goes into the job summary that `target_url` links to.
 */
export function setStatus(sha, name, conclusion, title, summary = '') {
  const { GITHUB_SERVER_URL: server, GITHUB_REPOSITORY: slug, GITHUB_RUN_ID: runId } = process.env;
  const targetUrl = server && slug && runId ? `${server}/${slug}/actions/runs/${runId}` : undefined;
  api(`repos/${repoSlug()}/statuses/${sha}`, { method: 'POST', body: statusBody(name, conclusion, title, targetUrl) });
  if (summary) writeSummary(`#### ${name}: ${title}\n\n${summary}\n`);
  console.log(`${conclusion === 'success' ? 'PASS' : 'FAIL'} ${name}: ${title}`);
}

/**
 * The body of a check run that is in progress: what a running review reports on the head commit before its
 * verdict. It has no conclusion; the completed run reported afterwards is newer, and the newest of a name counts.
 * It has no start time either: GitHub stamps it, as it stamps the completed run, so their order needs no local clock.
 */
export function inProgressCheck(name, sha, title) {
  return { name, head_sha: sha, status: 'in_progress', output: { title, summary: title } };
}

export function writeSummary(markdown) {
  if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, markdown + '\n');
}

export function setOutput(name, value) {
  if (process.env.GITHUB_OUTPUT) appendFileSync(process.env.GITHUB_OUTPUT, `${name}=${value}\n`);
}

/** Parses `git diff --numstat` output into [{ file, added, deleted }]. Binary files count as 0. */
export function parseNumstat(text) {
  return text
    .split('\n')
    .filter(Boolean)
    .map((line) => {
      const [a, d, ...rest] = line.split('\t');
      let file = rest.join('\t');
      const rename = file.match(/^(.*)\{(.*) => (.*)\}(.*)$/);
      if (rename) file = (rename[1] + rename[3] + rename[4]).replaceAll('//', '/');
      else if (file.includes(' => ')) file = file.split(' => ')[1];
      return { file, added: a === '-' ? 0 : Number(a), deleted: d === '-' ? 0 : Number(d) };
    });
}

/**
 * Splits a unified diff into per-file added and removed lines.
 * Returns Map<file, { added: string[], removed: string[] }>.
 */
export function parseDiff(text) {
  const files = new Map();
  let current = null;
  for (const line of text.split('\n')) {
    if (line.startsWith('diff --git ')) {
      current = null;
    } else if (line.startsWith('+++ ')) {
      const name = line.slice(4).trim();
      if (name !== '/dev/null') {
        current = { added: [], removed: [] };
        files.set(name.replace(/^b\//, ''), current);
      }
    } else if (line.startsWith('--- ')) {
      const name = line.slice(4).trim();
      if (name !== '/dev/null') {
        const key = name.replace(/^a\//, '');
        current = files.get(key) ?? { added: [], removed: [] };
        files.set(key, current);
      }
    } else if (current && line.startsWith('+')) {
      current.added.push(line.slice(1));
    } else if (current && line.startsWith('-')) {
      current.removed.push(line.slice(1));
    }
  }
  return files;
}

/** Conventional Commit title → { type, scope, breaking, subject } or null. */
export function parseTitle(title = '') {
  const m = title.match(/^(\w+)(?:\(([^)]+)\))?(!)?:\s+(.+)$/);
  if (!m) return null;
  return { type: m[1], scope: m[2] ?? null, breaking: Boolean(m[3]), subject: m[4] };
}

/** A user is a human maintainer when it is a User account on the maintainers list or team. */
export function makeHumanCheck(policy, lookupTeamMember = defaultTeamLookup) {
  const cache = new Map();
  return (actor) => {
    if (!actor || actor.type !== 'User') return false;
    if (policy.humans.logins.length > 0) return policy.humans.logins.includes(actor.login);
    if (!cache.has(actor.login)) cache.set(actor.login, lookupTeamMember(policy.humans.team, actor.login));
    return cache.get(actor.login);
  };
}

function defaultTeamLookup(team, login) {
  const org = repoSlug().split('/')[0];
  try {
    const m = api(`orgs/${org}/teams/${team}/memberships/${login}`);
    return m?.state === 'active';
  } catch {
    return false; // not a member, or the token cannot read teams: fail closed
  }
}

/**
 * From issue timeline events, the actor who last applied `label` while it is still applied,
 * or null when the label is not currently applied.
 */
export function lastLabelActor(timeline, label) {
  let actor = null;
  for (const e of timeline) {
    if (e.label?.name !== label) continue;
    if (e.event === 'labeled') actor = e.actor ?? null;
    if (e.event === 'unlabeled') actor = null;
  }
  return actor;
}
