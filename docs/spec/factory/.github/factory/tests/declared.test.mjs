import test from 'node:test';
import assert from 'node:assert/strict';
import { editorconfigVerdict, packagesVerdict } from '../declared.mjs';
import { parseDelta } from '../plan.mjs';

// ADR-0034 Decision 6. T7 moves the list to policy.declaredGuards.neverLowered.
const neverLowered = [
  'src/Http/Authentication/**', 'src/Application/Authorization/**', 'src/Application/Context/**', 'src/Persistence.EfCore*/Tenancy/**',
  'src/Infrastructure/Cryptography/**', 'src/Persistence.EfCore/Protection/**', 'src/Persistence.EfCore*/Audit/**', 'src/Privacy*/**',
  'src/Infrastructure/Redaction/**', '.github/**', '.claude/**', '.agents/**', 'AGENTS.md', '**/AGENTS.md', 'CLAUDE.md', 'docs/spec/**',
];

// #388: an excerpt of the base (50eec01) and the sections the pull request added (head 3159a8b).
const editorBase = `root = true

# Code style.

[*.cs]
# File-scoped namespaces.
csharp_style_namespace_declarations = file_scoped:error
dotnet_diagnostic.IDE0161.severity = error

[{src,samples,templates}/**.cs]
dotnet_naming_rule.async_methods_end_in_async.severity = error

[src/Domain/StateMachines/StateMachine\\{TState\\,TTrigger\\}.cs]
# CA1000: 02 §10 prescribes the static StateMachine<TState, TTrigger>.Define. Owner's decision Q6 on #224 (specs/0009-domain/plan.md), 2026-11-25.
dotnet_diagnostic.CA1000.severity = none
# CA1030: 02 §10 names the method Fire. Owner's decision Q6 on #224 (specs/0009-domain/plan.md), 2026-11-25.
dotnet_diagnostic.CA1030.severity = none
`;

const added388 = [
  ['src/Application/IRequest\\{TResult\\}.cs', 'CA1040'],
  ['src/Application/ICommand\\{TResult\\}.cs', 'CA1040'],
  ['src/Application/IQuery\\{TResult\\}.cs', 'CA1040'],
  ['src/Application/IStreamQuery\\{TItem\\}.cs', 'CA1040'],
  ['src/Application/IDomainEventHandler\\{TEvent\\}.cs', 'CA1711'],
  ['src/Application/Pipeline/RequestDelegate\\{TResult\\}.cs', 'CA1711'],
  ['src/Application/Pipeline/IRequestMiddleware\\{TRequest\\,TResult\\}.cs', 'CA1716'],
  ['src/Application/Context/Principal.cs', 'CA1724'],
];

const section = (header, id) => `\n[${header}]\n# ${id}: why. Plan 0013 T1 (specs/0013-thin-slice/plan.md, research §4).\ndotnet_diagnostic.${id}.severity = none\n`;
const entry = (header, id) => `[${header}] dotnet_diagnostic.${id}.severity = none`;
const firstSeven = added388.slice(0, 7);
const headWith = (sections) => editorBase + sections.map(([h, id]) => section(h, id)).join('');
const verdictOf = (sections, entries = sections.map(([h, id]) => entry(h, id)), base = editorBase) => editorconfigVerdict(base, headWith(sections), entries, neverLowered);

test('editorconfigVerdict_SectionsAsDeclared_AreDeclared', () => {
  assert.deepEqual(verdictOf(firstSeven), { declared: true, reasons: [] });
});

test('editorconfigVerdict_NeverLoweredFile_IsNotDeclared', () => {
  const v = verdictOf(added388);
  assert.equal(v.declared, false);
  assert.equal(v.reasons.length, 1);
  assert.match(v.reasons[0], /src\/Application\/Context\/Principal\.cs/);
  assert.match(v.reasons[0], /neverLowered/);
});

test('editorconfigVerdict_NeverLoweredFileInOtherCase_IsNotDeclared', () => {
  const v = verdictOf([['src/application/context/Principal.cs', 'CA1724']]);
  assert.equal(v.declared, false);
  assert.match(v.reasons[0], /neverLowered/);
});

test('editorconfigVerdict_NeverLoweredFileWithLeadingSlash_IsNotDeclared', () => {
  assert.equal(verdictOf([['/src/Application/Context/Principal.cs', 'CA1724']]).declared, false);
  assert.equal(verdictOf([['/AGENTS.md', 'CA1724']]).declared, false);
  assert.equal(verdictOf([['src/Domain/AGENTS.md', 'CA1724']]).declared, false);
});

