// Tests of factory-token.mjs. No network call and no stored key: the key pair is generated here,
// and the token strings are fakes the assertions look for.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { APPS, buildJwt, childEnv, keyFileProblem, loadKey, mintToken, repoFromRemote } from '../factory-token.mjs';

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
