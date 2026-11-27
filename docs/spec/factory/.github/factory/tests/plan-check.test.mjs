import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { loadPolicy } from '../lib.mjs';
import { planCheck } from '../plan-check.mjs';

const policy = loadPolicy();
const none = () => false;
const script = path.join(path.dirname(fileURLToPath(import.meta.url)), '..', 'plan-check.mjs');

/** A plan with a delta block and a Tickets table of the given rows (Key, Title, Type, Depends on, Touches, Tier). */
const plan = (rows) => [
  '# Plan', '', '```factory:delta', '{ "publicApi": [], "schema": [], "errorCodes": [] }', '```', '',
  '## Tickets', '',
  '| Key | Title | Type | Depends on | Touches | Tier | Estimate | Failing test first | Verify |',
  '|---|---|---|---|---|---|---|---|---|',
  ...rows.map(([key, title, type, deps, touches, tier]) => `| ${key} | ${title} | ${type} | ${deps} | ${touches} | ${tier} | 20 | \`X_Y_Z\` fails | \`node --test\` |`),
  '',
].join('\n');

const overlapAndLow = plan([
  ['T1', 'test: cover the reader', 'test', '—', '`tests/A.Tests/ReaderTests.cs`', 'low'],
  ['T2', 'ci: run the reader', 'chore', '—', '`tests/A.Tests/ReaderTests.cs`, `.github/workflows/reader.yml`', 'low'],
]);

test('planCheck_OverlapAndLowTier_GivesBothProblems', () => {
  const { problems } = planCheck(overlapAndLow, { policy, exists: none });
  assert.equal(problems.length, 2, problems.join('\n'));
  assert.match(problems.join('\n'), /T1 and T2: touches overlap/);
  assert.match(problems.join('\n'), /T2: declared tier low is below the floor high/);
});

test('planCheck_CleanPlan_HasNoProblems', () => {
  const text = plan([
    ['T1', 'test: cover the reader', 'test', '—', '`tests/A.Tests/ReaderTests.cs`', 'low'],
    ['T2', 'docs: describe the reader', 'docs', 'T1', '`docs/articles/reader.md`', 'low'],
  ]);
  assert.deepEqual(planCheck(text, { policy, exists: none }), { problems: [], notes: [] });
});

test('planCheck_Notes_DoNotFail', () => {
  // A tier above the floor and a T<n> of another plan in the prose are notes, not problems.
  const text = plan([['T1', 'test: cover the reader', 'test', '—', '`tests/A.Tests/ReaderTests.cs`', 'high']])
    + '\nAs T9 of plan 0013 did.\n';
  const { problems, notes } = planCheck(text, { policy, exists: none });
  assert.deepEqual(problems, []);
  assert.equal(notes.length, 2, notes.join('\n'));
  assert.match(notes.join('\n'), /T1: declared tier high is above the floor low/);
  assert.match(notes.join('\n'), /T9: not a key of this plan/);
});

test('planCheck_TicketProblemsAndInvalidDelta_AreProblems', () => {
  const text = plan([['T1', 'test: cover the reader', 'test', 'T7', '`tests/A.Tests/ReaderTests.cs`', 'low']])
    .replace('"errorCodes": []', '"errorCodes": ["Core.Bad"]');
  const { problems } = planCheck(text, { policy, exists: none });
  assert.match(problems.join('\n'), /T1: depends on unknown ticket T7/);
  assert.match(problems.join('\n'), /factory:delta errorCodes: "Core\.Bad"/);
});

test('cli_PlanWithProblems_ExitsOne', () => {
  const dir = mkdtempSync(path.join(tmpdir(), 'plan-check-'));
  const file = path.join(dir, 'plan.md');
  writeFileSync(file, overlapAndLow);
  const run = spawnSync(process.execPath, [script, file], { encoding: 'utf8' });
  assert.equal(run.status, 1, run.stderr);
  assert.match(run.stdout, /2 problem\(s\)/);
  assert.match(run.stdout, /T1 and T2: touches overlap/);
  assert.match(run.stdout, /note\(s\)/);
});
