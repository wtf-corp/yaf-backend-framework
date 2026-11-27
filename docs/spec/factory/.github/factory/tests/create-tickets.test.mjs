import test from 'node:test';
import assert from 'node:assert/strict';
import { ticketBody, createFromPlan, indexByMarker, ticketMarker, epicMarker } from '../create-tickets.mjs';
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

const folder = 'specs/0001-x';
const planFile = `${folder}/plan.md`;
const threeTickets = `# X: plan

## Tickets

| Key | Title | Depends on | Touches | Failing test first | Verify |
|---|---|---|---|---|---|
| T1 | chore: one | — | \`a.mjs\` | a test | a command |
| T2 | chore: two | T1 | \`b.mjs\` | a test | a command |
| T3 | chore: three | T2 | \`c.mjs\` | a test | a command |
`;
const policy = { size: { soft: 400 } };
const issue = (number, text, extra = {}) => ({ number, id: number * 10, state: 'open', html_url: `https://github.com/acme/app/issues/${number}`, body: text, ...extra });
const listing = [
  issue(14, `${ticketMarker('specs/0002-other', 'T3')}\nanother plan`),
  issue(13, `${ticketMarker(folder, 'T3')}\na pull request`, { pull_request: {} }),
  issue(12, `${ticketMarker(folder, 'T2')}\nbody`),
  issue(11, `${ticketMarker(folder, 'T1')}\nbody`),
  issue(10, `${epicMarker(folder)}\nPlan`),
];
const included = (status, body, headers = '') => `HTTP/2.0 ${status}\ncontent-type: application/json\n${headers}\n${JSON.stringify(body)}`;

/** A fake `gh`: answers `gh api -i` like GitHub; `fail(endpoint, method)` may throw instead. */
function fakeGh(fail = () => {}) {
  const calls = [];
  const project = [];
  const run = (cmd, args, options = {}) => {
    const endpoint = args[2];
    const method = args[args.indexOf('--method') + 1];
    calls.push({ endpoint, method, input: options.input ? JSON.parse(options.input) : undefined });
    fail(endpoint, method);
    if (endpoint.includes('issues?state=all')) return included('200 OK', endpoint.includes('page=1') ? listing : []);
    if (method === 'POST' && endpoint === 'repos/acme/app/issues') return included('201 Created', issue(20, JSON.parse(options.input).body));
    return included('201 Created', {});
  };
  const options = {
    run,
    repo: 'acme/app',
    read: () => threeTickets,
    project: {
      addItem: (url) => project.push(['add', url]),
      setStatus: (url, status) => project.push(['status', url, status]),
      statuses: () => new Map([[11, 'Done'], [12, 'Backlog']]),
    },
    retry: { budgetMs: 0, sleep: () => {}, now: () => 0, rateLimit: () => null },
  };
  return { calls, project, options };
}

test('createFromPlan_TwoOfThreeExist_ListsTheIssuesOnceAndSearchesNever', () => {
  const f = fakeGh();
  createFromPlan(planFile, policy, f.options);
  assert.equal(f.calls.filter((c) => c.endpoint.includes('search/issues')).length, 0);
  assert.deepEqual(f.calls.filter((c) => c.endpoint.startsWith('repos/acme/app/issues?state=all')).map((c) => c.endpoint), ['repos/acme/app/issues?state=all&per_page=100&page=1']);
  const created = f.calls.filter((c) => c.method === 'POST' && c.endpoint === 'repos/acme/app/issues');
  assert.equal(created.length, 1);
  assert.equal(created[0].input.title, 'chore: three');
  assert.ok(created[0].input.body.includes('Blocked by: #12'), created[0].input.body);
  assert.deepEqual(f.project.filter((p) => p[0] === 'status'), [['status', 'https://github.com/acme/app/issues/20', 'Backlog']]);
});

test('indexByMarker_PullRequestsAndOtherPlans_AreLeftOut', () => {
  const index = indexByMarker(listing, folder);
  assert.deepEqual([...index.keys()].sort(), [epicMarker(folder), ticketMarker(folder, 'T1'), ticketMarker(folder, 'T2')].sort());
  assert.equal(index.get(ticketMarker(folder, 'T1')).number, 11);
});

test('createFromPlan_RateLimitBudgetUsedUp_NamesTheNextTicketAndTheDispatch', () => {
  const limited = Object.assign(new Error('gh: API rate limit exceeded (HTTP 403)'), {
    stdout: included('403 Forbidden', { message: 'API rate limit exceeded' }, 'retry-after: 60\n'),
  });
  const f = fakeGh((endpoint, method) => { if (method === 'POST' && endpoint === 'repos/acme/app/issues') throw limited; });
  assert.throws(() => createFromPlan(planFile, policy, f.options), (e) => {
    assert.match(e.message, /\bT3\b/);
    assert.ok(e.message.includes(`gh workflow run factory-tickets.yml -f plan=${planFile}`), e.message);
    return true;
  });
});
