import test from 'node:test';
import assert from 'node:assert/strict';
import { deltaForms, PUBLIC_API_FORMS } from '../plan-forms.mjs';

/** A parsed delta (plan.parseDelta) with every category empty except the given ones. */
const delta = (entries) => ({ publicApi: [], schema: [], errorCodes: [], contracts: [], configuration: [], telemetry: [], dependencies: [], editorconfig: [], ...entries });

const allForms = (ns) => PUBLIC_API_FORMS.map((f) => `${f}${ns}`);

test('deltaForms_NamespacePrefixWithoutStaticReadonly_NamesTheMissingForms', () => {
  // Round 3 of plan 0013 (8773d1f8): nine forms of YAF.Application.*, the review's F1 found `static readonly` missing.
  const publicApi = ['YAF.Application.*', 'static YAF.Application.*', 'abstract YAF.Application.*', 'virtual YAF.Application.*', 'override YAF.Application.*',
    '~YAF.Application.*', '~static YAF.Application.*', '~override YAF.Application.*', '~virtual YAF.Application.*', 'YAF.Core.Results.ResultFactory*'];
  const found = deltaForms(delta({ publicApi }));
  assert.equal(found.length, 1);
  for (const missing of ['"static readonly YAF.Application.*"', '"static abstract YAF.Application.*"', '"const YAF.Application.*"', '"override sealed YAF.Application.*"',
    '"~static readonly YAF.Application.*"', '"~abstract YAF.Application.*"', '"~override sealed YAF.Application.*"']) assert.ok(found[0].includes(missing), `${missing} in ${found[0]}`);
  assert.match(found[0], /7 of the 16 forms/);
  assert.doesNotMatch(found[0], /"~virtual/);
});

test('deltaForms_AllSixteenForms_IsNone', () => {
  assert.equal(PUBLIC_API_FORMS.length, 16);
  assert.deepEqual(deltaForms(delta({ publicApi: [...allForms('YAF.Http.*'), ...allForms('YAF.Testing.*'), 'YAF.Domain.IAggregateStateSync*', 'static YAF.Core.Results.ResultFactory*'] })), []);
  assert.deepEqual(deltaForms(null), []);
});

test('deltaForms_ErrorCodeInUpperCase_IsAProblem', () => {
  const found = deltaForms(delta({ errorCodes: ['core.money.currency_mismatch', 'core.uninitialised', 'catalog.*', 'Catalog.Session.Full', 'core'] }));
  assert.equal(found.length, 2);
  assert.match(found[0], /errorCodes.*"Catalog\.Session\.Full"/);
  assert.match(found[1], /errorCodes.*"core"/);
});

test('deltaForms_ConfigurationKeyWithADot_IsAProblem', () => {
  const found = deltaForms(delta({ configuration: ['Http:ProblemDetails:TypeBaseUri', 'ConnectionStrings:catalog', 'Outbox:*', 'Http.ValidationStatusCode'] }));
  assert.equal(found.length, 1);
  assert.match(found[0], /configuration.*"Http\.ValidationStatusCode"/);
});

test('deltaForms_SchemaNameNotAnIdentifier_IsAProblem', () => {
  const found = deltaForms(delta({ schema: ['InitialCatalog', 'AddOutbox*', 'add outbox'] }));
  assert.equal(found.length, 1);
  assert.match(found[0], /schema.*"add outbox"/);
});

test('deltaForms_DependencyWithVersion_IsAccepted', () => {
  assert.deepEqual(deltaForms(delta({ dependencies: ['Npgsql.EntityFrameworkCore.PostgreSQL', 'dotnet-ef', 'xunit.v3.extensibility.core@3.2.1', 'Foo@1.0.0-preview.2'] })), []);
  const found = deltaForms(delta({ dependencies: ['Foo@', 'Foo 1.0.0', 'Foo@latest'] }));
  assert.equal(found.length, 3);
});

test('deltaForms_EditorconfigEntryWithoutSection_IsAProblem', () => {
  const ok = '[src/Application/IRequest\\{TResult\\}.cs] dotnet_diagnostic.CA1040.severity = warning';
  const found = deltaForms(delta({ editorconfig: [ok, 'dotnet_diagnostic.CA1040.severity = warning', '[src/A.cs] dotnet_diagnostic.CA1040.severity'] }));
  assert.equal(found.length, 2);
  assert.match(found[0], /editorconfig.*"dotnet_diagnostic\.CA1040\.severity = warning"/);
  assert.match(found[1], /editorconfig.*"\[src\/A\.cs\] dotnet_diagnostic\.CA1040\.severity"/);
});

test('deltaForms_ContractsAndTelemetry_AreNotChecked', () => {
  assert.deepEqual(deltaForms(delta({ contracts: ['anything at all'], telemetry: ['YAF.Application', 'yaf.request'] })), []);
});
