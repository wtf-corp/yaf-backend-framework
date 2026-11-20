// Tests of factory-token.mjs. No network call and no stored key: the key pair is generated here,
// and the token strings are fakes the assertions look for.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { APPS, buildJwt, childEnv, gitRefusal, keyFileProblem, loadKey, mintToken, parseArgs, refusedCommand, repoFromRemote, runCommand, withoutToken } from '../factory-token.mjs';

const { privateKey, publicKey } = crypto.generateKeyPairSync('rsa', { modulusLength: 2048 });
const pem = privateKey.export({ type: 'pkcs8', format: 'pem' });
const NOW = 1_800_000_000;
const FAKE_TOKEN = 'fake-installation-token';

const decode = (part) => JSON.parse(Buffer.from(part, 'base64url').toString('utf8'));

/** A fetch that records its calls and answers them in order from `responses` ([status, body] pairs). */
function fakeFetch(responses) {
  const calls = [];
  const fetch = async (url, init = {}) => {
    calls.push({ url, ...init });
    const [status, body] = responses[calls.length - 1];
    return { ok: status >= 200 && status < 300, status, json: async () => body };
  };
  return { fetch, calls };
}

test('APPS names the app id, the key variable, the slug and the bot id of both apps', () => {
  assert.deepEqual(APPS, {
    factory: { appId: 5834995, keyVar: 'YAF_FACTORY_KEY_FILE', slug: 'yaf-factory', botId: 358792446 },
    reviewer: { appId: 5837737, keyVar: 'YAF_REVIEWER_KEY_FILE', slug: 'yaf-reviewer', botId: 358867400 },
  });
});

test('buildJwt signs RS256 with the app id as issuer', () => {
  const jwt = buildJwt(APPS.factory.appId, pem, NOW);
  const [header, claims, signature, ...rest] = jwt.split('.');
  assert.equal(rest.length, 0);
  assert.deepEqual(decode(header), { alg: 'RS256', typ: 'JWT' });
  assert.deepEqual(decode(claims), { iat: NOW - 60, exp: NOW + 540, iss: APPS.factory.appId });
  assert.ok(crypto.verify('RSA-SHA256', Buffer.from(`${header}.${claims}`), publicKey, Buffer.from(signature, 'base64url')));
});

test('mintToken asks for a token scoped to the one repository', async () => {
  const { fetch, calls } = fakeFetch([
    [200, { id: 4242 }],
    [201, { token: FAKE_TOKEN, expires_at: '2027-01-15T09:00:00Z' }],
  ]);
  const result = await mintToken({ fetch, appId: 5834995, pem, repo: 'some-owner/some-repo', now: NOW });

  assert.deepEqual(result, { token: FAKE_TOKEN, expiresAt: '2027-01-15T09:00:00Z' });
  assert.equal(calls.length, 2);
  assert.equal(calls[0].method, 'GET');
  assert.equal(calls[0].url, 'https://api.github.com/repos/some-owner/some-repo/installation');
  assert.equal(calls[0].body, undefined);
  assert.equal(calls[1].method, 'POST');
  assert.equal(calls[1].url, 'https://api.github.com/app/installations/4242/access_tokens');
  assert.deepEqual(JSON.parse(calls[1].body), { repositories: ['some-repo'] });
  for (const call of calls) {
    assert.deepEqual(call.headers, {
      Authorization: `Bearer ${buildJwt(5834995, pem, NOW)}`,
      Accept: 'application/vnd.github+json',
      'X-GitHub-Api-Version': '2022-11-28',
    });
  }
});

test("a failed call's error holds no key, JWT or token", async () => {
  const jwt = buildJwt(5834995, pem, NOW);
  const keyLines = pem.split('\n').filter((line) => line && !line.startsWith('-----'));
  const cases = [
    { responses: [[401, { message: 'Bad credentials', token: FAKE_TOKEN }]],
      expected: 'GET /repos/some-owner/some-repo/installation failed with status 401: Bad credentials' },
    { responses: [[200, { id: 4242 }], [404, { message: 'Not Found', token: FAKE_TOKEN }]],
      expected: 'POST /app/installations/4242/access_tokens failed with status 404: Not Found' },
  ];
  for (const { responses, expected } of cases) {
    const { fetch } = fakeFetch(responses);
    const error = await mintToken({ fetch, appId: 5834995, pem, repo: 'some-owner/some-repo', now: NOW })
      .then(() => assert.fail('mintToken must reject'), (e) => e);
    assert.equal(error.message, expected);
    const everything = `${error.stack}\n${JSON.stringify(error, Object.getOwnPropertyNames(error))}`;
    for (const secret of [jwt, ...jwt.split('.'), FAKE_TOKEN, ...keyLines]) {
      assert.ok(!everything.includes(secret), 'the error must not hold a secret');
    }
  }
});

/** Runs `use` with a temporary file holding `content`, then deletes it. */
function withFile(content, use) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'factory-token-test-'));
  const file = path.join(dir, 'app.pem');
  try {
    fs.writeFileSync(file, content);
    fs.chmodSync(file, 0o644);
    return use(file);
  } finally {
    fs.rmSync(dir, { recursive: true, force: true });
  }
}

