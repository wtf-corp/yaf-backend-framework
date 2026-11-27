import test from 'node:test';
import assert from 'node:assert/strict';
import { ancestors, overlaps, uses } from '../plan-deps.mjs';

/** A parsed ticket with only what the checks read. */
const t = (key, touches, dependsOn = []) => ({ key, touches, dependsOn });

test('overlaps_SameFileNoDependency_IsAProblem', () => {
  // Round 1 of plan 0013: T23 and T27 both touch YAF.slnx, and neither depends on the other.
  const tickets = [
    t('T22', ['src/Application/Application.csproj']),
    t('T23', ['src/Application/Errors/', 'YAF.slnx'], ['T22']),
    t('T26', ['src/Analyzers/Analyzers.csproj']),
    t('T27', ['src/Persistence.EfCore/Persistence.EfCore.csproj', 'YAF.slnx'], ['T26']),
  ];
  const found = overlaps(tickets);
  assert.equal(found.length, 1);
  assert.match(found[0], /T23/);
  assert.match(found[0], /T27/);
  assert.match(found[0], /YAF\.slnx/);
});

test('overlaps_FolderAndFileInside_IsAProblem', () => {
  assert.equal(overlaps([t('T1', ['src/Application/Context/']), t('T2', ['src/Application/Context/Scope.cs'])]).length, 1);
  assert.equal(overlaps([t('T1', ['src/Application/Context']), t('T2', ['src/Application/Context/Scope.cs'])]).length, 1);
  assert.deepEqual(overlaps([t('T1', ['src/Application/Context/']), t('T2', ['src/Application/ContextFactory.cs'])]), []);
});

test('overlaps_GlobAndMatchedFile_IsAProblem', () => {
  assert.equal(overlaps([t('T1', ['src/Http/PublicAPI.*.txt']), t('T2', ['src/Http/PublicAPI.Unshipped.txt'])]).length, 1);
  assert.deepEqual(overlaps([t('T1', ['src/Http/PublicAPI.*.txt']), t('T2', ['src/Http/Http.csproj'])]), []);
});

test('overlaps_OneMigrationsFolder_IsAProblem', () => {
  const tickets = [
    t('T1', ['src/Catalog/Migrations/20261101_Initial.cs']),
    t('T2', ['src/Catalog/Migrations/CatalogDbContextModelSnapshot.cs']),
    t('T3', ['src/Registrations/Migrations/20261101_Initial.cs']),
  ];
  const found = overlaps(tickets);
  assert.equal(found.length, 1);
  assert.match(found[0], /T1 and T2/);
});

test('overlaps_TransitiveDependency_IsNone', () => {
  const tickets = [
    t('T1', ['YAF.slnx']),
    t('T2', ['src/Other/Other.csproj'], ['T1']),
    t('T3', ['YAF.slnx'], ['T2']),
  ];
  assert.deepEqual(overlaps(tickets), []);
});

test('overlaps_PathWithNote_ComparesThePathOnly', () => {
  assert.equal(overlaps([t('T1', ['`.editorconfig` (three sections)']), t('T2', ['.editorconfig'])]).length, 1);
});

test('ancestors_Chain_IsTransitive', () => {
  const of = ancestors([t('T1', []), t('T2', [], ['T1']), t('T3', [], ['T2']), t('T4', [])]);
  assert.deepEqual([...of.get('T3')].sort(), ['T1', 'T2']);
  assert.deepEqual([...of.get('T1')], []);
  assert.deepEqual([...of.get('T4')], []);
});

/** A parsed ticket with the cells `uses` reads; the type is chore unless given. */
const u = (key, { touches = [], dependsOn = [], title = '', failingTest = '', verify = '', requirements = '', type = 'chore' } = {}) =>
  ({ key, touches, dependsOn, title, failingTest, verify, requirements, type });
const none = () => false;

test('uses_TypeCreatedByALaterTicket_IsAProblem', () => {
  const tickets = [
    u('T1', { touches: ['src/A/A.csproj'] }),
    u('T2', { failingTest: 'In `FooTests`: `Foo.Create` returns a value' }),
    u('T3', { failingTest: '`Make_Foo_Works` — fails: `Foo` does not exist' }),
  ];
  const { problems, notes } = uses(tickets, { exists: none });
  assert.equal(problems.length, 1);
  assert.match(problems[0], /^T2: .*`Foo`.*T3/);
  assert.deepEqual(notes, []);
});

test('uses_NewFileOfANonDependency_IsAProblem', () => {
  const tickets = [
    u('T1', { touches: ['src/X/Bar.cs', '`src/X/Baz{T}.cs` (new)'] }),
    u('T2', { failingTest: '`Bar_Works` uses `Bar`', verify: '`grep -c Baz src/X/X.csproj`' }),
  ];
  const { problems } = uses(tickets, { exists: none });
  assert.equal(problems.length, 2);
  assert.match(problems[0], /^T2: .*`Bar`.*T1/);
  assert.match(problems[1], /^T2: .*`Baz`.*T1/);
});

test('uses_FileOnTheBase_IsNone', () => {
  const tickets = [u('T1', { touches: ['src/X/Bar.cs'] }), u('T2', { failingTest: '`Bar` returns 1' })];
  assert.deepEqual(uses(tickets, { exists: (p) => p === 'src/X/Bar.cs' }), { problems: [], notes: [] });
});

test('uses_Ancestor_IsNone', () => {
  const tickets = [
    u('T1', { touches: ['src/X/Bar.cs'] }),
    u('T2', { touches: ['src/X/Other.cs'], dependsOn: ['T1'] }),
    u('T3', { dependsOn: ['T2'], failingTest: '`Bar` returns 1', title: 'feat: use `Other`' }),
  ];
  assert.deepEqual(uses(tickets, { exists: none }), { problems: [], notes: [] });
});

test('uses_DocsTicket_IsANote', () => {
  // Plan 0013: the docs ticket T93 names `SessionData` of T80.
  const tickets = [
    u('T80', { failingTest: 'fails: `SessionData` does not exist' }),
    u('T93', { type: 'docs', verify: '`grep -q SessionData docs/articles/persistence.md`' }),
  ];
  const { problems, notes } = uses(tickets, { exists: none });
  assert.deepEqual(problems, []);
  assert.equal(notes.length, 1);
  assert.match(notes[0], /^T93: .*`SessionData`.*T80/);
});

test('uses_NewAdr_IsMatchedAsAdrNumber', () => {
  const tickets = [
    u('T1', { touches: ['docs/spec/adr/0034-guard-changes.md', 'docs/spec/adr/README.md'] }),
    u('T2', { verify: '`grep -q "ADR-0034" README.md`' }),
    u('T3', { verify: '`grep -q "ADR-0029" README.md`', failingTest: '`README` names `0034-guard-changes`' }),
  ];
  const { problems } = uses(tickets, { exists: (p) => p === 'docs/spec/adr/README.md' });
  assert.equal(problems.length, 1);
  assert.match(problems[0], /^T2: .*`ADR-0034`.*T1/);
});

test('uses_RequirementsCell_IsNotRead', () => {
  const tickets = [
    u('T1', { requirements: '`03` §1 (`Foo` signature)' }),
    u('T2', { failingTest: '`Foo` does not exist' }),
  ];
  assert.deepEqual(uses(tickets, { exists: none }), { problems: [], notes: [] });
});
