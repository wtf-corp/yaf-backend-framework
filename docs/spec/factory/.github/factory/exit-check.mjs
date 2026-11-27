// Runs the exit checks a merged plan declares and reports them on the plan's epic.
//   node .github/factory/exit-check.mjs specs/NNNN-slug/plan.md (--list | --dry-run | --post)
// --list prints the criteria and checks and runs nothing; --dry-run runs the checks and prints the
// comment; --post runs them and posts the comment on the epic. Run as the factory app (GH_TOKEN).
// Only a plan on `main` supplies commands: the path is `specs/<folder>/plan.md`, tracked at origin/main,
// read with `git show`. The checks run from a clean checkout at origin/main, one after another, with
// `bash -c` and without the app's credential. Exit 0 only when every check gave its expected exit code,
// every ticket of the plan is closed and the ci.yml run of the commit passed; 1 otherwise; 2 when refused.
import { mkdtempSync, openSync, closeSync, readFileSync, rmSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { run as runCommand } from './lib.mjs';
import { ghApi } from './ratelimit.mjs';
import { indexByMarker, epicMarker } from './create-tickets.mjs';

const MODES = ['--list', '--dry-run', '--post'];
const TAIL = 40;
const PLAN_PATH = /^specs\/(?!\.{1,2}\/)[\w.-]+\/plan\.md$/;

/**
 * The fenced block whose info string is `factory:exit`, containing JSON:
 *   { "criteria": [{ "text": "…", "checks": ["E1"] }], "checks": [{ "id": "E1", "title": "…", "run": "…", "expect": 0 }] }
 * `title` defaults to the id, `expect` to 0. Returns null when the block is missing; throws when it is malformed.
 */
export function parseExitChecks(markdown) {
  const m = markdown.match(/```factory:exit[ \t]*\r?\n([\s\S]*?)\r?\n```/);
  if (!m) return null;
  const raw = JSON.parse(m[1]);
  if (!Array.isArray(raw.checks) || !raw.checks.length) throw new Error('factory:exit has no checks');
  if (!Array.isArray(raw.criteria) || !raw.criteria.length) throw new Error('factory:exit has no criteria');
  const checks = raw.checks.map((c) => {
    if (typeof c?.id !== 'string' || !c.id || typeof c.run !== 'string' || !c.run.trim()) throw new Error('factory:exit: every check needs an "id" and a "run" command');
    if (c.expect !== undefined && !Number.isInteger(c.expect)) throw new Error(`factory:exit: check ${c.id} has an "expect" that is not an integer`);
    return { id: c.id, title: String(c.title ?? c.id), run: c.run, expect: c.expect ?? 0 };
  });
  const ids = new Set();
  for (const c of checks) {
    if (ids.has(c.id)) throw new Error(`factory:exit: check ${c.id} is declared twice`);
    ids.add(c.id);
  }
  const criteria = raw.criteria.map((c) => {
    if (typeof c?.text !== 'string' || !Array.isArray(c.checks) || !c.checks.length) throw new Error('factory:exit: every criterion needs a "text" and at least one check');
    const unknown = c.checks.filter((id) => !ids.has(id));
    if (unknown.length) throw new Error(`factory:exit: criterion "${c.text}" names unknown checks: ${unknown.join(', ')}`);
    return { text: c.text, checks: c.checks.map(String) };
  });
  return { criteria, checks };
}

/** `env` without the app's credential: the variables `withoutToken` in factory-token.mjs strips, in any case. */
export function withoutToken(env) {
  const added = /^(GH_TOKEN|GITHUB_TOKEN|GIT_CONFIG_COUNT|GIT_CONFIG_(KEY|VALUE)_\d+)$/i;
  return Object.fromEntries(Object.entries(env).filter(([key]) => !added.test(key)));
}

/** Runs one command with `bash -c`; stdout and stderr go to one file, in the order written. → { status, output } */
function bash(command, { cwd, env }) {
  const dir = mkdtempSync(path.join(tmpdir(), 'exit-check-'));
  const file = path.join(dir, 'output.txt');
  const fd = openSync(file, 'w');
  try {
    const r = spawnSync('bash', ['-c', command], { cwd, env, stdio: ['ignore', fd, fd] });
    return { status: r.status ?? -1, output: readFileSync(file, 'utf8') + (r.error ? `\n${r.error.message}\n` : '') };
  } finally {
    closeSync(fd);
    rmSync(dir, { recursive: true, force: true });
  }
}

const cell = (text) => String(text).replaceAll('|', '\\|').replace(/\r?\n/g, ' ');
const code = (text) => (text.includes('`') ? `\`\` ${text} \`\`` : `\`${text}\``);
const seconds = (ms) => `${(ms / 1000).toFixed(1)} s`;
const passed = (r) => r.status === r.expect;
const ciPassed = (ci) => ci?.conclusion === 'success';

/** A code fence longer than any run of backticks in the text. */
function fenced(lines) {
  const longest = Math.max(2, ...lines.map((l) => Math.max(0, ...(l.match(/`+/g) ?? []).map((s) => s.length))));
  const f = '`'.repeat(longest + 1);
  return [f, ...lines.map((l) => l.slice(0, 500)), f].join('\n');
}

/** The comment posted on the epic, marked `<!-- factory:exit-check <sha> -->`. */
export function exitComment({ planFile, sha, startedAt, exit, results, openTickets, ci }) {
  const byId = new Map(results.map((r) => [r.id, r]));
  const ok = results.every(passed) && !openTickets.length && ciPassed(ci);
  return [
    `<!-- factory:exit-check ${sha} -->`,
    `## Exit check of \`${planFile}\` at ${sha.slice(0, 7)}`,
    '',
    `**Verdict: ${ok ? 'passed' : 'failed'}** · ${results.filter(passed).length} of ${results.length} checks as expected · ${openTickets.length} ticket(s) not closed · CI ${ciPassed(ci) ? 'passed' : 'not passed'} · started ${startedAt}`,
    '',
    '### Criteria',
    '',
    '| Criterion | Checks | Verdict |',
    '|---|---|---|',
    ...exit.criteria.map((c) => `| ${cell(c.text)} | ${c.checks.join(', ')} | ${c.checks.every((id) => passed(byId.get(id))) ? 'passed' : 'failed'} |`),
    '',
    '### Checks',
    '',
    '| Check | Command | Exit | Time |',
    '|---|---|---|---|',
    ...results.map((r) => `| ${cell(`${r.id} ${r.title}`)} | ${cell(code(r.run))} | ${r.status} (expected ${r.expect}) | ${seconds(r.ms)} |`),
    '',
    '### Tickets not closed',
    '',
    ...(openTickets.length ? openTickets.map((t) => `- #${t.number} ${t.title}`) : ['None.']),
    '',
    '### CI',
    '',
    ci ? `[ci.yml run](${ci.url}): ${ci.status}, ${ci.conclusion || 'no conclusion'}` : 'No ci.yml run for this commit.',
    '',
    '### Output (last lines)',
    '',
    ...results.flatMap((r) => [`<details><summary>${r.id} ${r.title}: exit ${r.status}</summary>`, '', fenced(r.tail), '', '</details>', '']),
  ].join('\n');
}

/** The plan's text at origin/main, or a refusal: only a tracked `specs/<folder>/plan.md` is read. */
function readPlan(planFile, git) {
  if (!PLAN_PATH.test(planFile)) throw new Error(`${planFile}: give a repository-relative path of the form specs/<folder>/plan.md`);
  if (git('ls-tree', '--name-only', 'origin/main', '--', planFile).trim() !== planFile) throw new Error(`${planFile}: not tracked at origin/main`);
  return git('show', `origin/main:${planFile}`);
}

function repoFromOrigin(git) {
  const url = git('remote', 'get-url', 'origin').trim();
  const m = url.match(/github\.com[/:]([\w.-]+\/[\w.-]+?)(?:\.git)?\/?$/);
  if (!m) throw new Error(`cannot read the repository from origin (${url})`);
  return m[1];
}

/** Every issue of the repository, page by page, with the retry of ratelimit.mjs. */
function listIssues(run, repo) {
  const all = [];
  for (let page = 1; ; page++) {
    const items = ghApi([`repos/${repo}/issues?state=all&per_page=100&page=${page}`], { run }) ?? [];
    all.push(...items);
    if (items.length < 100) return all;
  }
}

/**
 * Runs the exit check. `git(...args)`, `exec(command, { cwd, env })`, `run` (the `gh` runner), `env`, `repo`,
 * `now` and `log` are parameters so that a test passes fakes. Throws when refused; returns the exit code.
 */
export function exitCheck(argv, {
  git = (...args) => runCommand('git', args), exec = bash, run = runCommand, env = process.env,
  repo, now = Date.now, log = console.log,
} = {}) {
  const modes = argv.filter((a) => a.startsWith('--'));
  const files = argv.filter((a) => !a.startsWith('--'));
  if (modes.length !== 1 || !MODES.includes(modes[0]) || files.length !== 1) throw new Error(`usage: exit-check.mjs specs/<folder>/plan.md with exactly one of ${MODES.join(', ')}`);
  const [mode] = modes;
  const [planFile] = files;
  const exit = parseExitChecks(readPlan(planFile, git));
  if (!exit) throw new Error(`${planFile}: no factory:exit block at origin/main`);

  if (mode === '--list') {
    for (const c of exit.criteria) log(`${c.text}\n${c.checks.map((id) => exit.checks.find((k) => k.id === id)).map((k) => `  ${k.id} ${k.title}: ${k.run} (expect ${k.expect})`).join('\n')}`);
    return 0;
  }

  if (git('status', '--porcelain').trim()) throw new Error('the checkout is not clean: commit, stash or remove the changes first');
  const sha = git('rev-parse', 'HEAD').trim();
  if (sha !== git('rev-parse', 'origin/main').trim()) throw new Error('HEAD is not origin/main: check out origin/main first');
  const cwd = git('rev-parse', '--show-toplevel').trim();
  const clean = withoutToken(env);
  const startedAt = new Date(now()).toISOString();

  const results = exit.checks.map((c) => {
    log(`$ ${c.run}`);
    const start = now();
    const r = exec(c.run, { cwd, env: clean });
    const lines = r.output.replace(/\r?\n$/, '').split(/\r?\n/);
    return { ...c, status: r.status, ms: now() - start, tail: lines.slice(-TAIL) };
  });

  repo ??= repoFromOrigin(git);
  const folder = path.posix.dirname(planFile);
  const index = indexByMarker(listIssues(run, repo), folder);
  const epic = index.get(epicMarker(folder));
  const openTickets = [...index].filter(([marker, i]) => marker !== epicMarker(folder) && i.state === 'open').map(([, i]) => i).sort((a, b) => a.number - b.number);
  const ci = JSON.parse(run('gh', ['run', 'list', '-R', repo, '-w', 'ci.yml', '-c', sha, '-L', '1', '--json', 'status,conclusion,url']))[0] ?? null;

  const comment = exitComment({ planFile, sha, startedAt, exit, results, openTickets, ci });
  log(comment);
  if (mode === '--post') {
    if (!epic) throw new Error(`${planFile}: no epic found to post on`);
    ghApi([`repos/${repo}/issues/${epic.number}/comments`, '--method', 'POST', '--input', '-'], { run: (cmd, a) => run(cmd, a, { input: JSON.stringify({ body: comment }) }) });
    log(`posted on #${epic.number}`);
  }
  return results.every(passed) && !openTickets.length && ciPassed(ci) ? 0 : 1;
}

if (process.argv[1]?.endsWith('exit-check.mjs')) {
  try {
    process.exitCode = exitCheck(process.argv.slice(2));
  } catch (e) {
    console.error(e.message);
    process.exitCode = 2;
  }
}
