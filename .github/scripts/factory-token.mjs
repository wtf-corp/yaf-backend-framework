// Token helper for running the factory locally (plan specs/0002-local-factory-operation, T2 to T5).
// Node 22+, no dependencies. It builds the JWT of a GitHub App, mints an installation token scoped to one
// repository and runs one command with it, as the app. The key and the JWT exist only in memory; no message
// holds them or a token, and the token is written only under --print.
import crypto from 'node:crypto';
import fs from 'node:fs';
import { execFileSync, execSync, spawn } from 'node:child_process';

const API = 'https://api.github.com';

/**
 * The two apps of the factory. `botId` is the user id of the account `<slug>[bot]`, which is not the App ID.
 * App IDs, variable names, slugs and bot ids are identifiers, not secrets.
 */
export const APPS = {
  factory: { appId: 5834995, keyVar: 'YAF_FACTORY_KEY_FILE', slug: 'yaf-factory', botId: 358792446 },
  reviewer: { appId: 5837737, keyVar: 'YAF_REVIEWER_KEY_FILE', slug: 'yaf-reviewer', botId: 358867400 },
};

/** Why a key file with this `mode` must not be used, or null. Windows modes say nothing about the ACL. */
export function keyFileProblem(mode, platform) {
  if (platform === 'win32' || (mode & 0o077) === 0) return null;
  return `is readable by group or others (mode ${(mode & 0o777).toString(8)})`;
}

/** Runs `command` through the platform's shell and returns its standard output. */
const runShell = (command) => execSync(command, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'inherit'] });

/**
 * The private key of `app` (a name in APPS) as PEM text. `keyCmd` wins: its standard output is the key.
 * Otherwise the key is read from `keyFile`, or from the path in the app's variable. No message holds the key.
 * `run` and `warn` are the shell runner and the warning sink, replaced in tests.
 */
export function loadKey({ app, keyFile, keyCmd, env, platform, run = runShell, warn = console.error }) {
  if (!Object.hasOwn(APPS, app)) throw new Error(`Unknown app "${app}": the apps are ${Object.keys(APPS).join(' and ')}.`);
  if (keyFile && keyCmd) throw new Error('--key-file and --key-cmd exclude each other: give one.');
  let pem;
  if (keyCmd) {
    try {
      pem = run(keyCmd);
    } catch (error) {
      throw new Error(`The --key-cmd command failed (exit status ${error?.status ?? 'unknown'}).`);
    }
  } else {
    const { keyVar } = APPS[app];
    const file = keyFile || env[keyVar];
    if (!file) throw new Error(`No key for app "${app}": set ${keyVar} to the path of its private key file, or give --key-file or --key-cmd.`);
    const problem = keyFileProblem(fs.statSync(file).mode, platform);
    if (problem) throw new Error(`The key file ${file} ${problem}. Run: chmod 600 "${file}"`);
    if (platform === 'win32') warn(`Warning: the permissions of ${file} were not checked; Windows file modes say nothing about who can read it.`);
    pem = fs.readFileSync(file, 'utf8');
  }
  if (!/-----BEGIN [A-Z ]*PRIVATE KEY-----/.test(pem)) throw new Error('The key is not a PEM private key: no PEM private-key header found.');
  return pem;
}

const base64url = (value) => Buffer.from(value).toString('base64url');

/** The RS256 JWT that authenticates as the app: issued 60 seconds back, valid for 9 minutes from now. */
export function buildJwt(appId, pem, nowSeconds) {
  const header = base64url(JSON.stringify({ alg: 'RS256', typ: 'JWT' }));
  const claims = base64url(JSON.stringify({ iat: nowSeconds - 60, exp: nowSeconds + 540, iss: appId }));
  const signature = crypto.sign('RSA-SHA256', Buffer.from(`${header}.${claims}`), pem).toString('base64url');
  return `${header}.${claims}.${signature}`;
}

