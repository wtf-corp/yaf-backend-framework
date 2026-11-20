import test from 'node:test';
import assert from 'node:assert/strict';
import { inProgressCheck } from '../lib.mjs';

test('inProgressCheck builds a check run in progress without a conclusion', () => {
  const sha = 'a'.repeat(40);
  const now = Date.parse('2026-11-20T10:00:00Z');
  const body = inProgressCheck('factory/review', sha, 'the review is running', now);
  assert.deepEqual(body, {
    name: 'factory/review',
    head_sha: sha,
    status: 'in_progress',
    started_at: '2026-11-20T10:00:00.000Z',
    output: { title: 'the review is running', summary: 'the review is running' },
  });
  assert.equal('conclusion' in body, false);
  assert.equal(inProgressCheck('factory/doc-review', sha, 'x', new Date(now)).started_at, body.started_at);
});