/** A second key, so that a test can tell which source was read. */
const otherPem = crypto.generateKeyPairSync('rsa', { modulusLength: 2048 }).privateKey.export({ type: 'pkcs8', format: 'pem' });
const neverRun = () => assert.fail('no command may run');
const neverWarn = () => assert.fail('no warning expected');
const everythingIn = (error) => `${error.stack}\n${JSON.stringify(error, Object.getOwnPropertyNames(error))}`;

test('keyFileProblem refuses a key file readable by group or others', () => {
  for (const platform of ['linux', 'darwin']) {
    assert.match(keyFileProblem(0o100644, platform), /group or others/);
    assert.match(keyFileProblem(0o100640, platform), /group or others/);
    assert.equal(keyFileProblem(0o100600, platform), null);
  }
  assert.equal(keyFileProblem(0o100666, 'win32'), null);
});

test("loadKey reads the file named by the app's variable and warns on Windows", () => {
  withFile(pem, (file) => {
    const warnings = [];
    const warn = (line) => warnings.push(line);
    const key = loadKey({ app: 'reviewer', env: { YAF_REVIEWER_KEY_FILE: file }, platform: 'win32', run: neverRun, warn });
    assert.equal(key, pem);
    assert.equal(warnings.length, 1);
    assert.match(warnings[0], /permissions .* not checked/);
    assert.ok(!warnings[0].includes('\n'), 'one line, and so no key');
  });
});

test("loadKey takes --key-file before the app's variable", () => {
  withFile(pem, (file) => withFile(otherPem, (other) => {
    const env = { YAF_FACTORY_KEY_FILE: other };
    assert.equal(loadKey({ app: 'factory', keyFile: file, env, platform: 'win32', run: neverRun, warn: () => {} }), pem);
  }));
});

test('loadKey takes the output of --key-cmd before the variable', () => {
  const commands = [];
  const run = (command) => { commands.push(command); return pem; };
  const env = { YAF_FACTORY_KEY_FILE: '/no/such/file' };
  assert.equal(loadKey({ app: 'factory', keyCmd: 'print-the-key', env, platform: 'linux', run, warn: neverWarn }), pem);
  assert.deepEqual(commands, ['print-the-key']);
});

test('loadKey runs --key-cmd through the shell and takes its output', () => {
  withFile(pem, (file) => {
    const keyCmd = `"${process.execPath}" -e "process.stdout.write(require('fs').readFileSync(process.argv[1], 'utf8'))" "${file}"`;
    assert.equal(loadKey({ app: 'factory', keyCmd, env: {}, platform: process.platform, warn: neverWarn }), pem);
  });
});

test('loadKey refuses an app it does not know, naming the apps', () => {
  for (const app of ['other', 'toString', undefined]) {
    assert.throws(() => loadKey({ app, keyCmd: 'print-the-key', env: {}, platform: 'linux', run: neverRun, warn: neverWarn }),
      /Unknown app .* factory and reviewer/);
  }
});

test('loadKey refuses --key-file together with --key-cmd', () => {
  const both = { app: 'factory', keyFile: '/no/such/file', keyCmd: 'print-the-key', env: {}, platform: 'linux' };
  assert.throws(() => loadKey({ ...both, run: neverRun, warn: neverWarn }), /--key-file and --key-cmd/);
});

test('loadKey with no source fails naming the variable', () => {
  const rest = { platform: 'linux', run: neverRun, warn: neverWarn };
  assert.throws(() => loadKey({ app: 'factory', env: {}, ...rest }), /YAF_FACTORY_KEY_FILE/);
  assert.throws(() => loadKey({ app: 'reviewer', env: { YAF_FACTORY_KEY_FILE: '/no/such/file' }, ...rest }), /YAF_REVIEWER_KEY_FILE/);
});

test('loadKey refuses a key file readable by group or others and names the fix', () => {
  withFile(pem, (file) => {
    assert.throws(() => loadKey({ app: 'factory', keyFile: file, env: {}, platform: 'linux', run: neverRun, warn: neverWarn }),
      (e) => e.message.includes(`chmod 600 "${file}"`) &&!everythingIn(e).includes('PRIVATE KEY'));
  });
});

test('loadKey refuses content that is no PEM private key and does not echo it', () => {
  const content = 'not-a-key-and-not-to-be-shown';
  const publicPem = publicKey.export({ type: 'spki', format: 'pem' });
  const fromCommand = (output) => () => loadKey({ app: 'factory', keyCmd: 'print-the-key', env: {}, platform: 'linux', run: () => output, warn: neverWarn });
  const fromFile = () => withFile(content, (file) => loadKey({ app: 'factory', keyFile: file, env: {}, platform: 'win32', run: neverRun, warn: () => {} }));
  for (const attempt of [fromCommand(content), fromCommand(publicPem), fromFile]) {
    assert.throws(attempt, (e) => /PEM private key/.test(e.message) && !everythingIn(e).includes(content) && !everythingIn(e).includes('PUBLIC KEY'));
  }
});

