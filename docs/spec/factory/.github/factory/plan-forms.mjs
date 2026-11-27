// Checks the line forms of a plan's `factory:delta` entries (plan 0015, idea 1; ADR-0025, ADR-0034).
// Input: the delta of `parseDelta` (plan.mjs). Output: problems as strings, as `ticketProblems` gives them.

/**
 * What the PublicAPI analyzer writes before a member of a namespace, with `~` for an obsolete one
 * (plan 0013's settled list). A namespace-wide prefix `YAF.X.*` covers a line only in the form it is declared in.
 */
export const PUBLIC_API_FORMS = [
  '', 'static ', 'static readonly ', 'static abstract ', 'const ', 'abstract ', 'virtual ', 'override ', 'override sealed ',
  '~', '~static ', '~static readonly ', '~abstract ', '~virtual ', '~override ', '~override sealed ',
];

const NAMESPACE_PREFIX = /^[A-Za-z_]\w*(\.[A-Za-z_]\w*)*\.\*$/;

/** The form of each category; publicApi, contracts and telemetry have none. A trailing `*` is a prefix. */
const FORMS = {
  // Lower-case codes with dots, `[a-z0-9_.]` (`03` §2): `core.request.malformed`, or a prefix `catalog.*`.
  errorCodes: { test: (e) => /^[a-z0-9_]+(\.[a-z0-9_]+)+$/.test(e) || /^[a-z0-9_]+(\.[a-z0-9_]+)*\.?\*$/.test(e), form: 'lower case a.b.c, or a prefix ending in *' },
  configuration: { test: (e) => /^\w+(:\w+)*:(\w+|\*)$/.test(e), form: 'A:B, sections joined by colons' },
  schema: { test: (e) => /^[A-Za-z_]\w*\*?$/.test(e), form: 'a migration name (an identifier)' },
  dependencies: { test: (e) => /^\w[\w.-]*(@\d+(\.\d+)*(-[0-9A-Za-z.-]+)?)?$/.test(e), form: 'Id or Id@Version' },
  editorconfig: { test: (e) => /^\[(\\.|[^\]\\])+\] [^\s=]+ = \S/.test(e), form: '[<section>] <key> = <value>' },
};

/**
 * Entries of the wrong form, and namespace-wide public API prefixes declared without all sixteen forms.
 * delta: parseDelta's result, or null (no block: `planFlags` reports that). → string[]
 */
export function deltaForms(delta) {
  if (!delta) return [];
  const problems = [];
  for (const [category, { test, form }] of Object.entries(FORMS)) {
    for (const entry of delta[category] ?? []) if (!test(entry)) problems.push(`factory:delta ${category}: "${entry}" is not of the form ${form}`);
  }
  const declared = new Map();
  const longestFirst = [...PUBLIC_API_FORMS].sort((a, b) => b.length - a.length);
  for (const entry of delta.publicApi ?? []) {
    const form = longestFirst.find((f) => entry.startsWith(f) && NAMESPACE_PREFIX.test(entry.slice(f.length)));
    if (form === undefined) continue;
    const ns = entry.slice(form.length);
    if (!declared.has(ns)) declared.set(ns, new Set());
    declared.get(ns).add(form);
  }
  for (const [ns, forms] of declared) {
    const missing = PUBLIC_API_FORMS.filter((f) => !forms.has(f)).map((f) => `"${f}${ns}"`);
    if (missing.length) problems.push(`factory:delta publicApi: the namespace prefix ${ns} lacks ${missing.length} of the ${PUBLIC_API_FORMS.length} forms the PublicAPI analyzer writes: ${missing.join(', ')}`);
  }
  return problems;
}
