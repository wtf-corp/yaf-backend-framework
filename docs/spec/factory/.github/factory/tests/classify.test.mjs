import test from 'node:test';
import assert from 'node:assert/strict';
import { loadPolicy } from '../lib.mjs';
import { classify, prKind, isPatchBumpOnly } from '../classify.mjs';

const policy = loadPolicy();
const base = { diff: new Map(), labels: [], policy };

test('a fix outside the sensitive paths with no delta is low', () => {
  const r = classify({ ...base, files: ['src/Domain/Money.cs', 'tests/Domain.Tests/MoneyTests.cs'], title: 'fix(domain): round half even' });
  assert.equal(r.tier, 'low');
});

test('a feature is medium', () => {
  const r = classify({ ...base, files: ['src/Domain/Period.cs'], title: 'feat(domain): add Period', deltaEmpty: false });
  assert.equal(r.tier, 'medium');
});

test('a feat title that only changes docs and tests is low', () => {
  const r = classify({ ...base, files: ['docs/articles/domain.md', 'tests/Domain.Tests/X.cs'], title: 'feat(docs): describe Period' });
  assert.equal(r.tier, 'low');
});

test('a sensitive path forces high whatever the type', () => {
  for (const f of ['src/Http/Authentication/ApiKeyHandler.cs', 'src/Persistence.EfCore.PostgreSql/Tenancy/Rls.cs', '.github/workflows/ci.yml', 'src/Persistence.EfCore/Migrations/001_Init.cs', 'adr/0030-x.md', '.claude/agents/implementer.md']) {
    const r = classify({ ...base, files: [f], title: 'docs: tidy' });
    assert.equal(r.tier, 'high', f);
    assert.equal(r.floor, 'high', f);
  }
});

test('the floor cannot be lowered by a label', () => {
  const r = classify({ ...base, files: ['src/Infrastructure/Cryptography/KeyRing.cs'], title: 'fix: x', labels: ['tier:low'], raisedTo: 'low' });
  assert.equal(r.tier, 'high');
});

test('a label can raise the tier', () => {
  const r = classify({ ...base, files: ['docs/a.md'], title: 'docs: x', raisedTo: 'high' });
  assert.equal(r.floor, 'low');
  assert.equal(r.tier, 'high');
});

test('oversize, guard changes, undeclared delta, breaking titles and needs-human force high', () => {
  const files = ['src/Domain/A.cs'];
  assert.equal(classify({ ...base, files, title: 'fix: x', sizeOverHard: true }).tier, 'high');
  assert.equal(classify({ ...base, files, title: 'fix: x', guardFindings: 1 }).tier, 'high');
  assert.equal(classify({ ...base, files, title: 'feat: x', deltaEmpty: false, deltaClean: false }).tier, 'high');
  assert.equal(classify({ ...base, files, title: 'feat!: x' }).tier, 'high');
  assert.equal(classify({ ...base, files, title: 'fix: x', body: 'text\n\nBREAKING CHANGE: gone' }).tier, 'high');
  assert.equal(classify({ ...base, files, title: 'fix: x', labels: ['needs-human'] }).tier, 'high');
});

test('a title that is not a Conventional Commit is at least medium', () => {
  assert.equal(classify({ ...base, files: ['src/Domain/A.cs'], title: 'update stuff' }).tier, 'medium');
});

test('a dependency patch update is low, anything else in the package file is high', () => {
  const patch = { removed: ['    <PackageVersion Include="Npgsql" Version="10.0.1" />'], added: ['    <PackageVersion Include="Npgsql" Version="10.0.3" />'] };
  const minor = { removed: ['    <PackageVersion Include="Npgsql" Version="10.0.1" />'], added: ['    <PackageVersion Include="Npgsql" Version="10.1.0" />'] };
  const added = { removed: [], added: ['    <PackageVersion Include="Evil" Version="1.0.0" />'] };
  assert.equal(isPatchBumpOnly(patch), true);
  assert.equal(isPatchBumpOnly(minor), false);
  assert.equal(isPatchBumpOnly(added), false);
  const files = ['Directory.Packages.props', 'src/Core/packages.lock.json'];
  assert.equal(classify({ ...base, files, title: 'chore(deps): bump Npgsql', diff: new Map([['Directory.Packages.props', patch]]) }).tier, 'low');
  assert.equal(classify({ ...base, files, title: 'chore(deps): bump Npgsql', diff: new Map([['Directory.Packages.props', minor]]) }).tier, 'high');
  assert.equal(classify({ ...base, files, title: 'chore(deps): add Evil', diff: new Map([['Directory.Packages.props', added]]) }).tier, 'high');
});

test('pull request kind', () => {
  assert.equal(prKind(['specs/0003-typed-ids/plan.md'], policy), 'plan');
  assert.equal(prKind(['specs/0003-typed-ids/plan.md', 'specs/0003-typed-ids/research.md'], policy), 'plan');
  assert.equal(prKind(['specs/0003-typed-ids/spec.md', 'specs/0003-typed-ids/plan.md'], policy), 'spec');
  assert.equal(prKind(['adr/0030-x.md'], policy), 'spec');
  assert.equal(prKind(['specs/0003-typed-ids/plan.md', 'src/Core/A.cs'], policy), 'code');
});