test('loadKey reports a failed --key-cmd without its output', () => {
  const output = 'partial-output-not-to-be-shown';
  const run = () => { throw Object.assign(new Error(`Command failed: ${output}`), { status: 3, stdout: output }); };
  assert.throws(() => loadKey({ app: 'factory', keyCmd: 'print-the-key', env: {}, platform: 'linux', run, warn: neverWarn }),
    (e) => /--key-cmd/.test(e.message) && !everythingIn(e).includes(output));
});

const REPO = 'some-owner/some-repo';
const asApp = (env, app = 'factory') => childEnv(env, { token: FAKE_TOKEN, repo: REPO, app });

test('childEnv makes the app the author of a commit', () => {
  for (const app of Object.keys(APPS)) {
    const { slug, botId } = APPS[app];
    const env = asApp({ GIT_AUTHOR_NAME: 'A Person', GIT_COMMITTER_EMAIL: 'a-person' }, app);
    const address = `${botId}+${slug}[bot]@users.noreply.github.com`;
    assert.equal(env.GIT_AUTHOR_NAME, `${slug}[bot]`);
    assert.equal(env.GIT_COMMITTER_NAME, `${slug}[bot]`);
    assert.equal(env.GIT_AUTHOR_EMAIL, address);
    assert.equal(env.GIT_COMMITTER_EMAIL, address);
  }
});

test("childEnv overrides git's credential helper for the command only", () => {
  const given = { PATH: '/usr/bin', GH_TOKEN: 'a-persons-token' };
  const env = asApp(given);
  assert.equal(env.GH_TOKEN, FAKE_TOKEN);
  assert.equal(env.GITHUB_TOKEN, FAKE_TOKEN);
  assert.equal(env.GIT_TERMINAL_PROMPT, '0');
  assert.equal(env.GIT_CONFIG_COUNT, '2');
  assert.equal(env.GIT_CONFIG_KEY_0, 'credential.helper');
  assert.equal(env.GIT_CONFIG_VALUE_0, '');
  assert.equal(env.GIT_CONFIG_KEY_1, 'credential.helper');
  assert.equal(env.GIT_CONFIG_VALUE_1, '!f() { echo username=x-access-token; echo "password=$GH_TOKEN"; }; f');
  assert.deepEqual(given, { PATH: '/usr/bin', GH_TOKEN: 'a-persons-token' }, 'the given environment is not changed');
});

test('childEnv refuses an existing GIT_CONFIG_COUNT', () => {
  for (const count of ['1', '0', '']) {
    assert.throws(() => asApp({ GIT_CONFIG_COUNT: count, GIT_CONFIG_KEY_0: 'user.name' }),
      (e) => /GIT_CONFIG_COUNT/.test(e.message) && !everythingIn(e).includes(FAKE_TOKEN));
  }
  assert.throws(() => asApp({ git_config_count: '1' }), /GIT_CONFIG_COUNT/, 'a plain copy of a Windows environment keeps the case given');
});

test('childEnv leaves other variables alone', () => {
  const given = { PATH: '/usr/bin', HOME: '/home/someone', FACTORY_PROJECT_OWNER: 'some-owner', FACTORY_PROJECT_NUMBER: '7' };
  const env = asApp(given);
  for (const [name, value] of Object.entries(given)) assert.equal(env[name], value);
  assert.deepEqual(Object.keys(env).filter((name) => !(name in given)).sort(), [
    'GH_TOKEN', 'GITHUB_REPOSITORY', 'GITHUB_TOKEN',
    'GIT_AUTHOR_EMAIL', 'GIT_AUTHOR_NAME', 'GIT_COMMITTER_EMAIL', 'GIT_COMMITTER_NAME',
    'GIT_CONFIG_COUNT', 'GIT_CONFIG_KEY_0', 'GIT_CONFIG_KEY_1', 'GIT_CONFIG_VALUE_0', 'GIT_CONFIG_VALUE_1',
    'GIT_TERMINAL_PROMPT',
  ].sort());
});

test('childEnv sets GITHUB_REPOSITORY only when it is not set', () => {
  assert.equal(asApp({}).GITHUB_REPOSITORY, REPO);
  assert.equal(asApp({ GITHUB_REPOSITORY: 'other-owner/other-repo' }).GITHUB_REPOSITORY, 'other-owner/other-repo');
});

test('withoutToken removes what childEnv added', () => {
  const given = { PATH: '/usr/bin', HOME: '/home/someone', FACTORY_PROJECT_NUMBER: '7' };
  const asGiven = asApp(given);
  const before = { ...asGiven };
  const env = withoutToken(asGiven);
  for (const name of Object.keys(env)) assert.doesNotMatch(name, /^(GH_TOKEN|GITHUB_TOKEN|GIT_CONFIG_.*)$/);
  assert.ok(!JSON.stringify(env).includes(FAKE_TOKEN), 'no value holds the token');
  const { GIT_AUTHOR_NAME, GIT_AUTHOR_EMAIL, GIT_COMMITTER_NAME, GIT_COMMITTER_EMAIL } = asGiven;
  assert.equal(GIT_AUTHOR_NAME, `${APPS.factory.slug}[bot]`);
  assert.deepEqual(env, {
    ...given, GITHUB_REPOSITORY: REPO, GIT_TERMINAL_PROMPT: '0',
    GIT_AUTHOR_NAME, GIT_AUTHOR_EMAIL, GIT_COMMITTER_NAME, GIT_COMMITTER_EMAIL,
  }, 'everything else stays, the commit identity included');
  assert.deepEqual(asGiven, before, 'the given environment is not changed');
});

