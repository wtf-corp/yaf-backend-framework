import test from 'node:test';
import assert from 'node:assert/strict';
import { loadPolicy } from '../lib.mjs';
import { tierFloor } from '../plan-tier.mjs';

const policy = loadPolicy();
const none = () => false;
/** A parsed ticket with only what the check reads. */
const t = (key, title, touches, tier) => ({ key, title, touches, tier });
const check = (tickets, { delta = null, exists = none } = {}) => tierFloor(tickets, { policy, delta, exists });

test('tierFloor_ErrorsApprovedDeclaredLow_IsAProblem', () => {
  // Round 1 of plan 0013, T64 (the review's F6): it adds the catalog's errors.approved.txt and was declared low.
  const dir = 'samples/conference/tests/Catalog/Application.Tests/';
  const found = check([t('T64', 'test(sample): approve the catalog\'s error catalogue and pipeline',
    [`${dir}errors.approved.txt`, `${dir}pipeline.approved.txt`, `${dir}ApprovalTests.cs`], 'low')]);
  assert.equal(found.problems.length, 1);
  assert.match(found.problems[0], /T64: declared tier low is below the floor medium/);
  assert.match(found.problems[0], /errors\.approved\.txt/);
});

test('tierFloor_MigrationsFolder_IsHigh', () => {
  const found = check([t('T1', 'feat(sample): add the schema', ['samples/conference/src/Services/Catalog/Infrastructure/Migrations/'], 'medium')]);
  assert.equal(found.problems.length, 1);
  assert.match(found.problems[0], /below the floor high: sensitive path/);
});

test('tierFloor_NewPublicApiFiles_StayLow', () => {
  // T7 of plan 0013: a new package's PublicAPI files hold `#nullable enable` only, which the delta does not count.
  const tickets = [t('T7', 'build: add the application package', ['src/Application/Application.csproj', 'src/Application/PublicAPI.Shipped.txt',
    'src/Application/PublicAPI.Unshipped.txt', 'tests/Application.Tests/', 'tests/Fixtures/ErrorsAlpha/', 'YAF.slnx'], 'low')];
  assert.deepEqual(check(tickets), { problems: [], notes: [] });
  // The same files on the base are a change of public API.
  assert.match(check(tickets, { exists: (p) => p.includes('PublicAPI') }).problems[0], /below the floor medium/);
  assert.match(check([t('T8', 'build: add it', ['src/Application/PublicAPI.*.txt'], 'low')], { exists: () => true }).problems[0], /below the floor medium/);
});

test('tierFloor_DeclaredAboveTheFloor_IsANote', () => {
  // Round 8 of plan 0013, T77 (the review's F4): a sample use case, every touch in the low paths.
  const found = check([t('T77', 'feat(sample): add the get-session use case', ['samples/conference/src/Services/Catalog/Application/Sessions/GetSession/',
    'samples/conference/tests/Catalog/Application.Tests/GetSession/'], 'high')]);
  assert.deepEqual(found.problems, []);
  assert.equal(found.notes.length, 1);
  assert.match(found.notes[0], /T77: declared tier high is above the floor low/);
});

test('tierFloor_EditorconfigWithDeclaredEntries_IsMedium', () => {
  const ticket = t('T2', 'build: relax CA1040 on the marker interfaces', ['.editorconfig'], 'medium');
  const entry = (file) => `[${file}] dotnet_diagnostic.CA1040.severity = warning`;
  const delta = (entries) => ({ editorconfig: entries, dependencies: [] });
  assert.deepEqual(check([ticket], { delta: delta([entry('src/Application/IRequest\\{TResult\\}.cs')]) }), { problems: [], notes: [] });
  // Without entries, or with a section on a neverLowered file, it stays high.
  assert.match(check([ticket]).problems[0], /floor high/);
  assert.match(check([ticket], { delta: delta([entry('src/Application/IRequest.cs'), entry('src/Application/Context/Principal.cs')]) }).problems[0], /floor high/);
  // Package pins declared with their version give medium too; without a version they lower nothing.
  const pins = t('T1', 'build: pin the packages', ['Directory.Packages.props'], 'medium');
  assert.deepEqual(check([pins], { delta: { editorconfig: [], dependencies: ['Npgsql@11.0.0'] } }).problems, []);
  assert.match(check([pins], { delta: { editorconfig: [], dependencies: ['Npgsql'] } }).problems[0], /floor high/);
});

test('tierFloor_FolderWithASensitiveSubfolder_IsANote', () => {
  const found = check([t('T3', 'feat: add the application layer', ['src/Application/'], 'medium')]);
  assert.deepEqual(found.problems, []);
  assert.equal(found.notes.length, 1);
  assert.match(found.notes[0], /T3: `src\/Application\/` contains sensitive paths: src\/Application\/Authorization\/\*\*/);
});

test('tierFloor_FeatTitleOutsideLowPaths_IsMedium', () => {
  assert.match(check([t('T4', 'feat(http): add the endpoint', ['src/Http/Endpoint.cs', 'tests/Http.Tests/EndpointTests.cs'], 'low')]).problems[0],
    /T4: declared tier low is below the floor medium: type feat/);
  assert.deepEqual(check([t('T5', 'feat(sample): add the page', ['samples/conference/README.md', 'docs/articles/page.md'], 'low')]), { problems: [], notes: [] });
  assert.match(check([t('T6', 'feat!: rename the option', ['src/Http/Options.cs'], 'medium')]).problems[0], /floor high: breaking change/);
});