test('editorconfigVerdict_LeadingSlash_IsDeclared', () => {
  assert.equal(verdictOf([['/src/Application/IRequest\\{TResult\\}.cs', 'CA1040']]).declared, true);
});

test('editorconfigVerdict_SettingNotDeclared_IsNotDeclared', () => {
  const v = verdictOf(firstSeven, firstSeven.slice(1).map(([h, id]) => entry(h, id)));
  assert.equal(v.declared, false);
  assert.match(v.reasons[0], /IRequest.*not declared/);
});

test('editorconfigVerdict_OtherSeverity_IsNotDeclared', () => {
  const v = verdictOf(firstSeven, firstSeven.map(([h, id]) => entry(h, id).replace('none', 'suggestion')));
  assert.equal(v.declared, false);
});

test('editorconfigVerdict_ChangedBaseSection_IsNotDeclared', () => {
  const head = headWith(firstSeven).replace('dotnet_diagnostic.IDE0161.severity = error', 'dotnet_diagnostic.IDE0161.severity = none');
  const v = editorconfigVerdict(editorBase, head, firstSeven.map(([h, id]) => entry(h, id)), neverLowered);
  assert.equal(v.declared, false);
  assert.match(v.reasons.join('\n'), /\[\*\.cs\]/);
});

test('editorconfigVerdict_SettingAddedToBaseSection_IsNotDeclared', () => {
  const head = editorBase.replace('dotnet_diagnostic.CA1030.severity = none', 'dotnet_diagnostic.CA1030.severity = none\ndotnet_diagnostic.CA1716.severity = none');
  const header = 'src/Domain/StateMachines/StateMachine\\{TState\\,TTrigger\\}.cs';
  assert.equal(editorconfigVerdict(editorBase, head, [entry(header, 'CA1716')], neverLowered).declared, false);
});

test('editorconfigVerdict_CommentsOnlyChanged_IsDeclared', () => {
  const head = editorBase.replace('# File-scoped namespaces.', '# File-scoped namespaces, always.\n; another comment\n');
  assert.deepEqual(editorconfigVerdict(editorBase, head, [], neverLowered), { declared: true, reasons: [] });
});

test('editorconfigVerdict_PreambleChanged_IsNotDeclared', () => {
  const head = headWith(firstSeven).replace('root = true', 'root = true\nindent_style = tab');
  const v = editorconfigVerdict(editorBase, head, firstSeven.map(([h, id]) => entry(h, id)), neverLowered);
  assert.equal(v.declared, false);
  assert.match(v.reasons[0], /before the first section/);
});

test('editorconfigVerdict_GlobSection_IsNotDeclared', () => {
  for (const header of ['src/Application/*.cs', 'src/Application/I?.cs', 'src/Application/[IR]*.cs', 'src/{Application,Domain}/X.cs', 'src/**/X.cs']) {
    const v = verdictOf([[header, 'CA1040']]);
    assert.equal(v.declared, false, header);
    assert.match(v.reasons[0], /does not name one file/, header);
  }
});

test('editorconfigVerdict_SectionWithoutSlash_IsNotDeclared', () => {
  const v = verdictOf([['Principal.cs', 'CA1724']]);
  assert.equal(v.declared, false);
  assert.match(v.reasons[0], /does not name one file/);
});

test('editorconfigVerdict_DotDotSegment_IsNotDeclared', () => {
  assert.equal(verdictOf([['src/Domain/../Application/Context/Principal.cs', 'CA1724']]).declared, false);
  assert.equal(verdictOf([['src/Domain/../Domain/X.cs', 'CA1724']]).declared, false);
});

test('editorconfigVerdict_BackslashSeparator_IsNotDeclared', () => {
  assert.equal(verdictOf([['src\\Application\\X.cs', 'CA1724']]).declared, false);
  assert.equal(verdictOf([['src/Application\\\\X.cs', 'CA1724']]).declared, false);
});

// #389: the base (50eec01) without its comments, and the seven pins the pull request added with their comments (head b3c1582).
const packagesBase = `<Project>

  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>

  <ItemGroup>
    <!-- Test runner (ADR-0018). Latest stable on nuget.org, checked 2026-11-20. -->
    <PackageVersion Include="xunit.v3" Version="4.0.3" />
    <PackageVersion Include="TngTech.ArchUnitNET.xUnitV3" Version="0.13.5" />
    <!-- Benchmarks in tests/Benchmarks only, never in a package (ADR-0030). Latest stable on nuget.org, checked 2026-11-24. -->
    <PackageVersion Include="BenchmarkDotNet" Version="0.15.8" />
  </ItemGroup>

</Project>
`;

