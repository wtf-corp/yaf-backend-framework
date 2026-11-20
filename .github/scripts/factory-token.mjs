// Token helper for running the factory locally (plan specs/0002-local-factory-operation, T2 to T5).
// Node 22+, no dependencies. This part builds the JWT of a GitHub App and mints an installation token
// scoped to one repository. The key and the JWT exist only in memory; no message holds them or a token.
import crypto from 'node:crypto';

const API = 'https://api.github.com';

/** The two apps of the factory. App IDs and variable names are identifiers, not secrets. */
export const APPS = {
  factory: { appId: 5834995, keyVar: 'YAF_FACTORY_KEY_FILE' },
  reviewer: { appId: 5837737, keyVar: 'YAF_REVIEWER_KEY_FILE' },
};

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
