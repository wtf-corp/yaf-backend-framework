// Decides whether a change to `.editorconfig` or `Directory.Packages.props` is exactly what a merged plan declares (ADR-0034).
// It only decides. The caller passes the declarations, read from the plan on the base branch (ADR-0034 Decision 5).
import { matchesAny } from './lib.mjs';

const verdict = (reasons) => ({ declared: reasons.length === 0, reasons });

/** `.editorconfig` → { preamble: string[], sections: [{ header, lines }] }, comments and blank lines left out, lines trimmed. */
function parseSections(text) {
  const preamble = [];
  const sections = [];
  for (const raw of text.split(/\r?\n/)) {
    const line = raw.trim();
    if (line === '' || line.startsWith('#') || line.startsWith(';')) continue;
    const header = line.match(/^\[(.*)\]$/);
    if (header) sections.push({ header: header[1], lines: [] });
    else (sections.at(-1)?.lines ?? preamble).push(line);
  }
  return { preamble, sections };
}

const same = (a, b) => a.length === b.length && a.every((x, i) => x === b[i]);

/**
 * The one file a section header names, or null (ADR-0034 Decision 2): a repository-relative path with a `/`,
 * no unescaped glob character, no `..` segment, `\` only as an escape. A leading `/` is removed.
 */
export function sectionFile(header) {
  let file = '';
  let slash = false;
  for (let i = 0; i < header.length; i++) {
    const c = header[i];
    if (c === '\\') {
      if (i + 1 >= header.length) return null;
      file += header[++i];
    } else if ('*?[]{}'.includes(c)) return null;
    else { file += c; slash ||= c === '/'; }
  }
  if (file.includes('\\') || !slash) return null;
  if (file.startsWith('/')) file = file.slice(1);
  if (file.split('/').some((s) => s === '' || s === '.' || s === '..')) return null;
  return file;
}

/**
 * base, head: the texts of `.editorconfig`. entries: the plan's `editorconfig` entries, `[<header>] <setting line>`.
 * neverLowered: globs (policy.declaredGuards.neverLowered). → { declared, reasons }
 */
export function editorconfigVerdict(base, head, entries, neverLowered) {
  const b = parseSections(base);
  const h = parseSections(head);
  const reasons = [];
  if (!same(b.preamble, h.preamble)) reasons.push('the lines before the first section changed');
  // The base sections, in their order and unchanged, are a subsequence of the head's; the others are new.
  const added = [];
  let j = 0;
  for (const s of h.sections) {
    const old = b.sections[j];
    if (old && old.header === s.header && same(old.lines, s.lines)) j++;
    else added.push(s);
  }
  for (const s of b.sections.slice(j)) reasons.push(`[${s.header}]: section of the base changed or removed`);
  const declared = new Set(entries);
  const lowerGlobs = neverLowered.map((g) => g.toLowerCase());
  for (const s of added) {
    const file = sectionFile(s.header);
    if (file === null) { reasons.push(`[${s.header}]: does not name one file`); continue; }
    if (matchesAny(file.toLowerCase(), lowerGlobs)) { reasons.push(`[${s.header}]: ${file} is under a neverLowered path`); continue; }
    for (const line of s.lines) if (!declared.has(`[${s.header}] ${line}`)) reasons.push(`[${s.header}] ${line}: not declared`);
  }
  return verdict(reasons);
}

/** `Directory.Packages.props` without comments → { pins: [{ text, attributes, child }], rest: string[] } */
function parsePackages(text) {
  const pins = [];
  const element = /<PackageVersion\b([^>]*?)(?:\/>|>([\s\S]*?)<\/PackageVersion\s*>)/g;
  const rest = text.replace(/<!--[\s\S]*?-->/g, '').replace(element, (whole, attrs, inner) => {
    const attributes = {};
    for (const a of attrs.matchAll(/([^\s=]+)\s*=\s*(?:"([^"]*)"|'([^']*)')/g)) attributes[a[1]] = a[2] ?? a[3];
    const names = attrs.replace(/([^\s=]+)\s*=\s*(?:"[^"]*"|'[^']*')/g, '').trim();
    pins.push({ text: whole, attributes, child: (inner ?? '').trim() !== '' || names !== '' });
    return '';
  });
  return { pins, rest: rest.split(/\r?\n/).filter((l) => l.trim() !== '') };
}

/**
 * base, head: the texts of `Directory.Packages.props`. dependencies: the plan's `dependencies` entries;
 * only `Id@Version` ones declare a pin. → { declared, reasons }
 */
export function packagesVerdict(base, head, dependencies) {
  const b = parsePackages(base);
  const h = parsePackages(head);
  const reasons = [];
  const added = [...h.pins];
  for (const pin of b.pins) {
    const at = added.findIndex((p) => p.text === pin.text);
    if (at < 0) reasons.push(`${pin.attributes.Include ?? pin.text}: PackageVersion of the base changed or removed`);
    else added.splice(at, 1);
  }
  if (!same(b.rest, h.rest)) reasons.push('the rest of the file changed (comments and PackageVersion elements taken out)');
  const declared = new Set(dependencies.filter((d) => d.includes('@')));
  for (const pin of added) {
    const names = Object.keys(pin.attributes).sort().join();
    if (pin.child || names !== 'Include,Version') { reasons.push(`${pin.text}: a new PackageVersion has exactly Include and Version and no child`); continue; }
    const id = `${pin.attributes.Include}@${pin.attributes.Version}`;
    if (!declared.has(id)) reasons.push(`${id}: not declared`);
  }
  return verdict(reasons);
}
