import test from 'node:test';
import assert from 'node:assert/strict';
import { ticketBody } from '../create-tickets.mjs';
import { parseTickets } from '../plan.mjs';

const plan = `# Plan

## Tickets

| Key | Title | Depends on | Touches | Failing test first | Verify |
|---|---|---|---|---|---|
| T1 | chore: x | — | \`src/Analyzers/Domain/\`, \`.editorconfig\` (three sections, YAF2011 only), src/Domain/AggregateRoot{TId,TSelf,TMemento}.cs | a test | a command |
`;

const body = ticketBody('specs/0001-x', parseTickets(plan)[0], [], 1).split('\n');

test('ticketBody_TouchesWithBackticks_WritesTheEntryAsItIs', () => {
  assert.ok(body.includes('- `.editorconfig` (three sections, YAF2011 only)'), body.join('\n'));
});

test('ticketBody_BarePath_WrapsItInBackticks', () => {
  assert.ok(body.includes('- `src/Analyzers/Domain/`'), body.join('\n'));
  assert.ok(body.includes('- `src/Domain/AggregateRoot{TId,TSelf,TMemento}.cs`'), body.join('\n'));
});
