// Detects a weakened guard in a diff (ADR-0026): suppressions, skipped tests, removed control tags,
// edited approved files, relaxed analyzer or banned-API configuration.
import { matchesAny } from './lib.mjs';

const ADDED_PATTERNS = [
  [/#pragma\s+warning\s+disable/, 'pragma warning disable'],
  [/\[\s*(assembly:\s*)?SuppressMessage\b/, 'SuppressMessage'],
  [/\bUnconditionalSuppressMessage\b/, 'UnconditionalSuppressMessage'],
  [/<NoWarn>/i, 'NoWarn'],
  [/<WarningsNotAsErrors>/i, 'WarningsNotAsErrors'],
  [/<TreatWarningsAsErrors>\s*false/i, 'TreatWarningsAsErrors=false'],
  [/\bSkip\s*=\s*"/, 'skipped test'],
  [/\[\s*(Fact|Theory)\s*\(.*\bExplicit\s*=\s*true/, 'explicit-only test'],
  [/<NuGetAuditSuppress\b/, 'NuGet audit suppression'],
  [/dotnet_diagnostic\.[A-Za-z0-9]+\.severity\s*=\s*(none|silent|suggestion)/, 'analyzer severity lowered'],
  [/#nullable\s+disable/, 'nullable disable'],
];

const REMOVED_PATTERNS = [
  [/\[\s*Control\s*\(/, 'removed [Control] tag'],
  [/\[\s*(Fact|Theory)\b/, 'removed test'],
];

const APPROVED = ['**/*.approved.*'];
const BANNED = ['**/BannedSymbols.txt', 'BannedSymbols.txt'];
// The prose of specs and plans configures nothing; it may quote a setting (plan 0015, T22).
const PROSE = ['specs/**/*.md'];

/** diff: Map<file, { added, removed }> → [{ file, what, line }] */
export function findGuardChanges(diff) {
  const findings = [];
  for (const [file, change] of diff) {
    if (matchesAny(file, PROSE)) continue;
    for (const line of change.added) for (const [re, what] of ADDED_PATTERNS) if (re.test(line)) findings.push({ file, what, line: line.trim() });
    const isTest = /(^|\/)tests\//.test(file);
    for (const line of change.removed) {
      for (const [re, what] of REMOVED_PATTERNS) {
        if (what === 'removed test' && !isTest) continue;
        if (re.test(line)) findings.push({ file, what, line: line.trim() });
      }
    }
    // Existing lines of an approved file may not disappear or change; adding lines is the normal case.
    if (matchesAny(file, APPROVED)) {
      const added = change.added.map((s) => s.trim());
      for (const line of change.removed) if (line.trim() && !added.includes(line.trim())) findings.push({ file, what: 'approved line removed or changed', line: line.trim() });
    }
    if (matchesAny(file, BANNED)) for (const line of change.removed) if (line.trim()) findings.push({ file, what: 'banned API entry removed', line: line.trim() });
  }
  return findings;
}

/** A removed test that reappears (moved or renamed attribute line) is not a finding. */
export function dropMovedTests(findings, diff) {
  const addedTests = [...diff.values()].flatMap((c) => c.added).filter((l) => /\[\s*(Fact|Theory)\b/.test(l)).length;
  let budget = addedTests;
  return findings.filter((f) => (f.what === 'removed test' && budget-- > 0 ? false : true));
}

/** declaredFiles: guard files whose change the base plan declares exactly (ADR-0034); their findings are reported, not counted. */
export function guardVerdict(allFindings, labelAppliedByHuman, declaredFiles = []) {
  const findings = allFindings.filter((f) => !declaredFiles.includes(f.file));
  if (allFindings.length === 0) return { pass: true, title: 'no weakened guard' };
  if (findings.length === 0) return { pass: true, title: `${allFindings.length} guard change(s), all in files whose change is declared by the plan (ADR-0034)` };
  return labelAppliedByHuman
    ? { pass: true, title: `${findings.length} guard change(s), accepted by a maintainer (guard-change)` }
    : { pass: false, title: `${findings.length} guard change(s): fix the cause, or a maintainer applies guard-change` };
}
