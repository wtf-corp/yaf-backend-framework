// Token helper for running the factory locally (plan specs/0002-local-factory-operation, T2 to T5).
// Node 22+, no dependencies. This part builds the JWT of a GitHub App and mints an installation token
// scoped to one repository. The key and the JWT exist only in memory; no message holds them or a token.
import crypto from 'node:crypto';
import fs from 'node:fs';
import { execSync } from 'node:child_process';

const API = 'https://api.github.com';

/** The two apps of the factory. App IDs and variable names are identifiers, not secrets. */
export const APPS = {
  factory: { appId: 5834995, keyVar: 'YAF_FACTORY_KEY_FILE' },
  reviewer: { appId: 5837737, keyVar: 'YAF_REVIEWER_KEY_FILE' },
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
