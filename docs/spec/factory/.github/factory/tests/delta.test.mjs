import test from 'node:test';
import assert from 'node:assert/strict';
import { loadPolicy, parseDiff } from '../lib.mjs';
import { actualDelta, compareDelta, deltaVerdict, isEmptyDelta } from '../delta.mjs';
import { parseDelta, parseTickets, planFlags, ticketProblems, hasDeviations } from '../plan.mjs';

const policy = loadPolicy();

const plan = `# 0003 — Typed ids: plan

## Public API delta

\`\`\`factory:delta
{ "publicApi": ["YAF.Core.ITypedId<TSelf>*", "YAF.Core.TypedIdAttribute"], "errorCodes": ["core.ids.*"], "schema": [], "dependencies": [] }
\`\`\`

## Deviations from the specification

none

## Tickets

| Key | Title | Requirements | Depends on | Touches | Tier | Size | Failing test first | Verify |
|---|---|---|---|---|---|---|---|---|
| T1 | feat(core): typed id contract | 02 §2 | — | src/Core/Ids/** | medium | 120 | \`TypedIdTests.New_is_v7\` | \`dotnet test tests/Core.Tests\` |
| T2 | feat(analyzers): typed id generator | 02 §2 | T1 | src/Analyzers/Ids/** | high | 180 | \`GeneratorTests.Emits_struct\` | \`dotnet test tests/Analyzers.Tests\` |
`;

const diffText = (file, lines) => `diff --git a/${file} b/${file}\n--- a/${file}\n+++ b/${file}\n@@ -1,0 +1,${lines.length} @@\n${lines.join('\n')}\n`;

test('the delta block and the tickets table are read from the plan', () => {
  const delta = parseDelta(plan);
  assert.deepEqual(delta.publicApi, ['YAF.Core.ITypedId<TSelf>*', 'YAF.Core.TypedIdAttribute']);
  const tickets = parseTickets(plan);
  assert.equal(tickets.length, 2);
  assert.deepEqual(tickets[1].dependsOn, ['T1']);
  assert.equal(tickets[1].tier, 'high');
  assert.equal(tickets[0].size, 120);
  assert.equal(hasDeviations(plan), false);
});

test('a declared addition is clean; tier medium passes', () => {
  const diff = parseDiff(diffText('src/Core/PublicAPI.Unshipped.txt', ['+YAF.Core.ITypedId<TSelf>.Value.get -> System.Guid', '+YAF.Core.TypedIdAttribute.TypedIdAttribute() -> void']));
  const actual = actualDelta(diff, policy);
  const cmp = compareDelta(actual, parseDelta(plan));
  assert.equal(cmp.undeclared.length, 0);
  assert.equal(deltaVerdict('medium', actual, cmp).pass, true);
  assert.equal(deltaVerdict('low', actual, cmp).pass, false); // low may not change the approved files at all
});

test('an undeclared addition fails tier medium', () => {
  const diff = parseDiff(diffText('src/Core/PublicAPI.Unshipped.txt', ['+YAF.Core.Sneaky.Backdoor() -> void']));
  const actual = actualDelta(diff, policy);
  const cmp = compareDelta(actual, parseDelta(plan));
  assert.equal(cmp.undeclared.length, 1);
  assert.equal(deltaVerdict('medium', actual, cmp).pass, false);
});

test('a removed entry is never covered by a declaration', () => {
  const diff = parseDiff(diffText('tests/Core.Tests/errors.approved.txt', ['-core.ids.invalid', '+core.ids.malformed']));
  const actual = actualDelta(diff, policy);
  const cmp = compareDelta(actual, parseDelta(plan));
  assert.equal(cmp.undeclared.length, 0);
  assert.equal(cmp.removed.length, 1);
  assert.equal(deltaVerdict('medium', actual, cmp).pass, false);
});

test('a plan without a block declares nothing', () => {
  const diff = parseDiff(diffText('src/Core/PublicAPI.Unshipped.txt', ['+YAF.Core.X']));
  const cmp = compareDelta(actualDelta(diff, policy), null);
  assert.equal(cmp.undeclared.length, 1);
});

test('files outside the approved sources, comments and moved lines are no delta', () => {
  const diff = parseDiff(diffText('src/Core/Result.cs', ['+public sealed class Result {}']) + diffText('src/Core/PublicAPI.Unshipped.txt', ['-YAF.Core.A', '+YAF.Core.A', '+#nullable enable']));
  assert.equal(isEmptyDelta(actualDelta(diff, policy)), true);
});

test('plan flags: public API, high tickets, deviations', () => {
  const flags = planFlags(plan, policy);
  assert.equal(flags.flagged, true);
  assert.ok(flags.reasons.some((r) => r.includes('publicApi')));
  assert.ok(flags.reasons.some((r) => r.includes('T2')));
  const quiet = plan.replace(/\{ "publicApi".*\}/, '{ "publicApi": [], "errorCodes": [], "schema": [], "dependencies": [] }').replace('| high |', '| low |');
  assert.equal(planFlags(quiet, policy).flagged, false);
  assert.equal(planFlags(quiet.replace('\nnone\n', '\n05 §4: uses a lease row instead of an advisory lock\n'), policy).flagged, true);
  assert.equal(planFlags(quiet.replace(/```factory:delta[\s\S]*?```/, ''), policy).flagged, true);
});

test('ticket list problems a script can decide', () => {
  const tickets = parseTickets(plan);
  assert.deepEqual(ticketProblems(tickets, policy), []);
  const bad = parseTickets(plan.replace('| T1 | medium', '| T1 | medium').replace('| — | src/Core/Ids/** | medium | 120', `| T2 | src/Core/Ids/** | medium | ${policy.size.soft + 1}`));
  const problems = ticketProblems(bad, policy);
  assert.ok(problems.some((p) => p.includes('comes later')));
  assert.ok(problems.some((p) => p.includes('soft limit')));
});