test('withoutToken removes the variables in any case and leaves other variables alone', () => {
  const given = {
    Path: '/usr/bin', gh_token: FAKE_TOKEN, Github_Token: FAKE_TOKEN,
    git_config_count: '1', Git_Config_Key_0: 'credential.helper', git_config_value_0: FAKE_TOKEN,
    GIT_CONFIG_GLOBAL: '/some/file', GH_TOKEN_NOTE: 'kept',
  };
  assert.deepEqual(withoutToken(given), { Path: '/usr/bin', GIT_CONFIG_GLOBAL: '/some/file', GH_TOKEN_NOTE: 'kept' });
});

test('repoFromRemote reads owner/name from an HTTPS or SSH GitHub URL, with or without .git', () => {
  for (const url of [
    'https://github.com/some-owner/some-repo.git',
    'https://github.com/some-owner/some-repo',
    'git@github.com:some-owner/some-repo.git',
    'git@github.com:some-owner/some-repo',
    'ssh://git@github.com/some-owner/some-repo.git',
    'https://github.com/some-owner/some-repo.git\n',
  ]) {
    assert.equal(repoFromRemote(url), REPO, url);
  }
  assert.equal(repoFromRemote('https://github.com/some-owner/some.repo.git'), 'some-owner/some.repo');
});

test('repoFromRemote refuses a URL that is no GitHub repository and does not echo it', () => {
  const withCredentials = `https://someone:${FAKE_TOKEN}@github.com/some-owner/some-repo.git`;
  const others = ['https://example.com/some-owner/some-repo.git', 'https://github.com/some-owner', 'https://github.com/a/b/c', '',
    'https://github.com/o/r?x=1', 'https://github.com/o/r#frag', 'git@github.com/o/r', 'ssh://git@github.com:o/r'];
  for (const url of [withCredentials, ...others]) {
    assert.throws(() => repoFromRemote(url), (e) => /GitHub/.test(e.message) && !everythingIn(e).includes(FAKE_TOKEN));
  }
});

const SCRIPT = fileURLToPath(new URL('../factory-token.mjs', import.meta.url));
const USAGE = /Usage:\s+node \.github\/scripts\/factory-token\.mjs --app factory\|reviewer/;
/** The environment of the tests without a key variable: a run that went past the arguments would fail on the key, with exit code 1. */
const noKeyEnv = Object.fromEntries(Object.entries(process.env).filter(([name]) => !/^YAF_|^GIT_CONFIG_/i.test(name)));
const runScript = (args, options = {}) => spawnSync(process.execPath, [SCRIPT, ...args], { env: noKeyEnv, encoding: 'utf8', ...options });

test('the command line exits 2 with the usage text when no command is given', () => {
  const result = runScript(['--app', 'factory', '--repo', REPO]);
  assert.equal(result.status, 2);
  assert.match(result.stderr, /No command given/);
  assert.match(result.stderr, USAGE);
  assert.equal(result.stdout, '');
});

test('the command line exits 2 with the usage text for an unknown app', () => {
  const result = runScript(['--app', 'other', '--repo', REPO, '--', process.execPath, '-e', 'process.exit(0)']);
  assert.equal(result.status, 2);
  assert.match(result.stderr, /--app must be one of: factory, reviewer/);
  assert.match(result.stderr, USAGE);
});

test('parseArgs reads each flag and takes everything after "--" as the command', () => {
  assert.deepEqual(parseArgs(['--app', 'factory', '--', 'gh', 'pr', 'list', '--app', 'x', '--print', '--']),
    { app: 'factory', keyFile: undefined, keyCmd: undefined, repo: undefined, print: false, command: ['gh', 'pr', 'list', '--app', 'x', '--print', '--'] });
  assert.deepEqual(parseArgs(['--repo', REPO, '--key-file', '/some/key.pem', '--app', 'reviewer', '--', 'git', 'push']),
    { app: 'reviewer', keyFile: '/some/key.pem', keyCmd: undefined, repo: REPO, print: false, command: ['git', 'push'] });
  assert.deepEqual(parseArgs(['--app', 'factory', '--key-cmd', 'print-the-key --now', '--print']),
    { app: 'factory', keyFile: undefined, keyCmd: 'print-the-key --now', repo: undefined, print: true, command: [] });
});

