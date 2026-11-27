// Checks each ticket's declared tier against the floor its touches and title give under the policy (plan 0015, idea 1).
// The floor is classify.mjs's, computed from the touches as if they were the pull request's files.
import path from 'node:path';
import { matchesAny } from './lib.mjs';
import { classify, TIERS } from './classify.mjs';

/** The path of a touches entry without a note after it: "`.editorconfig` (three sections)" → ".editorconfig". */
function pathOf(entry) {
  const span = entry.match(/^`([^`]+)`/);
  return (span ? span[1] : entry.split(/\s/)[0]).replaceAll('\\', '/').replace(/^\.\//, '');
}

const isGlob = (p) => /[*?]/.test(p);
// A last segment without an extension is a folder, except a dot file such as `.editorconfig`.
const isFolder = (p) => p.endsWith('/') || (!isGlob(p) && !p.split('/').pop().startsWith('.') && path.posix.extname(p.split('/').pop()) === '');
const folder = (p) => (p.endsWith('/') ? p : `${p}/`);

/** The approved files of `policy.delta.sources` a touch names, less the PublicAPI files that are new on the base. */
function approvedFiles(p, policy, exists) {
  const sources = Object.values(policy.delta.sources).flat();
  const dir = path.posix.dirname(p);
  const names = isGlob(p)
    ? sources.map((g) => (dir === '.' ? '' : `${dir}/`) + g.split('/').pop()).filter((c) => matchesAny(c, [p]))
    : [p];
  return names.filter((c) => matchesAny(c, sources) && !(/(^|\/)PublicAPI\.\w+\.txt$/.test(c) && !exists(c)));
}

/**
 * The guard files of `policy.declaredGuards.files` the plan declares entries for (ADR-0034): `.editorconfig` with
 * `editorconfig` entries none of whose sections names a `neverLowered` file, `Directory.Packages.props` with `Id@Version`.
 */
function declaredFiles(delta, policy) {
  if (!delta) return [];
  const sections = delta.editorconfig.map((e) => (e.match(/^\[((?:\\.|[^\]\\])+)\]/)?.[1] ?? '').replace(/\\(.)/g, '$1').replace(/^\//, ''));
  const editorconfig = sections.length > 0 && !sections.some((s) => s === '' || matchesAny(s, policy.declaredGuards.neverLowered));
  const packages = delta.dependencies.some((d) => d.includes('@'));
  return policy.declaredGuards.files.filter((f) => (f === '.editorconfig' ? editorconfig : f === 'Directory.Packages.props' && packages));
}

/** Sensitive globs below a folder touch that its probe does not match: `src/Application/` holds `src/Application/Authorization/**`. */
function sensitiveBelow(p, policy) {
  const segments = p.split('/').filter(Boolean);
  return policy.sensitivePaths.filter((g) => {
    const head = g.split('/').slice(0, segments.length);
    return g.split('/').length > segments.length && !head.includes('**') && path.posix.matchesGlob(segments.join('/'), head.join('/'));
  });
}

/**
 * Tickets whose declared tier is below the floor (a problem) or above it (a note: raising is allowed, but say so),
 * and folder touches that hold a sensitive subfolder (a note: a file there makes the pull request high).
 * A folder touch is probed with a file inside it. The plan's tier and title are those of `parseTickets`.
 * delta: `parseDelta`'s result, or null. exists(path): whether a repository-relative path is on the base.
 * → { problems: string[], notes: string[] }
 */
export function tierFloor(tickets, { policy, delta, exists }) {
  const declared = declaredFiles(delta, policy);
  const problems = [], notes = [];
  for (const t of tickets) {
    const touches = t.touches.map(pathOf);
    const files = touches.map((p) => (isFolder(p) ? `${folder(p)}x` : p));
    const approved = touches.flatMap((p) => approvedFiles(p, policy, exists));
    const { floor, reasons } = classify({
      files, title: t.title, policy, declaredFiles: declared, deltaEmpty: approved.length === 0,
      // A dependency file in a plan cannot be shown to be a patch update only.
      diff: new Map([['Directory.Packages.props', { added: ['?'], removed: [] }]]),
    });
    const why = reasons.map((r) => (r.startsWith('changes public API') ? `${r}: ${approved.join(', ')}` : r)).join('; ');
    const [d, f] = [TIERS.indexOf(t.tier), TIERS.indexOf(floor)];
    if (d < f) problems.push(`${t.key}: declared tier ${t.tier} is below the floor ${floor}: ${why}`);
    else if (d > f) notes.push(`${t.key}: declared tier ${t.tier} is above the floor ${floor} (${why}); raising is allowed, say why in the plan`);
    for (const p of touches.filter((x, i) => isFolder(x) && !matchesAny(files[i], policy.sensitivePaths))) {
      const below = sensitiveBelow(p, policy);
      if (below.length) notes.push(`${t.key}: \`${folder(p)}\` contains sensitive paths: ${below.join(', ')}; a file there makes the pull request high`);
    }
  }
  return { problems, notes };
}
