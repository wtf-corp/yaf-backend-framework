import test from 'node:test';
import assert from 'node:assert/strict';
import { planRefs } from '../plan-refs.mjs';

/** A plan with the given prose, Test strategy rows (What | Ticket) and Tickets rows ([key, title]). */
const plan = ({ prose = 'Nothing.', strategy = [], tickets = [['T1', 'chore: one']] } = {}) => `# Plan

${prose}

## Test strategy

| What must be true | Ticket | Check |
|---|---|---|
${strategy.map(([what, ticket]) => `| ${what} | ${ticket} | a check |`).join('\n')}

## Tickets

| Key | Title | Depends on | Touches | Failing test first | Verify |
|---|---|---|---|---|---|
${tickets.map(([key, title]) => `| ${key} | ${title} | — | a | a test | a command |`).join('\n')}
`;

test('refs_UnknownKeyInTestStrategy_IsAProblem', () => {
  const { problems } = planRefs(plan({ strategy: [['It works', 'T1, T2']] }));
  assert.equal(problems.length, 1);
  assert.match(problems[0], /T2/);
  assert.match(problems[0], /Test strategy/);
});

test('refs_KnownKeysInTestStrategy_AreNoProblem', () => {
  const { problems, notes } = planRefs(plan({ strategy: [["Plan 0013's T65 exists", 'T1']] }));
  assert.deepEqual(problems, []);
  assert.equal(notes.length, 1); // T65 in the What column is prose: a note
});

test('refs_MergedKeyRemoved_IsAProblem', () => {
  const base = plan({ tickets: [['T1', 'chore: one'], ['T5', 'chore: five']] });
  const { problems } = planRefs(plan(), { base });
  assert.equal(problems.length, 1);
  assert.match(problems[0], /T5/);
  assert.match(problems[0], /base/);
});

test('refs_UnknownKeyInProse_IsANote', () => {
  const { problems, notes } = planRefs(plan({ prose: 'As T1 and T9 do.' }), { research: 'Line one.\nPlan 0013 T23 overlaps.\n' });
  assert.deepEqual(problems, []);
  assert.equal(notes.length, 2);
  assert.match(notes.find((n) => n.startsWith('T9')), /plan\.md:3/);
  assert.match(notes.find((n) => n.startsWith('T23')), /research\.md:2/);
});

test('refs_KeyRetitledSinceThePreviousCommit_IsANoteWithItsLines', () => {
  const tickets = [['T1', 'chore: one'], ['T44', 'chore: new title']];
  const previous = plan({ tickets: [['T1', 'chore: one'], ['T44', 'chore: old title']] });
  const research = 'Intro.\n\nT44 does it.\n';
  const { problems, notes } = planRefs(plan({ prose: 'See T44.', tickets }), { research, previous, base: previous });
  assert.deepEqual(problems, []);
  assert.equal(notes.length, 1);
  assert.match(notes[0], /^T44: title changed since the previous commit/);
  assert.match(notes[0], /chore: old title/);
  assert.match(notes[0], /plan\.md:3/);
  assert.match(notes[0], /research\.md:3/);
  assert.doesNotMatch(notes[0], /plan\.md:1[0-9]/); // its own row in the Tickets table is no citation
});

test('refs_KeyRetitledOnlySinceTheBase_SaysTheBase', () => {
  const base = plan({ tickets: [['T1', 'chore: first']] });
  const { notes } = planRefs(plan(), { previous: plan(), base });
  assert.equal(notes.length, 1);
  assert.match(notes[0], /since the base version/);
});

test('refs_ReferencesInCodeSpans_AreRead', () => {
  const { problems, notes } = planRefs(plan({ prose: 'Run `check T7`.', strategy: [['It works', '`T3`']] }));
  assert.equal(problems.length, 1);
  assert.match(problems[0], /T3/);
  assert.equal(notes.length, 1);
  assert.match(notes[0], /^T7/);
});

test('refs_NoPreviousNoBase_OnlyChecksTheHead', () => {
  assert.deepEqual(planRefs(plan({ prose: 'T1 only.', strategy: [['x', 'T1']] })), { problems: [], notes: [] });
});