test('parseArgs refuses an unknown flag, an unknown app, a missing command and --print with a command', () => {
  const cases = [
    [['--app', 'factory', '--verbose', '--', 'gh'], /Unknown argument "--verbose"/],
    [['--app', 'factory', 'gh', 'pr', 'list'], /Unknown argument "gh"/],
    [['--app', 'other', '--', 'gh'], /--app must be one of: factory, reviewer/],
    [['--app', 'toString', '--', 'gh'], /--app must be one of/],
    [['--', 'gh'], /--app must be one of/],
    [['--app', 'factory'], /No command given/],
    [['--app', 'factory', '--'], /No command given/],
    [['--app', 'factory', '--print', '--', 'gh'], /--print runs no command/],
    [['--app', 'factory', '--key-file', 'a', '--key-cmd', 'b', '--', 'gh'], /--key-file and --key-cmd/],
    [['--app', 'factory', '--repo', 'no-slash', '--', 'gh'], /--repo must be <owner>\/<name>/],
    [['--app', 'factory', '--repo', 'a/b/../c', '--', 'gh'], /--repo must be/],
    [['--app', 'factory', '--repo', '../x', '--', 'gh'], /--repo must be/],
    [['--app', 'factory', '--repo', 'x/..', '--', 'gh'], /--repo must be/],
    [['--app', 'factory', '--repo', './x', '--', 'gh'], /--repo must be/],
    [['--app', 'factory', '--repo', 'x/.', '--', 'gh'], /--repo must be/],
    [['--app', 'factory', '--key-file'], /--key-file needs a value/],
    [['--app', 'factory', '--repo', '--', 'gh'], /--repo needs a value/],
  ];
  for (const [argv, expected] of cases) assert.throws(() => parseArgs(argv), expected, argv.join(' '));
});

test('a git command is refused when origin is not HTTPS', () => {
  for (const url of ['git@github.com:some-owner/some-repo.git', 'ssh://git@github.com/some-owner/some-repo.git']) {
    for (const command of ['git', 'git.exe', '/usr/bin/git', 'C:\\Program Files\\Git\\cmd\\GIT.EXE', 'git.', 'git.exe.', 'git.EXE ']) {
      const refusal = gitRefusal(command, url);
      assert.match(refusal, /not HTTPS/, `${command} ${url}`);
      assert.match(refusal, /serves HTTPS only/);
      assert.ok(refusal.includes('git remote set-url origin https://github.com/some-owner/some-repo.git'));
    }
    for (const command of ['gh', 'node', process.execPath, 'git-lfs']) assert.equal(gitRefusal(command, url), null, command);
  }
  for (const command of ['git', 'gh', 'node']) {
    assert.equal(gitRefusal(command, 'https://github.com/some-owner/some-repo.git'), null);
    assert.equal(gitRefusal(command, 'https://github.com/some-owner/some-repo.git\n'), null);
  }
  for (const url of ['', `http://someone:${FAKE_TOKEN}@example.com/some-repo.git`]) {
    const refusal = gitRefusal('git', url);
    assert.ok(refusal.includes('git remote set-url origin https://github.com/<owner>/<name>.git'));
    assert.ok(!refusal.includes(FAKE_TOKEN), 'the URL is not echoed');
  }
});

test('the command line refuses a git command over an SSH origin before it reads a key', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'factory-token-test-'));
  try {
    const git = (...args) => assert.equal(spawnSync('git', args, { cwd: dir, env: noKeyEnv }).status, 0, `git ${args[0]}`);
    git('init', '--quiet');
    git('remote', 'add', 'origin', 'git@github.com:some-owner/some-repo.git');
    const result = runScript(['--app', 'factory', '--', 'git', 'status'], { cwd: dir });
    assert.equal(result.status, 2);
    assert.match(result.stderr, /git remote set-url origin https:\/\/github\.com\/some-owner\/some-repo\.git/);
    assert.doesNotMatch(result.stderr, /YAF_FACTORY_KEY_FILE/);
    assert.equal(result.stdout, '');
  } finally {
    fs.rmSync(dir, { recursive: true, force: true });
  }
});

/** Runs `use` with a temporary directory holding the generated key, readable by its owner only, then deletes it. */
function withKeyDir(use) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'factory-token-test-'));
  try {
    const keyFile = path.join(dir, 'app.pem');
    fs.writeFileSync(keyFile, pem, { mode: 0o600 });
    return use(dir, keyFile);
  } finally {
    fs.rmSync(dir, { recursive: true, force: true });
  }
}

/** A module for --import that replaces fetch: no network, the installation and then the fake token. */
const FETCH_STUB = `let calls = 0;
globalThis.fetch = async () => {
  const body = calls++ === 0 ? { id: 4242 } : { token: '${FAKE_TOKEN}', expires_at: '2027-01-15T09:00:00Z' };
  return { ok: true, status: 200, json: async () => body };
};
`;

test('the command line with --print writes the token and its expiry, and nothing else', () => {
  withKeyDir((dir, keyFile) => {
    const stub = path.join(dir, 'fetch-stub.mjs');
    fs.writeFileSync(stub, FETCH_STUB);
    const result = spawnSync(process.execPath, ['--import', pathToFileURL(stub).href, SCRIPT, '--app', 'factory', '--key-file', keyFile, '--repo', REPO, '--print'],
      { env: noKeyEnv, encoding: 'utf8' });
    assert.equal(result.status, 0, result.stderr);
    assert.equal(result.stdout, `${FAKE_TOKEN}\n2027-01-15T09:00:00Z\n`);
    assert.ok(!result.stderr.includes(FAKE_TOKEN));
    assert.ok(!result.stderr.includes('PRIVATE KEY'));
  });
});

