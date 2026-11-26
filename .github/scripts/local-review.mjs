// TEMPORARY: the model reviews (checks factory/review and factory/doc-review) run on a maintainer's machine
// instead of in CI, until the factory-review environment has a working API account. Move them back to CI
// then: set repository variable FACTORY_REVIEW_IN_CI=true, point the two checks in the main ruleset back at
// GitHub Actions (integration_id 15368), and delete this script. See "Model reviews run locally" in AGENTS.md.
//
// It does what .github/workflows/factory-{pr,doc}-review.yml do in CI, with the same inputs:
//   - the BASE commit is checked out as the working directory, so the instructions, prompt, schema and policy
//     the reviewer gets are the reviewed ones; the pull request head is checked out in pr/ and is only read;
//   - the reviewer is a fresh, headless Claude Code session (`claude -p`) with read-only tools, the policy's
//     model for the role, the base branch's prompt and the verdict's JSON schema;
//   - the verdict is posted by the base branch's post-review.mjs as the check run and the summary comment.
// It also posts the review check of the other kind as "not applicable", which the gates job does in CI.
//
//   node .github/scripts/local-review.mjs <pr-number> [--dry-run] [--keep]
//     --dry-run  run the review and print the verdict; post nothing (needs no GitHub App token)
//     --keep     keep the temporary checkout for inspection
//
// GH_TOKEN must be an installation token of the reviewer app (yaf-reviewer: Checks write, Pull requests write,
// Contents read). Never the factory app's token, which cannot write checks, and never a personal token: the
// ruleset accepts these two checks only from the reviewer app. The model runs on the local Claude Code login.
// Node 22+, git, gh and claude on PATH; no dependencies.
import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { withoutToken } from './factory-token.mjs';

const args = process.argv.slice(2);
const prNumber = Number(args.find((a) => /^\d+$/.test(a)));
const dryRun = args.includes('--dry-run');
const keep = args.includes('--keep');
if (!prNumber) {
  console.error('usage: node .github/scripts/local-review.mjs <pr-number> [--dry-run] [--keep]');
  process.exit(2);
}

const KIT = 'docs/spec/factory/.github/factory';
// Same as the CI workflows, without the inline-comment tool (an MCP server of the CI action). Reads are limited
// to the temporary checkout (./**); Claude Code applies Read rules to Grep and Glob on a best-effort basis.
const TOOLS = ['Read(./**)', 'Grep', 'Glob', 'Bash(git -C pr diff *)', 'Bash(git -C pr log *)', 'Bash(git -C pr show *)'];
const REVIEWS = {
  code: { check: 'factory/review', role: 'prReviewer', prompt: 'pr-review.md', schema: 'review.schema.json', maxTurns: 60, other: 'factory/doc-review', otherTitle: 'not a spec or plan pull request' },
  doc: { check: 'factory/doc-review', role: 'docReviewer', prompt: 'doc-review.md', schema: 'doc-review.schema.json', maxTurns: 80, other: 'factory/review', otherTitle: null },
};

function run(cmd, cmdArgs, opts = {}) {
  const r = spawnSync(cmd, cmdArgs, { encoding: 'utf8', maxBuffer: 256 * 1024 * 1024, ...opts });
  if (r.error) throw r.error;
  if (r.status !== 0 && !opts.allowFail) throw new Error(`${cmd} ${cmdArgs.slice(0, 3).join(' ')} … exited ${r.status}\n${r.stderr}`);
  return r;
}
const gh = (...a) => JSON.parse(run('gh', ['api', ...a]).stdout);

const repo = process.env.GITHUB_REPOSITORY
  ?? run('git', ['remote', 'get-url', 'origin']).stdout.trim().replace(/^.*github\.com[/:]/, '').replace(/\.git$/, '');

if (!dryRun) {
  if (!process.env.GH_TOKEN) throw new Error('GH_TOKEN is not set: run this with an installation token of the reviewer app (or use --dry-run)');
  // An installation token cannot read /user; a personal token can. Personal tokens are refused.
  if (run('gh', ['api', 'user'], { allowFail: true }).status === 0) throw new Error('GH_TOKEN is a personal token; use an installation token of the reviewer app');
}

const pr = gh(`repos/${repo}/pulls/${prNumber}`);
if (pr.state !== 'open') throw new Error(`#${prNumber} is ${pr.state}`);
if (pr.draft) throw new Error(`#${prNumber} is a draft; CI does not review drafts either`);
if (pr.head.repo?.full_name !== repo) throw new Error(`#${prNumber} comes from a fork; forks are not reviewed`);
const base = pr.base.sha;
const head = pr.head.sha;

const work = mkdtempSync(path.join(tmpdir(), `factory-review-${prNumber}-`));
const baseDir = path.join(work, 'base');
const prDir = path.join(baseDir, 'pr');
const cleanup = () => {
  if (keep) return console.error(`kept ${work}`);
  run('git', ['worktree', 'remove', '--force', prDir], { allowFail: true });
  run('git', ['worktree', 'remove', '--force', baseDir], { allowFail: true });
  rmSync(work, { recursive: true, force: true });
};

