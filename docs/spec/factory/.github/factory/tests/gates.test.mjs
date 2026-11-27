import test from 'node:test';
import assert from 'node:assert/strict';
import { loadPolicy, parseDiff, lastLabelActor, makeHumanCheck, parseTitle } from '../lib.mjs';
import { findGuardChanges, dropMovedTests, guardVerdict } from '../guards.mjs';
import { parseBody, ticketVerdict } from '../ticket.mjs';
import { approvingHuman, humanApprovalVerdict } from '../human-approval.mjs';
import { verdictOf } from '../post-review.mjs';
import { plannedChanges, blockers } from '../status.mjs';
import { classify } from '../classify.mjs';
import { declaredGuardFiles } from '../gates.mjs';

const policy = { ...loadPolicy(), humans: { team: 'maintainers', logins: ['alice'] } };
const isHuman = makeHumanCheck(policy);
const d = (file, lines) => parseDiff(`diff --git a/${file} b/${file}\n--- a/${file}\n+++ b/${file}\n@@ -1 +1 @@\n${lines.join('\n')}\n`);

test('weakened guards are found', () => {
  assert.equal(findGuardChanges(d('src/Core/A.cs', ['+#pragma warning disable CA1062'])).length, 1);
  assert.equal(findGuardChanges(d('tests/Core.Tests/A.cs', ['+    [Fact(Skip = "flaky")]'])).length, 1);
  assert.equal(findGuardChanges(d('src/Core/Core.csproj', ['+    <NoWarn>CS1591</NoWarn>'])).length, 1);
  assert.equal(findGuardChanges(d('tests/Core.Tests/errors.approved.txt', ['-core.a', '+core.b'])).length, 1);
  assert.equal(findGuardChanges(d('tests/Core.Tests/errors.approved.txt', ['+core.c'])).length, 0);
  assert.equal(findGuardChanges(d('src/Core/A.cs', ['+public int X { get; }'])).length, 0);
});

// Built from parts so that this file's own added lines do not match the guard patterns.
const lowered = 'dotnet_diagnostic.CA1040.severity = ' + 'none';
const pragma = '#pragma warning ' + 'disable CA1062';

test('findGuardChanges_SeverityLineInAPlan_IsNoFinding', () => {
  assert.equal(findGuardChanges(d('specs/0015-x/plan.md', [`+Set \`${lowered}\` in .editorconfig.`])).length, 0);
});

test('findGuardChanges_SeverityLineInEditorconfig_IsAFinding', () => {
  assert.equal(findGuardChanges(d('.editorconfig', [`+${lowered}`])).length, 1);
});

test('findGuardChanges_PragmaInADocsArticle_IsAFinding', () => {
  assert.equal(findGuardChanges(d('docs/articles/x.md', [`+${pragma}`])).length, 1);
});

test('a moved test is not a removed test; a deleted one is', () => {
  const moved = d('tests/Core.Tests/A.cs', ['-    [Fact]', '+    [Fact]']);
  assert.equal(dropMovedTests(findGuardChanges(moved), moved).length, 0);
  const deleted = d('tests/Core.Tests/A.cs', ['-    [Fact]']);
  assert.equal(dropMovedTests(findGuardChanges(deleted), deleted).length, 1);
});

test('guard-change counts only from a human maintainer', () => {
  const findings = [{ file: 'a', what: 'x', line: 'y' }];
  assert.equal(guardVerdict(findings, false).pass, false);
  assert.equal(guardVerdict(findings, true).pass, true);
  const timeline = [
    { event: 'labeled', label: { name: 'guard-change' }, actor: { login: 'yaf-factory[bot]', type: 'Bot' } },
  ];
  assert.equal(isHuman(lastLabelActor(timeline, 'guard-change')), false);
  timeline.push({ event: 'unlabeled', label: { name: 'guard-change' }, actor: { login: 'alice', type: 'User' } });
  assert.equal(lastLabelActor(timeline, 'guard-change'), null);
  timeline.push({ event: 'labeled', label: { name: 'guard-change' }, actor: { login: 'alice', type: 'User' } });
  assert.equal(isHuman(lastLabelActor(timeline, 'guard-change')), true);
  assert.equal(isHuman({ login: 'mallory', type: 'User' }), false);
});

test('guardVerdict_OnlyDeclaredFindings_Passes', () => {
  const declared = { file: '.editorconfig', what: 'analyzer severity lowered', line: 'x' };
  const other = { file: 'src/Core/A.cs', what: 'pragma warning disable', line: 'y' };
  const v = guardVerdict([declared], false, ['.editorconfig']);
  assert.equal(v.pass, true);
  assert.match(v.title, /declared by the plan/);
  assert.equal(guardVerdict([declared, other], false, ['.editorconfig']).pass, false);
  assert.equal(guardVerdict([declared], false).pass, false);
});

const planWith = (entries) => `# plan\n\n\`\`\`factory:delta\n${JSON.stringify({ editorconfig: entries })}\n\`\`\`\n`;
const config = 'root = true\n\n[*.cs]\nindent_style = space\n';
const setting = 'dotnet_diagnostic.CA1040.severity = warning';
const editorconfigChange = new Map([['.editorconfig', { base: config, head: `${config}\n[src/Core/A.cs]\n${setting}\n` }]]);

