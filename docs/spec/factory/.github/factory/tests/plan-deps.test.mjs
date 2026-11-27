import test from 'node:test';
import assert from 'node:assert/strict';
import { ancestors, overlaps } from '../plan-deps.mjs';

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
