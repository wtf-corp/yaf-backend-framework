import test from 'node:test';
import assert from 'node:assert/strict';
import { inProgressCheck } from '../lib.mjs';

test('inProgressCheck builds a check run in progress without a conclusion', () => {
  const sha = 'a'.repeat(40);
  const body = inProgressCheck('factory/review', sha, 'the review is running');
  assert.deepEqual(body, {
    name: 'factory/review',
    head_sha: sha,
    status: 'in_progress',
    output: { title: 'the review is running', summary: 'the review is running' },
  });
  assert.equal('conclusion' in body, false);
  assert.equal('started_at' in body, false); // GitHub stamps the start, not the local clock
});