/**
 * Exchanges the app's JWT for an installation token that reaches only `repo` ("owner/name").
 * `now` is the time in seconds. Returns { token, expiresAt }. A status that is not 2xx throws an error
 * built from the method, the path, the status and the `message` field of the response, nothing else.
 */
export async function mintToken({ fetch, appId, pem, repo, now = Math.floor(Date.now() / 1000) }) {
  const headers = {
    Authorization: `Bearer ${buildJwt(appId, pem, now)}`,
    Accept: 'application/vnd.github+json',
    'X-GitHub-Api-Version': '2022-11-28',
  };
  const call = async (method, path, body) => {
    const response = await fetch(`${API}${path}`, { method, headers, body: body && JSON.stringify(body) });
    const data = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(`${method} ${path} failed with status ${response.status}: ${data?.message ?? ''}`);
    return data;
  };
  const installation = await call('GET', `/repos/${repo}/installation`);
  const granted = await call('POST', `/app/installations/${installation.id}/access_tokens`, {
    repositories: [repo.split('/')[1]],
  });
  return { token: granted.token, expiresAt: granted.expires_at };
}

/** "owner/name" from the URL of a GitHub remote, HTTPS or SSH, with or without ".git". The error does not echo the URL. */
export function repoFromRemote(url) {
  const match = /^(?:https:\/\/github\.com\/|ssh:\/\/git@github\.com\/|git@github\.com:)([\w.-]+\/[\w.-]+?)(?:\.git)?$/.exec(url.trim());
  if (!match) throw new Error('The remote URL is not that of a GitHub repository: give --repo <owner>/<name>.');
  return match[1];
}

/**
 * The environment of a command that runs as `app` (a name in APPS) with `token` on `repo`: a copy of `env`
 * with the token for gh, a credential helper for git over HTTPS that replaces the configured ones for that
 * process only, and the app's bot account as author and committer of a commit. Everything else passes through.
 */
export function childEnv(env, { token, repo, app }) {
  refuseGitConfig(env);
  const { slug, botId } = APPS[app];
  const name = `${slug}[bot]`;
  const email = `${botId}+${name}@users.noreply.github.com`;
  return {
    ...env,
    GH_TOKEN: token,
    GITHUB_TOKEN: token,
    GITHUB_REPOSITORY: env.GITHUB_REPOSITORY || repo,
    GIT_TERMINAL_PROMPT: '0',
    GIT_CONFIG_COUNT: '2',
    GIT_CONFIG_KEY_0: 'credential.helper',
    GIT_CONFIG_VALUE_0: '',
    GIT_CONFIG_KEY_1: 'credential.helper',
    GIT_CONFIG_VALUE_1: '!f() { echo username=x-access-token; echo "password=$GH_TOKEN"; }; f',
    GIT_AUTHOR_NAME: name,
    GIT_AUTHOR_EMAIL: email,
    GIT_COMMITTER_NAME: name,
    GIT_COMMITTER_EMAIL: email,
  };
}

/**
 * The inverse of childEnv: a copy of `env` without the token (GH_TOKEN, GITHUB_TOKEN) and without the git
 * configuration given in the environment (GIT_CONFIG_COUNT, GIT_CONFIG_KEY_n, GIT_CONFIG_VALUE_n), for a child
 * that must not hold the app's credential. The commit identity stays: it is no credential.
 */
export function withoutToken(env) {
  // Any case, as in refuseGitConfig below.
  const added = /^(GH_TOKEN|GITHUB_TOKEN|GIT_CONFIG_COUNT|GIT_CONFIG_(KEY|VALUE)_\d+)$/i;
  return Object.fromEntries(Object.entries(env).filter(([key]) => !added.test(key)));
}

/** Throws when `env` already holds GIT_CONFIG_COUNT, which childEnv would otherwise overwrite. */
function refuseGitConfig(env) {
  // Any case: Windows reads variable names without regard to case, and a copy of process.env keeps the case given.
  if (Object.keys(env).some((key) => key.toUpperCase() === 'GIT_CONFIG_COUNT')) throw new Error('GIT_CONFIG_COUNT is already set: unset it, the helper does not overwrite git configuration given in the environment.');
}