test('the command line refuses an origin whose owner and name are dots before it reads a key', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'factory-token-test-'));
  try {
    const git = (...args) => assert.equal(spawnSync('git', args, { cwd: dir, env: noKeyEnv }).status, 0, `git ${args[0]}`);
    git('init', '--quiet');
    git('remote', 'add', 'origin', 'https://github.com/../..');
    const result = runScript(['--app', 'factory', '--', process.execPath, '-e', 'process.exit(0)'], { cwd: dir });
    assert.equal(result.status, 1);
    assert.match(result.stderr, /The repository is not <owner>\/<name>/);
    assert.doesNotMatch(result.stderr, /YAF_FACTORY_KEY_FILE/);
  } finally {
    fs.rmSync(dir, { recursive: true, force: true });
  }
});

test('the command line refuses a git command when only the push URL of origin is SSH', () => {
  const https = 'https://github.com/some-owner/some-repo.git';
  const ssh = 'git@github.com:some-owner/some-repo.git';
  // Repository-local configuration only: a separate push URL, and a rewrite of the URL for pushes.
  for (const setting of [['remote.origin.pushurl', ssh], ['url.git@github.com:.pushInsteadOf', 'https://github.com/']]) {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'factory-token-test-'));
    try {
      const git = (...args) => assert.equal(spawnSync('git', args, { cwd: dir, env: noKeyEnv }).status, 0, `git ${args[0]}`);
      git('init', '--quiet');
      git('remote', 'add', 'origin', https);
      git('config', '--local', ...setting);
      const result = runScript(['--app', 'factory', '--', 'git', 'push'], { cwd: dir });
      assert.equal(result.status, 2, setting[0]);
      assert.match(result.stderr, /not HTTPS/);
      assert.match(result.stderr, /push URL/);
      assert.doesNotMatch(result.stderr, /YAF_FACTORY_KEY_FILE/);
      assert.equal(result.stdout, '');
    } finally {
      fs.rmSync(dir, { recursive: true, force: true });
    }
  }
});

test('the command line runs the command as the app and exits with its code', () => {
  withKeyDir((dir, keyFile) => {
    const stub = path.join(dir, 'fetch-stub.mjs');
    fs.writeFileSync(stub, FETCH_STUB);
    const check = `process.exit(process.env.GH_TOKEN === '${FAKE_TOKEN}' && process.env.GIT_AUTHOR_NAME === 'yaf-reviewer[bot]' ? 7 : 0)`;
    const result = spawnSync(process.execPath, ['--import', pathToFileURL(stub).href, SCRIPT, '--app', 'reviewer', '--key-file', keyFile, '--repo', REPO, '--', process.execPath, '-e', check],
      { env: noKeyEnv, encoding: 'utf8' });
    assert.equal(result.status, 7, result.stderr);
    assert.ok(!result.stdout.includes(FAKE_TOKEN));
    assert.ok(!result.stderr.includes(FAKE_TOKEN));
  });
});

test('the command line refuses an existing GIT_CONFIG_COUNT before it reads a key', () => {
  const result = runScript(['--app', 'factory', '--repo', REPO, '--', process.execPath, '-e', 'process.exit(0)'], { env: { ...noKeyEnv, GIT_CONFIG_COUNT: '1' } });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /GIT_CONFIG_COUNT is already set/);
  assert.doesNotMatch(result.stderr, /YAF_FACTORY_KEY_FILE/);
});

const REFUSED = /^Refused: .* reserved for a human maintainer/;
const refused = (command, ...args) => assert.match(refusedCommand(command, args) ?? '', REFUSED, `${command} ${args.join(' ')}`);
const passes = (command, ...args) => assert.equal(refusedCommand(command, args), null, `${command} ${args.join(' ')}`);

test('refusedCommand refuses gh pr merge', () => {
  refused('gh', 'pr', 'merge', '5');
  refused('gh', 'pr', 'merge', '5', '--auto', '--squash');
  refused('gh', 'pr', '-R', 'owner/name', 'merge', '5');
  // The program is named as Windows accepts it, as in gitRefusal.
  for (const program of ['gh.exe', 'GH', 'gh.', 'gh.exe. ', 'C:\\Program Files\\GitHub CLI\\gh.exe', '/usr/bin/gh']) refused(program, 'pr', 'merge', '5');
  assert.match(refusedCommand('gh', ['pr', 'merge', '5']), /gh pr merge/);
  assert.match(refusedCommand('gh', ['pr', 'merge', '5']), /not a boundary/);
});

test('an approving review is refused', () => {
  refused('gh', 'pr', 'review', '5', '--approve');
  refused('gh', 'pr', 'review', '--approve', '5', '--body', 'fine');
  refused('gh', 'pr', 'review', '5', '-a');
  refused('gh', 'pr', 'review', '5', '-ab', 'fine');
  passes('gh', 'pr', 'review', '5', '--comment', '--body', 'a note');
  passes('gh', 'pr', 'review', '5', '--request-changes', '-b', 'approve it later');
});

