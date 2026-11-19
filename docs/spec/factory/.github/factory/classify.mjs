// Risk tier of a pull request (ADR-0029, brief F6). Pure: everything it needs is passed in.
// The computed tier is a FLOOR. A label can raise it; nothing lowers it.
import { matchesAny } from './lib.mjs';

export const TIERS = ['low', 'medium', 'high'];
const rank = (t) => TIERS.indexOf(t);
export const maxTier = (a, b) => (rank(a) >= rank(b) ? a : b);

/**
 * True when a diff of Directory.Packages.props only moves existing packages to a newer patch version.
 * change: { added: string[], removed: string[] } of that file.
 */
export function isPatchBumpOnly(change) {
  const parse = (line) => {
    const m = line.match(/<PackageVersion\s+Include="([^"]+)"\s+Version="(\d+)\.(\d+)\.(\d+)([^"]*)"\s*\/>/);
    return m ? { id: m[1], major: m[2], minor: m[3], patch: Number(m[4]), rest: m[5] } : null;
  };
  const added = change.added.filter((l) => l.trim());
  const removed = change.removed.filter((l) => l.trim());
  if (added.length === 0 || added.length !== removed.length) return false;
  const before = new Map();
  for (const line of removed) {
    const p = parse(line);
    if (!p) return false;
    before.set(p.id, p);
  }
  return added.every((line) => {
    const p = parse(line);
    const old = p && before.get(p.id);
    return Boolean(old) && old.major === p.major && old.minor === p.minor && p.patch > old.patch && p.rest === '' && old.rest === '';
  });
}

/**
 * input:
 *   files            string[]  changed paths
 *   diff             Map<file, { added, removed }>
 *   title            pull request title (Conventional Commit)
 *   body             pull request body
 *   labels           string[]  labels currently applied
 *   raisedTo         tier requested by a tier:* label that someone other than the gates bot applied, or null
 *   sizeOverHard     boolean
 *   deltaEmpty       boolean   no change in the approved files
 *   deltaClean       boolean   every change in the approved files is declared by the plan and nothing is removed
 *   guardFindings    number
 *   policy
 * → { tier, floor, reasons: string[] }
 */
export function classify(input) {
  const { files, diff, title = '', body = '', labels = [], raisedTo = null, sizeOverHard = false, deltaEmpty = true, deltaClean = true, guardFindings = 0, policy } = input;
  const high = [];
  const medium = [];

  const patchFiles = policy.dependencyPatchFiles ?? [];
  const props = diff?.get('Directory.Packages.props');
  const patchOnly = props ? isPatchBumpOnly(props) : true;

  for (const f of files) {
    if (matchesAny(f, patchFiles)) {
      if (!patchOnly) high.push(`dependency change beyond a patch update: ${f}`);
      continue;
    }
    if (matchesAny(f, policy.sensitivePaths)) high.push(`sensitive path: ${f}`);
  }

  const cc = title.match(/^(\w+)(?:\([^)]*\))?(!)?:/);
  const type = cc?.[1] ?? null;
  if (cc?.[2] || /^BREAKING[ -]CHANGE:/m.test(body)) high.push('breaking change');
  if (sizeOverHard) high.push('over the hard size limit');
  if (guardFindings > 0) high.push(`${guardFindings} guard change(s)`);
  if (!deltaClean) high.push('delta not declared by the plan, or a removed entry');
  if (labels.includes('needs-human')) high.push('needs-human');
  if (labels.includes('major-release')) high.push('major-release');

  if (!deltaEmpty) medium.push('changes public API, error codes, contracts, configuration keys or telemetry names');
  if (type === null) medium.push('title is not a Conventional Commit');
  else if (!policy.tiers.low.types.includes(type) && !(files.length > 0 && files.every((f) => matchesAny(f, policy.tiers.low.paths)))) {
    // Only docs, tests and samples changed: low whatever the type says.
    medium.push(policy.tiers.medium.types.includes(type) ? `type ${type}` : `type ${type} is not listed as low`);
  }

  const floor = high.length ? 'high' : medium.length ? 'medium' : 'low';
  const reasons = high.length ? high : medium.length ? medium : ['no sensitive path, no delta, low-risk type'];
  let tier = floor;
  if (raisedTo && rank(raisedTo) > rank(floor)) {
    tier = raisedTo;
    reasons.push(`raised to ${raisedTo} by label`);
  }
  return { tier, floor, reasons: [...new Set(reasons)] };
}

/** Kind of pull request by the files it changes: 'spec' | 'plan' | 'code'. */
export function prKind(files, policy) {
  if (files.length === 0) return 'code';
  const isSpec = (f) => matchesAny(f, policy.docPaths.spec);
  const isPlan = (f) => matchesAny(f, policy.docPaths.plan);
  if (files.every((f) => isSpec(f) || isPlan(f))) return files.some(isSpec) ? 'spec' : 'plan';
  return 'code';
}