test('declaredGuardFiles_DeclarationOnlyInTheHeadPlan_IsNone', () => {
  const basePlan = planWith([]);
  assert.deepEqual(declaredGuardFiles(basePlan, editorconfigChange, policy), []);
  assert.deepEqual(declaredGuardFiles(null, editorconfigChange, policy), []);
  const r = classify({ files: ['.editorconfig', 'specs/0015-x/plan.md'], diff: new Map(), title: 'build: relax CA1040', policy, declaredFiles: [] });
  assert.equal(r.tier, 'high');
  // the same entry on the base plan declares the file
  assert.deepEqual(declaredGuardFiles(planWith([`[src/Core/A.cs] ${setting}`]), editorconfigChange, policy), ['.editorconfig']);
});

test('pull request body', () => {
  const body = 'Adds the contract.\n\nCloses #123\nPlan: specs/0003-typed-ids/plan.md#T1\n<!-- Closes #999 -->';
  assert.deepEqual(parseBody(body), { closes: [123], plan: { file: 'specs/0003-typed-ids/plan.md', key: 'T1' }, trivial: null });
  assert.equal(parseBody('Trivial because: typo in a comment').trivial, 'typo in a comment');
});

test('ticket verdict', () => {
  const parsed = parseBody('Closes #123\nPlan: specs/0003-typed-ids/plan.md#T1');
  const issue = { number: 123, state: 'open', labels: [{ name: 'factory' }], body: '<!-- factory:ticket specs/0003-typed-ids#T1 -->' };
  const base = { kind: 'code', tier: 'medium', changedLines: 100, parsed, issue, planExistsOnBase: true, touchesPlan: false };
  assert.equal(ticketVerdict(base).pass, true);
  assert.equal(ticketVerdict({ ...base, planExistsOnBase: false }).pass, false);
  assert.equal(ticketVerdict({ ...base, issue: { ...issue, state: 'closed' } }).pass, false);
  assert.equal(ticketVerdict({ ...base, issue: { ...issue, body: '<!-- factory:ticket specs/0009-other#T1 -->' } }).pass, false);
  assert.equal(ticketVerdict({ ...base, touchesPlan: true }).pass, false);
  assert.equal(ticketVerdict({ ...base, parsed: parseBody('no ticket') }).pass, false);
  assert.equal(ticketVerdict({ ...base, kind: 'plan' }).pass, true);
  const trivial = parseBody('Trivial because: typo');
  assert.equal(ticketVerdict({ ...base, parsed: trivial, tier: 'low', changedLines: 4 }).pass, true);
  assert.equal(ticketVerdict({ ...base, parsed: trivial, tier: 'high', changedLines: 4 }).pass, false);
  assert.equal(ticketVerdict({ ...base, parsed: trivial, tier: 'low', changedLines: 80 }).pass, false);
});

test('human approval: a maintainer, not the author, on the current commit', () => {
  const r = (login, type, state, commit_id) => ({ user: { login, type }, state, commit_id });
  const ctx = { author: 'yaf-factory[bot]', headSha: 'h2', isHuman };
  assert.equal(approvingHuman([r('alice', 'User', 'APPROVED', 'h2')], ctx), 'alice');
  assert.equal(approvingHuman([r('alice', 'User', 'APPROVED', 'h1')], ctx), null); // stale
  assert.equal(approvingHuman([r('reviewer[bot]', 'Bot', 'APPROVED', 'h2')], ctx), null);
  assert.equal(approvingHuman([r('mallory', 'User', 'APPROVED', 'h2')], ctx), null);
  assert.equal(approvingHuman([r('alice', 'User', 'APPROVED', 'h2'), r('alice', 'User', 'CHANGES_REQUESTED', 'h2')], ctx), null);
  assert.equal(approvingHuman([r('alice', 'User', 'APPROVED', 'h2')], { ...ctx, author: 'alice' }), null);
  assert.equal(humanApprovalVerdict('high', null).pass, false);
  assert.equal(humanApprovalVerdict('medium', null).pass, true);
});

test('review verdict becomes a check only when nothing serious is open', () => {
  const ok = { verdict: 'ready', passes: [{ pass: 'correctness', result: 'pass', notes: '' }], findings: [{ severity: 'Minor' }] };
  assert.equal(verdictOf('factory/review', ok).pass, true);
  assert.equal(verdictOf('factory/review', { ...ok, findings: [{ severity: 'Major' }] }).pass, false);
  assert.equal(verdictOf('factory/review', { ...ok, passes: [{ pass: 'correctness', result: 'fail', notes: '' }] }).pass, false);
  assert.equal(verdictOf('factory/review', null).pass, false);
  assert.equal(verdictOf('factory/doc-review', { verdict: 'pass', checklist: [], findings: [] }).pass, true);
});

test('status: closed is Done, unblocked Backlog becomes Ready', () => {
  assert.deepEqual(blockers('x\nBlocked by: #1, #2\ny'), [1, 2]);
  assert.deepEqual(blockers('Blocked by: nothing'), []);
  const tickets = [
    { number: 1, state: 'closed', body: 'Blocked by: nothing', labels: ['factory'] },
    { number: 2, state: 'open', body: 'Blocked by: #1', labels: ['factory'] },
    { number: 3, state: 'open', body: 'Blocked by: #1, #2', labels: ['factory'] },
    { number: 4, state: 'open', body: '', labels: ['factory', 'epic'] },
  ];
  const changes = plannedChanges(tickets, new Map([[1, 'In review'], [2, 'Backlog'], [3, 'Backlog']]));
  assert.deepEqual(changes, [{ number: 1, status: 'Done' }, { number: 2, status: 'Ready' }]);
});

test('conventional commit title', () => {
  assert.deepEqual(parseTitle('feat(core)!: drop X'), { type: 'feat', scope: 'core', breaking: true, subject: 'drop X' });
  assert.equal(parseTitle('whatever'), null);
});
