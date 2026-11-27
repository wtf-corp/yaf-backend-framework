import test from 'node:test';
import assert from 'node:assert/strict';
import { parseExitChecks, exitCheck, exitComment } from '../exit-check.mjs';
import { planFlags } from '../plan.mjs';

const fence = '```';
const block = (checks) => `${fence}factory:exit
${JSON.stringify({
  criteria: [{ text: 'Sessions work', checks: checks.map((c) => c.id) }],
  checks,
})}
${fence}`;
const planText = (checks = [{ id: 'E1', title: 'session tests', run: 'dotnet test x', expect: 0 }]) => `# X: plan

${fence}factory:delta
{}
${fence}

${block(checks)}

## Tickets

| Key | Title | Tier | Touches | Failing test first | Verify |
|---|---|---|---|---|---|
| T1 | chore: one | low | \`a.mjs\` | a test | a command |
`;

const planFile = 'specs/0001-x/plan.md';
const SHA = 'a'.repeat(40);
const ok = (body) => `HTTP/2.0 200 OK\ncontent-type: application/json\n\n${JSON.stringify(body)}`;

/** Fake git, exec and gh. Records every command run and every gh call. */
function fakes({ text = planText(), tracked = [planFile], dirty = '', head = SHA, issues = [], ci = [{ status: 'completed', conclusion: 'success', url: 'https://ci/1' }], exits = {}, env = {} } = {}) {
  const ran = [], gh = [], printed = [];
  const deps = {
    repo: 'acme/app',
    env,
    now: (() => { let t = 0; return () => (t += 1500); })(),
    log: (s) => printed.push(s),
    git: (...args) => {
      const a = args.join(' ');
      if (a === 'rev-parse --show-toplevel') return '/repo\n';
      if (a.startsWith('ls-tree')) return tracked.filter((t) => t === args.at(-1)).join('\n');
      if (a.startsWith('show origin/main:')) return text;
      if (a === 'status --porcelain') return dirty;
      if (a === 'rev-parse HEAD') return head + '\n';
      if (a === 'rev-parse origin/main') return SHA + '\n';
      throw new Error(`unexpected git ${a}`);
    },
    exec: (command, options) => {
      ran.push({ command, options });
      return exits[command] ?? { status: 0, output: 'ok\n' };
    },
    run: (cmd, args, options = {}) => {
      gh.push({ args, input: options.input });
      if (args[0] === 'run') return JSON.stringify(ci);
      if (args.includes('POST')) return 'HTTP/2.0 201 Created\n\n{"id":1}';
      return ok(issues);
    },
  };
  return { deps, ran, gh, printed, writes: () => gh.filter((c) => c.args.includes('POST')) };
}

const issue = (number, marker, state = 'closed', title = `issue ${number}`) => ({ number, state, title, body: marker });
const epic = issue(9, '<!-- factory:epic specs/0001-x -->', 'open');

test('planFlags_ExitBlock_IsFlagged', () => {
  const policy = { delta: { flaggingCategories: ['publicApi', 'schema', 'dependencies'] } };
  assert.deepEqual(planFlags(planText().replace('{}', '{ "publicApi": [] }'), policy), { flagged: true, reasons: ["declares exit checks that run on the owner's machine"] });
  assert.deepEqual(planFlags(planText().replace(block([]).split('\n')[0], '```json'), policy), { flagged: false, reasons: [] });
});

test('parseExitChecks_Block_GivesCriteriaAndChecks', () => {
  const parsed = parseExitChecks(planText([{ id: 'E1', title: 't', run: 'a', expect: 0 }, { id: 'E2', run: 'b' }]));
  assert.deepEqual(parsed.criteria, [{ text: 'Sessions work', checks: ['E1', 'E2'] }]);
  assert.deepEqual(parsed.checks, [{ id: 'E1', title: 't', run: 'a', expect: 0 }, { id: 'E2', title: 'E2', run: 'b', expect: 0 }]);
  assert.equal(parseExitChecks('# no block'), null);
});

test('parseExitChecks_CriterionNamesAnUnknownCheck_Throws', () => {
  const text = planText().replace('"checks":["E1"]', '"checks":["E1","E7"]');
  assert.throws(() => parseExitChecks(text), /E7/);
});

test('exitCheck_DirtyCheckoutOrNotOriginMain_Refuses', () => {
  for (const options of [{ dirty: ' M src/a.cs\n' }, { head: 'b'.repeat(40) }]) {
    const f = fakes(options);
    assert.throws(() => exitCheck([planFile, '--dry-run'], f.deps), /clean|origin\/main/);
    assert.equal(f.ran.length, 0);
  }
});

test('exitCheck_FailingCommand_RecordsExitTimeAndTail', () => {
  const output = Array.from({ length: 50 }, (_, i) => `line ${i + 1}`).join('\n') + '\n';
  const f = fakes({ issues: [epic], exits: { 'dotnet test x': { status: 3, output } } });
  assert.equal(exitCheck([planFile, '--dry-run'], f.deps), 1);
  const comment = f.printed.join('\n');
  assert.match(comment, /\| E1 session tests \| `dotnet test x` \| 3 \(expected 0\) \| 1\.5 s \|/);
  assert.match(comment, /line 11\nline 12/);
  assert.doesNotMatch(comment, /line 10\n/);
  assert.match(comment, /line 50/);
});