/** "owner/name" with no segment that is "." or "..": such a segment would change the path of an API call. */
const REPO_NAME = /^(?!\.{1,2}\/)[\w.-]+\/(?!\.{1,2}$)[\w.-]+$/;

const USAGE = `Usage:
  node .github/scripts/factory-token.mjs --app factory|reviewer [--key-file <path> | --key-cmd "<command>"] [--repo <owner>/<name>] -- <command> [args…]
  node .github/scripts/factory-token.mjs --app factory|reviewer [--key-file <path> | --key-cmd "<command>"] [--repo <owner>/<name>] --print

Runs the command as the app: with a token for this one repository, for gh and for git over HTTPS, and with
the app's bot account as author of a commit. --print writes the token instead; for your own terminal only.`;

/**
 * The options of a command line: { app, keyFile, keyCmd, repo, print, command }, where `command` is everything
 * after "--". Throws an error that says what is wrong with the arguments; the entry point adds the usage text.
 */
export function parseArgs(argv) {
  const options = { app: undefined, keyFile: undefined, keyCmd: undefined, repo: undefined, print: false, command: [] };
  const values = { '--app': 'app', '--key-file': 'keyFile', '--key-cmd': 'keyCmd', '--repo': 'repo' };
  for (let i = 0; i < argv.length; i++) {
    const flag = argv[i];
    if (flag === '--') {
      options.command = argv.slice(i + 1);
      break;
    }
    if (flag === '--print') options.print = true;
    else if (Object.hasOwn(values, flag)) {
      if (i + 1 >= argv.length || argv[i + 1] === '--') throw new Error(`${flag} needs a value.`);
      options[values[flag]] = argv[++i];
    } else throw new Error(`Unknown argument "${flag}": a command comes after "--".`);
  }
  if (!Object.hasOwn(APPS, options.app ?? '')) throw new Error(`--app must be one of: ${Object.keys(APPS).join(', ')}.`);
  if (options.keyFile && options.keyCmd) throw new Error('--key-file and --key-cmd exclude each other: give one.');
  if (options.repo !== undefined && !REPO_NAME.test(options.repo)) throw new Error('--repo must be <owner>/<name>.');
  if (options.print && options.command.length > 0) throw new Error('--print runs no command: give one or the other.');
  if (!options.print && options.command.length === 0) throw new Error('No command given: put it after "--", or give --print.');
  return options;
}

/** The program a command names: no folder, lower case, no ".exe". Windows drops trailing dots and spaces from a file name: "git." and "git.exe." start git too. */
const programName = (command) => command.split(/[\\/]/).pop().toLowerCase().replace(/[. ]+$/, '').replace(/\.exe$/, '').replace(/[. ]+$/, '');

/** GitHub compares label names without regard to case. */
const HUMAN_LABEL = /guard-change|major-release|size-accepted/i;
/** What a `gh api` argument must not name; `labels` counts only together with a human's label. */
const HUMAN_API = /pulls\/[^/]+\/(reviews|merge)|merge-queue|rulesets|branches\/.+\/protection|environments/i;
/** GraphQL mutations that merge or arm a merge; a review mutation counts only together with APPROVE. A query read from a file (-F query=@file) is not looked into. */
const HUMAN_MUTATION = /mergePullRequest|enablePullRequestAutoMerge/;
const REVIEW_MUTATION = /(add|submit)PullRequestReview/;
/** Options of git that stand before the subcommand and take their value as the next argument. */
const GIT_VALUE_OPTIONS = ['-C', '-c', '--git-dir', '--work-tree', '--namespace', '--config-env'];

