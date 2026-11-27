import test from 'node:test';
import assert from 'node:assert/strict';
import { addItem, setStatus } from '../project.mjs';
import { firstStatus } from '../create-tickets.mjs';

process.env.FACTORY_PROJECT_OWNER = 'acme';
process.env.FACTORY_PROJECT_NUMBER = '1';

const url = 'https://github.com/acme/app/issues/8';
const item = { id: 'PVTI_8', content: { number: 8, url } };
const other = { id: 'PVTI_7', content: { number: 7, url: 'https://github.com/acme/app/issues/7' } };
const exists = () => new Error(`Command failed: gh project item-add 1 --owner acme --url ${url} --format json\nGraphQL: Content already exists in this project (addProjectV2ItemById)\n`);

/** A fake `gh` runner: `add` answers item-add (or throws), `lists` are the answers of successive item-list calls. */
function fake(add, lists = []) {
  const calls = [];
  const waits = [];
  const gh = (args) => {
    calls.push(args.slice(0, 2).join(' '));
    if (args[1] === 'item-add') return add(args);
    return { items: lists.shift() ?? [] };
  };
  return { calls, waits, options: { gh, wait: (ms) => waits.push(ms) } };
}

test('addItem returns the id that item-add gives', () => {
  const f = fake(() => ({ id: 'PVTI_8' }));
  assert.equal(addItem(url, f.options), 'PVTI_8');
  assert.deepEqual(f.calls, ['project item-add']);
});

test('addItem returns the existing item when the Project already holds the issue', () => {
  const f = fake(() => { throw exists(); }, [[other, item]]);
  assert.equal(addItem(url, f.options), 'PVTI_8');
  assert.deepEqual(f.calls, ['project item-add', 'project item-list']);
  assert.deepEqual(f.waits, []);
});

test('addItem reads the list again until the item shows', () => {
  const f = fake(() => { throw exists(); }, [[other], [other], [other, item]]);
  assert.equal(addItem(url, f.options), 'PVTI_8');
  assert.deepEqual(f.calls, ['project item-add', 'project item-list', 'project item-list', 'project item-list']);
  assert.deepEqual(f.waits, [2000, 2000]);
});

test('addItem rethrows any other error', () => {
  const error = new Error('Command failed: gh project item-add\nGraphQL: Could not resolve to a ProjectV2\n');
  const f = fake(() => { throw error; }, [[item]]);
  assert.throws(() => addItem(url, f.options), (e) => e === error);
  assert.deepEqual(f.calls, ['project item-add']);
});

test('addItem rethrows the same error when no item matches after the last read', () => {
  const error = exists();
  const f = fake(() => { throw error; }, [[other], [other], [other], [item]]);
  assert.throws(() => addItem(url, f.options), (e) => e === error);
  assert.equal(f.calls.length, 4); // item-add and three reads
  assert.deepEqual(f.waits, [2000, 2000]);
});

/**
 * A fake Project behind `gh` on a fake clock: sleeping moves the clock. `editFails` is how many
 * item-edit calls throw GraphQL's rate-limit error before one succeeds.
 */
function fakeProject({ editFails = 0 } = {}) {
  let t = 1_800_000_000_000;
  const calls = [];
  const waits = [];
  const gh = (args) => {
    calls.push({ what: args[1], at: t });
    if (args[1] === 'view') return { id: 'PVT_1' };
    if (args[1] === 'field-list') return { fields: [{ name: 'Status', id: 'F_1', options: [{ name: 'Ready', id: 'O_R' }] }] };
    if (args[1] === 'item-add') return { id: `PVTI_${args.at(-1).split('/').pop()}` };
    if (args[1] === 'item-edit' && editFails-- > 0) throw new Error('Command failed: gh project item-edit --format json\nGraphQL: API rate limit exceeded\n');
    return {};
  };
  const rateLimit = () => ({ resources: { graphql: { remaining: 0, reset: (t + 30_000) / 1000 } } });
  const options = { gh, now: () => t, sleep: (ms) => { waits.push(ms); t += ms; }, rateLimit };
  return { calls, waits, options };
}

test('setStatus_GraphQLRateLimitOnce_WaitsForTheResetAndRetries', () => {
  const p = fakeProject({ editFails: 1 });
  setStatus(url, 'Ready', p.options);
  // The pace after item-add (a write), then one wait of 30 s for the reset; no more.
  assert.deepEqual(p.waits, [1000, 30_000]);
  assert.deepEqual(p.calls.map((c) => c.what), ['view', 'field-list', 'item-add', 'item-edit', 'item-edit']);
});

test('setStatus_TwoTickets_WritesAreASecondApart', () => {
  const p = fakeProject();
  setStatus(url, 'Ready', p.options);
  setStatus(other.content.url, 'Ready', p.options);
  const writes = p.calls.filter((c) => c.what === 'item-add' || c.what === 'item-edit');
  assert.equal(writes.length, 4);
  for (let i = 1; i < writes.length; i++) assert.ok(writes[i].at - writes[i - 1].at >= 1000, `write ${i} came ${writes[i].at - writes[i - 1].at} ms after the one before`);
});

test('an existing open ticket with no status gets its first status', () => {
  const open = { number: 8, state: 'open' };
  assert.equal(firstStatus(open, [], new Map()), 'Ready');
  assert.equal(firstStatus(open, [], new Map([[8, null]])), 'Ready');
  assert.equal(firstStatus(open, [7], new Map()), 'Backlog');
});

test('an existing ticket with a status, or a closed one, is left alone', () => {
  assert.equal(firstStatus({ number: 8, state: 'open' }, [], new Map([[8, 'In progress']])), null);
  assert.equal(firstStatus({ number: 8, state: 'open' }, [7], new Map([[8, 'Ready']])), null);
  assert.equal(firstStatus({ number: 8, state: 'closed' }, [], new Map()), null);
});
