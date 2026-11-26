import test from 'node:test';
import assert from 'node:assert/strict';
import { parseTickets } from '../plan.mjs';

/** One ticket row with the given Depends on and Touches cells, parsed. */
const row = (touches, dependsOn = '—') => parseTickets(`# Plan

## Tickets

| Key | Title | Depends on | Touches | Failing test first | Verify |
|---|---|---|---|---|---|
| T1 | chore: x | ${dependsOn} | ${touches} | a test | a command |
`)[0];

test('parseTickets_TouchesWithCommaInBraces_KeepsThePathWhole', () => {
  assert.deepEqual(
    row('`src/Domain/AggregateRoot{TId,TSelf,TMemento}.cs`, `src/Domain/PublicAPI.Unshipped.txt`').touches,
    ['src/Domain/AggregateRoot{TId,TSelf,TMemento}.cs', 'src/Domain/PublicAPI.Unshipped.txt'],
  );
});

test('parseTickets_TouchesWithCommaInBracesWithoutBackticks_KeepsThePathWhole', () => {
  assert.deepEqual(
    row('src/Testing/InMemoryRepository{TAggregate,TId}.cs, tests/Testing.Tests/').touches,
    ['src/Testing/InMemoryRepository{TAggregate,TId}.cs', 'tests/Testing.Tests/'],
  );
});

test('parseTickets_TouchesWithNoteAfterPath_KeepsTheEntryAsWritten', () => {
  assert.deepEqual(
    row('`src/Analyzers/Domain/`, `.editorconfig` (three sections, YAF2011 only)').touches,
    ['src/Analyzers/Domain/', '`.editorconfig` (three sections, YAF2011 only)'],
  );
});

test('parseTickets_TouchesWithSeparatorsInCodeSpanParenthesesOrBrackets_DoesNotSplitThere', () => {
  assert.deepEqual(row('`a,b;c`, d (e; f), g[h,i]').touches, ['a,b;c', 'd (e; f)', 'g[h,i]']);
});

test('parseTickets_TouchesWithSeparatorsAtDepthZero_SplitsAtEach', () => {
  assert.deepEqual(row('`a`; b<br>c<br/>`d`, e').touches, ['a', 'b', 'c', 'd', 'e']);
});

test('parseTickets_TouchesWithUnmatchedClosingBracket_IgnoresIt', () => {
  assert.deepEqual(row('a), b').touches, ['a)', 'b']);
});

test('parseTickets_TouchesWithUnclosedBrace_KeepsTheRestAsOneEntry', () => {
  assert.deepEqual(row('a, b{c, d').touches, ['a', 'b{c, d']);
});

test('parseTickets_TouchesWithLoneBacktick_TreatsItAsPlainText', () => {
  assert.deepEqual(row('`a, b').touches, ['`a', 'b']);
  assert.deepEqual(row('a`b, c').touches, ['a`b', 'c']);
});

test('parseTickets_DependsOn_SplitsKeysAndDropsDash', () => {
  assert.deepEqual(row('x', 'T1, T2').dependsOn, ['T1', 'T2']);
  assert.deepEqual(row('x', '—').dependsOn, []);
});