test('exitCheck_ChecksRunWithoutTokens', () => {
  const env = { PATH: '/bin', GH_TOKEN: 's', GITHUB_TOKEN: 's', GIT_CONFIG_COUNT: '1', GIT_CONFIG_KEY_0: 'credential.helper', git_config_value_1: 'x' };
  const f = fakes({ issues: [epic], env });
  exitCheck([planFile, '--dry-run'], f.deps);
  assert.equal(f.ran.length, 1);
  assert.deepEqual(f.ran[0].options.env, { PATH: '/bin' });
  assert.equal(f.ran[0].options.cwd, '/repo');
});

test('exitCheck_PlanNotTrackedOnMain_Refuses', () => {
  for (const p of ['/abs/specs/0001-x/plan.md', 'C:/specs/0001-x/plan.md', 'specs/../specs/0001-x/plan.md', 'specs/0001-x/../0001-x/plan.md', 'docs/plan.md', 'specs/0001-x/spec.md', 'specs/0001-x/sub/plan.md', 'specs\\0001-x\\plan.md', 'specs/0002-y/plan.md']) {
    const f = fakes();
    assert.throws(() => exitCheck([p, '--dry-run'], f.deps), /specs\/<folder>\/plan\.md|not tracked/, p);
    assert.equal(f.ran.length, 0, p);
  }
});

test('exitCheck_PlanReadFromOriginMain_NotFromTheFile', () => {
  const f = fakes({ issues: [epic], text: planText([{ id: 'E1', run: 'from main' }]) });
  f.deps.read = () => planText([{ id: 'E1', run: 'from the working tree' }]);
  exitCheck([planFile, '--dry-run'], f.deps);
  assert.deepEqual(f.ran.map((r) => r.command), ['from main']);
});

test('exitComment_CriteriaChecksOpenTicketsAndCi_AreRendered', () => {
  const comment = exitComment({
    planFile, sha: SHA, startedAt: '2026-11-27T10:00:00.000Z',
    exit: parseExitChecks(planText()),
    results: [{ id: 'E1', title: 'session tests', run: 'dotnet test x', expect: 0, status: 0, ms: 61000, tail: ['done'] }],
    openTickets: [{ number: 12, title: 'chore: one' }],
    ci: { status: 'completed', conclusion: 'failure', url: 'https://ci/1' },
  });
  assert.ok(comment.startsWith(`<!-- factory:exit-check ${SHA} -->`), comment);
  assert.match(comment, /\| Sessions work \| E1 \| passed \|/);
  assert.match(comment, /\| E1 session tests \| `dotnet test x` \| 0 \(expected 0\) \| 61\.0 s \|/);
  assert.match(comment, /- #12 chore: one/);
  assert.match(comment, /\[ci\.yml run\]\(https:\/\/ci\/1\): completed, failure/);
  assert.match(comment, /<details><summary>E1 session tests: exit 0<\/summary>/);
  assert.match(comment, /Verdict: failed/);
});

test('exitCheck_OpenTicket_ExitsOne', () => {
  const closed = fakes({ issues: [epic, issue(10, '<!-- factory:ticket specs/0001-x#T1 -->')] });
  assert.equal(exitCheck([planFile, '--dry-run'], closed.deps), 0);
  const open = fakes({ issues: [epic, issue(10, '<!-- factory:ticket specs/0001-x#T1 -->', 'open', 'chore: one'), issue(11, '<!-- factory:ticket specs/0002-y#T1 -->', 'open')] });
  assert.equal(exitCheck([planFile, '--dry-run'], open.deps), 1);
  assert.match(open.printed.join('\n'), /- #10 chore: one/);
  assert.doesNotMatch(open.printed.join('\n'), /#11/);
});

test('exitCheck_List_RunsNothing', () => {
  const f = fakes({ dirty: ' M a\n' });
  assert.equal(exitCheck([planFile, '--list'], f.deps), 0);
  assert.equal(f.ran.length, 0);
  assert.equal(f.gh.length, 0);
  assert.match(f.printed.join('\n'), /Sessions work[\s\S]*E1 session tests: dotnet test x \(expect 0\)/);
});

test('exitCheck_DryRun_RunsTheChecksAndPostsNothing', () => {
  const f = fakes({ issues: [epic] });
  assert.equal(exitCheck([planFile, '--dry-run'], f.deps), 0);
  assert.equal(f.ran.length, 1);
  assert.equal(f.writes().length, 0);
  assert.match(f.printed.join('\n'), new RegExp(`<!-- factory:exit-check ${SHA} -->`));

  const post = fakes({ issues: [epic] });
  assert.equal(exitCheck([planFile, '--post'], post.deps), 0);
  assert.equal(post.writes().length, 1);
  assert.ok(post.writes()[0].args.includes('repos/acme/app/issues/9/comments'));
  assert.match(JSON.parse(post.writes()[0].input).body, new RegExp(`^<!-- factory:exit-check ${SHA} -->`));
});

test('exitCheck_TwoModes_Refuses', () => {
  for (const argv of [[planFile, '--list', '--post'], [planFile, '--dry-run', '--post'], [planFile], [planFile, '--force']]) {
    const f = fakes();
    assert.throws(() => exitCheck(argv, f.deps), /one of --list, --dry-run, --post/);
    assert.equal(f.ran.length, 0);
  }
});
