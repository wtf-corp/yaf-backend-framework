import test from 'node:test';
import assert from 'node:assert/strict';
import { addItem } from '../project.mjs';
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
