import test from 'node:test';
import assert from 'node:assert/strict';
import { inProgressCheck, statusBody } from '../lib.mjs';

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

test('statusBody builds a commit status and cuts the description to 140 characters', () => {
  assert.deepEqual(statusBody('factory/tier', 'failure', 'x'.repeat(300), 'https://example.test/run'), {
    context: 'factory/tier',
    state: 'failure',
    description: 'x'.repeat(140),
    target_url: 'https://example.test/run',
  });
});

test('statusBody maps success through and leaves out target_url when the run is unknown', () => {
  const body = statusBody('factory/size', 'success', 'within the limit');
  assert.deepEqual(body, { context: 'factory/size', state: 'success', description: 'within the limit' });
  assert.equal('target_url' in body, false);
});