/** What of `gh <args>` is reserved for a human, or null. */
function refusedGh(args) {
  // The subcommand words: flags are skipped, and so is the value of -R/--repo, which may stand between them.
  const words = args.filter((arg, i) => !arg.startsWith('-') && !['-R', '--repo'].includes(args[i - 1]));
  const [group, verb] = words;
  const two = `${group} ${verb}`;
  if (['release', 'label', 'secret'].includes(group)) return `gh ${group}`;
  if (['pr merge', 'repo edit', 'variable set', 'variable delete', 'workflow disable'].includes(two)) return `gh ${two}`;
  if (two === 'pr review' && args.some((arg) => arg.startsWith('--approve') || /^-a/.test(arg))) return 'an approving review (gh pr review --approve)';
  const labelIn = (list) => list.map((arg) => HUMAN_LABEL.exec(arg)?.[0]).find(Boolean);
  const label = labelIn(args);
  if (label && ['pr edit', 'issue edit'].includes(two)) return `the label ${label} (gh ${two})`;
  // On create only the labels count: a title or a body may name one.
  const created = ['pr create', 'issue create'].includes(two) && labelIn(args.filter((arg, i) => /^(--label|-l)/.test(arg) || ['--label', '-l'].includes(args[i - 1])));
  if (created) return `the label ${created} (gh ${two})`;
  if (group !== 'api') return null;
  if (label && args.some((arg) => /labels/i.test(arg))) return `the label ${label} (gh api)`;
  const has = (pattern) => args.some((arg) => pattern.test(arg));
  if (has(REVIEW_MUTATION) && has(/APPROVE/)) return 'an approving review (gh api graphql)';
  const named = args.map((arg) => (HUMAN_API.exec(arg) ?? HUMAN_MUTATION.exec(arg))?.[0]).find(Boolean);
  return named ? `gh api on ${named}` : null;
}