test('a push to a protected branch is refused, with git -C too', () => {
  for (const branch of ['main', 'next', 'stable']) {
    refused('git', 'push', 'origin', branch);
    refused('git', 'push', 'origin', `HEAD:${branch}`);
    refused('git', 'push', '-u', 'origin', `ci/40-x:refs/heads/${branch}`);
    refused('git', 'push', 'origin', `HEAD:heads/${branch}`);
    refused('git', '-C', '.', 'push', 'origin', branch);
    refused('git', '-c', 'push.default=current', '-C', '../tree', 'push', 'origin', `HEAD:${branch}`);
  }
  refused('git', 'push', 'origin', 'ci/40-x', 'main');
  refused('git', 'push', 'origin', ':main');
  refused('git', 'push', '--delete', 'origin', 'stable');
  refused('GIT.EXE', '--git-dir', '.git', 'push', 'origin', 'main');
});

test('a forced push is refused', () => {
  refused('git', 'push', '--force', 'origin', 'ci/40-x');
  refused('git', 'push', '--force-with-lease=ci/40-x', 'origin', 'ci/40-x');
  refused('git', 'push', '-f', 'origin', 'ci/40-x');
  refused('git', 'push', 'origin', 'ci/40-x', '-uf');
  refused('git', 'push', 'origin', '+ci/40-x');
  refused('git', '-C', '.', 'push', 'origin', '-f', 'ci/40-x');
  refused('git', 'push', '--mirror', 'origin');
  refused('git', 'push', '--all', 'origin');
  refused('git', 'push', 'origin', '--branches');
});

test('a push of tags is refused', () => {
  refused('git', 'push', '--tags', 'origin');
  refused('git', 'push', '--follow-tags', 'origin', 'ci/40-x');
  refused('git', 'push', 'origin', 'refs/tags/v1.0.0');
  refused('git', 'push', 'origin', 'HEAD:refs/tags/v1.0.0');
  refused('git', 'push', 'origin', 'tags/v1.0.0');
});

test('a merge or an approval through GraphQL and a human-only label on create are refused', () => {
  refused('gh', 'api', 'graphql', '-f', 'query=mutation { mergePullRequest(input: {pullRequestId: "x"}) { clientMutationId } }');
  refused('gh', 'api', 'graphql', '-f', 'query=mutation { enablePullRequestAutoMerge(input: {pullRequestId: "x"}) { clientMutationId } }');
  refused('gh', 'api', 'graphql', '-f', 'query=mutation { addPullRequestReview(input: {pullRequestId: "x", event: APPROVE}) { clientMutationId } }');
  refused('gh', 'api', 'graphql', '-f', 'query=mutation($e: PullRequestReviewEvent!) { submitPullRequestReview(input: {event: $e}) { clientMutationId } }', '-f', 'e=APPROVE');
  passes('gh', 'api', 'graphql', '-f', 'query=mutation { addPullRequestReview(input: {pullRequestId: "x", event: COMMENT}) { clientMutationId } }');
  passes('gh', 'api', 'graphql', '-f', 'query={ repository(owner: "o", name: "n") { pullRequest(number: 5) { mergeable } } }');
  for (const label of ['guard-change', 'major-release', 'size-accepted', 'Size-Accepted']) {
    refused('gh', 'pr', 'create', '--title', 'ci: x', '--label', label);
    refused('gh', 'pr', 'create', '--title', 'ci: x', `--label=factory,${label}`);
    refused('gh', 'issue', 'create', '--title', 'x', '-l', label);
    refused('gh', 'pr', 'edit', '5', '--add-label', label.toUpperCase());
  }
  passes('gh', 'pr', 'create', '--title', 'ci: x', '--body', 'guard-change is asked of a maintainer', '--label', 'factory');
  passes('gh', 'issue', 'create', '--title', 'size-accepted was refused', '-l', 'needs-human');
});

test('tags, releases, the human-only labels and repository settings are refused', () => {
  refused('git', 'tag', 'v1.0.0');
  refused('git', '-C', '.', 'tag', '-d', 'v1.0.0');
  refused('gh', 'release', 'create', 'v1.0.0');
  refused('gh', 'label', 'create', 'x');
  refused('gh', 'repo', 'edit', '--enable-auto-merge');
  refused('gh', 'secret', 'list');
  refused('gh', 'variable', 'set', 'FACTORY_PAUSED', '--body', 'false');
  refused('gh', 'variable', 'delete', 'FACTORY_PAUSED');
  refused('gh', 'workflow', 'disable', 'factory-gates.yml');
  for (const label of ['guard-change', 'major-release', 'size-accepted']) {
    refused('gh', 'pr', 'edit', '5', '--add-label', label);
    refused('gh', 'pr', 'edit', '5', `--add-label=blocked,${label}`);
    refused('gh', 'issue', 'edit', '5', '--remove-label', label);
    refused('gh', 'api', 'repos/o/n/issues/5/labels', '-f', `labels[]=${label}`);
    refused('gh', 'api', '-X', 'DELETE', `repos/o/n/issues/5/labels/${label}`);
  }
  refused('gh', 'api', '-X', 'POST', 'repos/o/n/pulls/5/reviews', '-f', 'event=APPROVE');
  refused('gh', 'api', '-X', 'PUT', 'repos/o/n/pulls/5/merge');
  refused('gh', 'api', 'repos/o/n/merge-queue');
  refused('gh', 'api', 'repos/o/n/rulesets');
  refused('gh', 'api', '-X', 'DELETE', 'repos/o/n/branches/main/protection');
  refused('gh', 'api', 'repos/o/n/environments/factory-review');
});