let cancelRunning = null; // set while a check run in progress is on the head commit and no verdict is posted
try {
  run('git', ['fetch', '--quiet', 'origin', base, `refs/pull/${prNumber}/head`]);
  run('git', ['worktree', 'add', '--quiet', '--detach', baseDir, base]);
  run('git', ['worktree', 'add', '--quiet', '--detach', prDir, head]);

  // Kind and model, decided by the base branch's scope.mjs, exactly as in CI.
  const eventFile = path.join(work, 'event.json');
  writeFileSync(eventFile, JSON.stringify({ pull_request: { number: prNumber, base: { sha: base }, head: { sha: head } } }));
  const env = { ...process.env, GITHUB_REPOSITORY: repo, GITHUB_EVENT_PATH: eventFile };
  const scope = (role) => {
    const out = path.join(work, `scope-${role}.txt`);
    writeFileSync(out, '');
    run('node', [`${KIT}/scope.mjs`, '--pr-dir', 'pr', '--role', role], { cwd: baseDir, env: { ...env, GITHUB_OUTPUT: out } });
    return Object.fromEntries(readFileSync(out, 'utf8').trim().split('\n').map((l) => l.split('=')));
  };
  const kind = scope('prReviewer').kind;
  const review = REVIEWS[kind === 'code' ? 'code' : 'doc'];
  const { agent, model } = scope(review.role);
  if (agent !== 'claude') throw new Error(`the policy assigns agent "${agent}" to ${review.role}; only claude is wired in`);

  const prompt = `${readFileSync(path.join(baseDir, KIT, 'prompts', review.prompt), 'utf8')}\n`
    + `Pull request: #${prNumber} in ${repo}.${kind === 'code' ? '' : ` Kind: ${kind}.`} Base commit: ${base}. Head commit: ${head}.`;
  const schema = readFileSync(path.join(baseDir, KIT, review.schema), 'utf8').replaceAll('\n', '');

  // The running review is reported as a check run in progress, so that a second one is not started meanwhile.
  // It is advisory: a failed post is a warning. An exception before the verdict cancels it (below); if this script
  // dies, it stays in progress and counts as owed again after 30 minutes (inflight.mjs).
  // Skipped while the base branch's kit does not have the function yet.
  const kit = await import(new URL(`file:///${path.join(baseDir, KIT, 'lib.mjs').replaceAll('\\', '/')}`));
  if (!dryRun && kit.inProgressCheck) {
    try {
      const { id } = kit.api(`repos/${repo}/check-runs`, { method: 'POST', body: kit.inProgressCheck(review.check, head, 'the review is running') });
      cancelRunning = () => kit.api(`repos/${repo}/check-runs/${id}`, { method: 'PATCH', body: { status: 'completed', conclusion: 'cancelled' } });
    } catch (e) {
      console.error(`warning: the check in progress was not posted: ${String(e.message).split('\n')[0]}`);
    }
  }

  console.error(`#${prNumber}: kind ${kind}, ${review.check} by ${model}, base ${base.slice(0, 7)}, head ${head.slice(0, 7)}`);
  // Fresh headless session in the base checkout. Only the checkout's project settings and no MCP servers are
  // loaded, so the maintainer's personal settings, hooks and connectors do not reach the reviewer.
  const result = run('claude', [
    '-p', prompt,
    '--model', model,
    '--max-turns', String(review.maxTurns),
    '--allowedTools', TOOLS.join(','),
    '--json-schema', schema,
    '--output-format', 'json',
    '--setting-sources', 'project',
    '--strict-mcp-config',
  ], { cwd: baseDir, allowFail: true, input: '', env: withoutToken(process.env) });

  let verdict = null;
  try {
    const out = JSON.parse(result.stdout);
    if (out.is_error) console.error(`the reviewer failed: ${out.result}`);
    verdict = out.structured_output ?? null;
  } catch {
    console.error(`the reviewer returned no JSON (exit ${result.status}):\n${result.stderr.slice(0, 2000)}`);
  }

  if (dryRun) {
    console.log(JSON.stringify(verdict, null, 2));
  } else {
    // The base branch's poster: the same check names, rounds counting and needs-human escalation as in CI.
    run('node', [`${KIT}/post-review.mjs`, '--check', review.check], { cwd: baseDir, env: { ...env, REVIEW_OUTPUT: JSON.stringify(verdict) }, stdio: ['ignore', 'inherit', 'inherit'] });
    cancelRunning = null;
    const otherTitle = review.otherTitle ?? `${kind} pull request: reviewed by factory/doc-review`;
    process.env.GITHUB_REPOSITORY = repo;
    kit.setCheck(head, review.other, 'success', otherTitle);
  }
  process.exitCode = verdict ? 0 : 1;
} catch (e) {
  // No verdict was posted: cancelled is neither passed nor a failed review, so the review is owed again at once.
  try { cancelRunning?.(); } catch { /* best effort: a run left in progress is owed again after 30 minutes */ }
  throw e;
} finally {
  cleanup();
}