const pins389 = [
  'Microsoft.EntityFrameworkCore.Relational@11.0.0', 'Npgsql.EntityFrameworkCore.PostgreSQL@11.0.0', 'Microsoft.EntityFrameworkCore.Design@11.0.0',
  'Testcontainers.PostgreSql@4.17.0', 'Microsoft.AspNetCore.Mvc.Testing@11.0.0', 'xunit.v3.extensibility.core@4.0.3', 'TngTech.ArchUnitNET@0.13.5',
];

const packagesHead = (pins, attributes = '') => packagesBase.replace('  </ItemGroup>', pins.map((p) => {
  const [id, version] = p.split('@');
  return `    <!-- Why ${id} (ADR-0003). Licence (nuspec). Latest stable on nuget.org,\n         checked 2026-11-26 (specs/0013-thin-slice/research.md §2). -->\n    <PackageVersion Include="${id}" Version="${version}"${attributes} />\n`;
}).join('') + '  </ItemGroup>');

test('packagesVerdict_PinsAsDeclared_AreDeclared', () => {
  assert.deepEqual(packagesVerdict(packagesBase, packagesHead(pins389), pins389), { declared: true, reasons: [] });
});

test('packagesVerdict_OtherVersion_IsNotDeclared', () => {
  const v = packagesVerdict(packagesBase, packagesHead(pins389), pins389.map((p) => p.replace('Testcontainers.PostgreSql@4.17.0', 'Testcontainers.PostgreSql@4.16.0')));
  assert.equal(v.declared, false);
  assert.match(v.reasons[0], /Testcontainers\.PostgreSql@4\.17\.0/);
});

test('packagesVerdict_VersionlessEntry_IsNotDeclared', () => {
  assert.equal(packagesVerdict(packagesBase, packagesHead(pins389), pins389.map((p) => p.split('@')[0])).declared, false);
});

test('packagesVerdict_PropertyAdded_IsNotDeclared', () => {
  const head = packagesHead(pins389).replace('  </PropertyGroup>', '    <CentralPackageVersionOverrideEnabled>true</CentralPackageVersionOverrideEnabled>\n  </PropertyGroup>');
  const v = packagesVerdict(packagesBase, head, pins389);
  assert.equal(v.declared, false);
  assert.match(v.reasons[0], /rest of the file/);
});

test('packagesVerdict_ConditionAttribute_IsNotDeclared', () => {
  const v = packagesVerdict(packagesBase, packagesHead(pins389.slice(0, 1), ` Condition="'$(TargetFramework)' == 'net11.0'"`), pins389);
  assert.equal(v.declared, false);
  assert.match(v.reasons[0], /Include and Version/);
});

test('packagesVerdict_Child_IsNotDeclared', () => {
  const head = packagesBase.replace('  </ItemGroup>', '    <PackageVersion Include="TngTech.ArchUnitNET" Version="0.13.5"><Foo /></PackageVersion>\n  </ItemGroup>');
  assert.equal(packagesVerdict(packagesBase, head, pins389).declared, false);
});

test('packagesVerdict_BasePinChanged_IsNotDeclared', () => {
  const head = packagesBase.replace('Version="0.15.8"', 'Version="0.15.9"');
  const v = packagesVerdict(packagesBase, head, ['BenchmarkDotNet@0.15.9']);
  assert.equal(v.declared, false);
  assert.match(v.reasons[0], /BenchmarkDotNet/);
});

test('packagesVerdict_BasePinRemoved_IsNotDeclared', () => {
  const v = packagesVerdict(packagesBase, packagesBase.replace('    <PackageVersion Include="BenchmarkDotNet" Version="0.15.8" />\n', ''), []);
  assert.equal(v.declared, false);
});

test('parseDelta_EditorconfigCategory_IsRead', () => {
  const plan = '```factory:delta\n{ "dependencies": ["TngTech.ArchUnitNET@0.13.5"], "editorconfig": ["[src/Application/IRequest\\\\{TResult\\\\}.cs] dotnet_diagnostic.CA1040.severity = none"] }\n```\n';
  const delta = parseDelta(plan);
  assert.deepEqual(delta.editorconfig, ['[src/Application/IRequest\\{TResult\\}.cs] dotnet_diagnostic.CA1040.severity = none']);
  assert.deepEqual(delta.dependencies, ['TngTech.ArchUnitNET@0.13.5']);
  assert.deepEqual(parseDelta('```factory:delta\n{ "schema": [] }\n```\n').editorconfig, []);
});
