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