/** What of `git <args>` is reserved for a human, or null. */
function refusedGit(args) {
  let i = 0;
  while (i < args.length && args[i].startsWith('-')) i += GIT_VALUE_OPTIONS.includes(args[i]) ? 2 : 1;
  const [subcommand, ...rest] = args.slice(i);
  if (subcommand === 'tag') return 'git tag';
  if (subcommand !== 'push') return null;
  if (rest.some((arg) => arg.startsWith('--force') || arg === '--mirror' || /^-[a-zA-Z]*f/.test(arg) || arg.startsWith('+'))) return 'a forced push';
  // Every local branch goes with these, a local main, next or stable among them.
  if (rest.some((arg) => arg === '--all' || arg === '--branches')) return 'a push of every branch';
  // The first argument that is no option is the remote; a refspec is [+]<source>[:<destination>].
  const refs = rest.filter((arg) => !arg.startsWith('-')).slice(1).map((refspec) => refspec.slice(refspec.lastIndexOf(':') + 1));
  if (rest.some((arg) => arg === '--tags' || arg === '--follow-tags') || refs.some((ref) => /^(refs\/)?tags\//.test(ref))) return 'a push of tags';
  // git completes a destination with refs/ and with refs/heads/: "heads/main" is main too.
  const destinations = refs.map((ref) => ref.replace(/^(refs\/)?(heads\/)?/, ''));
  const branch = destinations.find((destination) => ['main', 'next', 'stable'].includes(destination));
  return branch ? `a push to ${branch}` : null;
}

/**
 * Why `command` with `args` must not run as an app, or null: merging, approving, the labels, tags, releases,
 * repository settings and pushes that AGENTS.md (hard rule 8) reserves for a human maintainer, as the deny rules
 * of .claude/settings.json name them. Those rules match command text and miss a command wrapped in this helper.
 * It stops the accident and is no boundary: what a shell or a script given as the command does is not looked into.
 */
export function refusedCommand(command, args) {
  const program = programName(command);
  const what = program === 'gh' ? refusedGh(args) : program === 'git' ? refusedGit(args) : null;
  return what && `Refused: ${what} is reserved for a human maintainer (AGENTS.md, hard rule 8). `
    + 'This check stops the accident; it is not a boundary: the permissions of the app and the ruleset are.';
}

/**
 * Why `command` must not run with the app's token when the URL of origin is `originUrl`, or null.
 * Only git is refused, and only when origin is not HTTPS. The message does not echo the URL.
 */
export function gitRefusal(command, originUrl) {
  if (programName(command) !== 'git' || /^https:\/\//i.test(originUrl.trim())) return null;
  let repo = '<owner>/<name>';
  try {
    repo = repoFromRemote(originUrl);
  } catch {
    // Not a GitHub URL, or no origin at all: the message keeps the placeholder.
  }
  return 'Refused: the URL of origin is not HTTPS. The credential helper this script sets serves HTTPS only, so over SSH '
    + 'git would authenticate with your own key and act as you, not as the app. '
    + `Run: git remote set-url origin https://github.com/${repo}.git`;
}

/**
 * Starts `command` with `args` in `env`, without a shell and with the standard streams inherited. Resolves to
 * its exit code, or to 1 when it ended by a signal or could not start; `warn` then gets one line saying which.
 */
export function runCommand(command, args, env, warn = console.error) {
  return new Promise((resolve) => {
    const notStarted = (error) => {
      warn(`Could not start "${command}" (${error?.code ?? 'unknown error'}). The command runs without a shell: `
        + 'it must be a program on the PATH, and a .cmd or .bat file must be wrapped in its interpreter (cmd /c <file> …).');
      resolve(1);
    };
    try {
      const child = spawn(command, args, { env, stdio: 'inherit', shell: false });
      child.on('error', notStarted);
      child.on('exit', (code, signal) => {
        if (code === null) warn(`"${command}" ended by signal ${signal}.`);
        resolve(code ?? 1);
      });
    } catch (error) {
      notStarted(error);
    }
  });
}

/** The URL of origin, or "" when there is none. */
function readOriginUrl(...flags) {
  try {
    return execFileSync('git', ['remote', 'get-url', ...flags, 'origin'], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim();
  } catch {
    return '';
  }
}

/** The command line: the exit code of a run with the arguments `argv` in the environment `env`. */
async function main(argv, env) {
  let options;
  try {
    options = parseArgs(argv);
  } catch (error) {
    console.error(`${error.message}\n\n${USAGE}`);
    return 2;
  }
  try {
    const { app, keyFile, keyCmd, print, command: [command, ...args] } = options;
    const reserved = print ? null : refusedCommand(command, args);
    if (reserved) {
      console.error(reserved);
      return 2;
    }
    const origin = readOriginUrl();
    // A push goes to the push URLs, which remote.origin.pushurl and url.<base>.pushInsteadOf can make differ from the fetch URL.
    const pushRefusal = () => readOriginUrl('--push', '--all').split('\n').map((url) => gitRefusal(command, url)).find(Boolean);
    const refusal = print ? null : gitRefusal(command, origin) ?? pushRefusal();
    if (refusal) {
      console.error(refusal);
      if (/^https:\/\//i.test(origin)) console.error('It is a push URL of origin that is not HTTPS: see remote.origin.pushurl and url.<base>.pushInsteadOf in the git configuration.');
      return 2;
    }
    const repo = options.repo ?? repoFromRemote(origin);
    if (!REPO_NAME.test(repo)) throw new Error('The repository is not <owner>/<name>: give --repo <owner>/<name>.');
    if (!print) refuseGitConfig(env);
    const pem = loadKey({ app, keyFile, keyCmd, env, platform: process.platform });
    const { token, expiresAt } = await mintToken({ fetch, appId: APPS[app].appId, pem, repo });
    if (print) {
      console.log(`${token}\n${expiresAt}`);
      return 0;
    }
    return await runCommand(command, args, childEnv(env, { token, repo, app }));
  } catch (error) {
    // The messages thrown in this file hold no key, JWT or token; a stack is not printed.
    console.error(error.message);
    return 1;
  }
}

if (process.argv[1]?.endsWith('factory-token.mjs')) process.exit(await main(process.argv.slice(2), process.env));