test('reading and a push of a ticket branch pass', () => {
  passes('gh', 'pr', 'view', '5', '--json', 'state');
  passes('gh', 'pr', 'create', '--title', 'ci: merge the lists', '--body', 'Closes #40', '--base', 'main');
  passes('gh', 'pr', 'edit', '5', '--add-label', 'needs-human');
  passes('gh', 'issue', 'edit', '40', '--add-label', 'blocked');
  passes('gh', 'issue', 'comment', '40', '--body', 'size-accepted is a label for a human');
  passes('gh', 'variable', 'list');
  passes('gh', 'workflow', 'view', 'factory-gates.yml');
  passes('gh', 'repo', 'view');
  passes('gh', 'api', 'repos/o/n/pulls/5');
  passes('gh', 'api', 'repos/o/n/pulls/5/comments');
  passes('gh', 'api', 'repos/o/n/issues/5/labels', '-f', 'labels[]=blocked');
  passes('git', 'push', '-u', 'origin', 'ci/40-refused-commands');
  passes('git', '-C', '../tree', 'push', '-u', 'origin', 'ci/40-refused-commands');
  passes('git', 'push', 'origin', 'HEAD:fix/41-main');
  passes('git', 'push', 'origin', 'main:ci/40-from-main');
  passes('git', 'fetch', 'origin', 'main');
  passes('git', 'log', '--oneline', 'origin/main');
  passes('git', 'commit', '-m', 'ci: tag and push -f');
  passes('node', '.github/scripts/local-review.mjs', '5');
  passes(process.execPath, '-e', 'process.exit(0)');
});

test('the command line exits 2 on a refused command', () => {
  // No key is configured: a run that went on to mint would fail on the key, with exit code 1.
  for (const command of [['gh', 'pr', 'merge', '5'], ['git', '-C', '.', 'push', 'origin', 'main']]) {
    const result = runScript(['--app', 'factory', '--repo', REPO, '--', ...command]);
    assert.equal(result.status, 2, command.join(' '));
    assert.match(result.stderr, REFUSED);
    assert.doesNotMatch(result.stderr, /YAF_FACTORY_KEY_FILE/);
    assert.equal(result.stdout, '');
  }
});

test('runCommand returns 1 for a child that ended by a signal', async () => {
  const messages = [];
  const code = await runCommand(process.execPath, ['-e', "process.kill(process.pid, 'SIGKILL')"], process.env, (line) => messages.push(line));
  assert.equal(code, 1);
  // Windows has no signals: the child ends with exit code 1 and nothing is said.
  if (process.platform !== 'win32') assert.match(messages[0], /ended by signal SIGKILL/);
});

test("runCommand returns the child's exit code", async () => {
  const messages = [];
  const warn = (line) => messages.push(line);
  assert.equal(await runCommand(process.execPath, ['-e', 'process.exit(3)'], process.env, warn), 3);
  assert.equal(await runCommand(process.execPath, ['-e', 'process.exit(0)'], process.env, warn), 0);
  assert.deepEqual(messages, []);
});

test('runCommand hands the child the environment it was given, and its arguments without a shell', async () => {
  const env = { ...process.env, GH_TOKEN: FAKE_TOKEN };
  const check = `process.exit(process.env.GH_TOKEN === '${FAKE_TOKEN}' ? 0 : 4)`;
  assert.equal(await runCommand(process.execPath, ['-e', check], env, neverWarn), 0);
  assert.equal(await runCommand(process.execPath, ['-e', check], { ...env, GH_TOKEN: 'another' }, neverWarn), 4);
  const unparsed = 'a b && exit 9 ; $HOME %PATH% "q"';
  assert.equal(await runCommand(process.execPath, ['-e', 'process.exit(process.argv[1] === process.env.EXPECTED ? 0 : 5)', unparsed], { ...env, EXPECTED: unparsed }, neverWarn), 0);
});

test('runCommand returns 1 with a message for a command that does not exist', async () => {
  const messages = [];
  const code = await runCommand('no-such-command-factory-token-test', ['--flag'], { ...process.env, GH_TOKEN: FAKE_TOKEN }, (line) => messages.push(line));
  assert.equal(code, 1);
  assert.equal(messages.length, 1);
  assert.match(messages[0], /Could not start "no-such-command-factory-token-test"/);
  assert.match(messages[0], /\.cmd or \.bat file must be wrapped in its interpreter/);
  assert.ok(!messages[0].includes(FAKE_TOKEN));
});
