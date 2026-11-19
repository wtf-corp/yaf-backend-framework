// Tests of factory-token.mjs. No network call and no stored key: the key pair is generated here,
// and the token strings are fakes the assertions look for.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import { APPS, buildJwt, mintToken } from '../factory-token.mjs';

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

test('APPS names the app id and the key variable of both apps', () => {
  assert.deepEqual(APPS, {
    factory: { appId: 5834995, keyVar: 'YAF_FACTORY_KEY_FILE' },
    reviewer: { appId: 5837737, keyVar: 'YAF_REVIEWER_KEY_FILE' },
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
