// Reads the machine-readable parts of a plan.md: the `factory:delta` block and the Tickets table.

const DELTA_CATEGORIES = ['publicApi', 'schema', 'errorCodes', 'contracts', 'configuration', 'telemetry', 'dependencies', 'editorconfig'];

/**
 * The block is a fenced code block whose info string is `factory:delta`, containing JSON:
 *   ```factory:delta
 *   { "publicApi": ["YAF.Core.Result<T>*"], "schema": [], "errorCodes": ["core.result.*"], "dependencies": [] }
 *   ```
 * An entry ending in `*` is a prefix. Returns null when the block is missing.
 */
export function parseDelta(markdown) {
  const m = markdown.match(/```factory:delta\s*\n([\s\S]*?)\n```/);
  if (!m) return null;
  const raw = JSON.parse(m[1]);
  const delta = {};
  for (const c of DELTA_CATEGORIES) delta[c] = Array.isArray(raw[c]) ? raw[c].map(String) : [];
  const unknown = Object.keys(raw).filter((k) => !DELTA_CATEGORIES.includes(k));
  if (unknown.length) throw new Error(`factory:delta has unknown categories: ${unknown.join(', ')}`);
  return delta;
}

function cells(row) {
  return row.replace(/^\s*\|/, '').replace(/\|\s*$/, '').split(/(?<!\\)\|/).map((c) => c.trim().replaceAll('\\|', '|'));
}

const COLUMN_ALIASES = {
  key: ['key', 'id'],
  title: ['title', 'ticket'],
  requirements: ['requirements', 'requirements / sections', 'sections'],
  dependsOn: ['depends on', 'blocked by'],
  touches: ['touches'],
  tier: ['tier', 'tier (expected)'],
  size: ['size', 'size estimate', 'estimate'],
  failingTest: ['failing test first', 'failing test'],
  verify: ['verify', 'verification', 'verification command'],
  type: ['type'],
  area: ['area'],
};

/**
 * Splits a cell at `,`, `;` and `<br>` at depth 0 only: outside a code span and outside `{…}`, `(…)`
 * and `[…]`. An unmatched closing bracket is ignored; a backtick with no later one is plain text.
 */
function splitCell(text) {
  const parts = [];
  const br = /<br\s*\/?>/y;
  let start = 0, depth = 0, span = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (c === '`') { if (span || text.includes('`', i + 1)) span = !span; continue; }
    if (span) continue;
    if ('{(['.includes(c)) depth++;
    else if ('})]'.includes(c)) depth = Math.max(0, depth - 1);
    else if (depth === 0) {
      br.lastIndex = i;
      const end = c === ',' || c === ';' ? i + 1 : br.test(text) ? br.lastIndex : -1;
      if (end > 0) { parts.push(text.slice(start, i)); start = end; i = end - 1; }
    }
  }
  return [...parts, text.slice(start)];
}

/** Parses the table under the `## Tickets` heading. Returns [] when there is none. */
export function parseTickets(markdown) {
  const section = markdown.split(/^##\s+Tickets\s*$/m)[1];
  if (!section) return [];
  const lines = section.split(/^##\s/m)[0].split('\n').filter((l) => l.trim().startsWith('|'));
  if (lines.length < 3) return [];
  const header = cells(lines[0]).map((h) => h.toLowerCase());
  const index = {};
  for (const [field, names] of Object.entries(COLUMN_ALIASES)) index[field] = header.findIndex((h) => names.includes(h));
  if (index.key < 0 || index.title < 0) throw new Error('Tickets table needs at least the columns Key and Title');
  const cell = (row, field) => (index[field] >= 0 ? (row[index[field]] ?? '') : '');
  const list = (text) => splitCell(text).map((s) => s.trim()).map((s) => (/^`[^`]*`$/.test(s) ? s.slice(1, -1) : s)).filter((s) => s && s !== '—' && s !== '-' && s.toLowerCase() !== 'none');
  return lines.slice(2).map(cells).filter((r) => r[index.key]).map((r) => ({
    key: cell(r, 'key'),
    title: cell(r, 'title'),
    requirements: cell(r, 'requirements'),
    dependsOn: list(cell(r, 'dependsOn')),
    touches: list(cell(r, 'touches')),
    tier: (cell(r, 'tier').toLowerCase().match(/low|medium|high/) ?? ['medium'])[0],
    size: Number((cell(r, 'size').match(/\d+/) ?? [0])[0]),
    failingTest: cell(r, 'failingTest'),
    verify: cell(r, 'verify'),
    type: (cell(r, 'type').toLowerCase().match(/feat|fix|refactor|docs|test|chore/) ?? ['feat'])[0],
    area: cell(r, 'area'),
  }));
}

/** True when the plan has a "Deviations from the specification" section that says something other than "none". */
export function hasDeviations(markdown) {
  const section = markdown.split(/^##\s+Deviations[^\n]*$/m)[1];
  if (!section) return false;
  const text = section.split(/^##\s/m)[0].replace(/<!--[\s\S]*?-->/g, '').trim();
  return text !== '' && !/^none\.?$/i.test(text);
}

/**
 * Structural problems in the ticket list that a script can decide (the plan reviewer judges the rest).
 * → string[]
 */
export function ticketProblems(tickets, policy) {
  const problems = [];
  const keys = tickets.map((t) => t.key);
  const seen = new Set();
  tickets.forEach((t, i) => {
    if (seen.has(t.key)) problems.push(`${t.key}: duplicate key`);
    seen.add(t.key);
    for (const d of t.dependsOn) {
      const at = keys.indexOf(d);
      if (at < 0) problems.push(`${t.key}: depends on unknown ticket ${d}`);
      else if (at >= i) problems.push(`${t.key}: depends on ${d}, which comes later`);
    }
    if (t.size > policy.size.soft) problems.push(`${t.key}: estimate ${t.size} is over the soft limit ${policy.size.soft}`);
    if (!t.failingTest && t.type !== 'docs') problems.push(`${t.key}: no failing test named`);
    if (!t.verify) problems.push(`${t.key}: no verification command`);
    if (t.touches.length === 0) problems.push(`${t.key}: no 'touches'`);
  });
  return problems;
}

/**
 * A plan is flagged (a human maintainer must approve it) when it declares a public API change,
 * a schema change, a new dependency, a deviation from the spec, or a ticket in tier high.
 * → { flagged, reasons }
 */
export function planFlags(markdown, policy) {
  const reasons = [];
  const delta = parseDelta(markdown);
  if (!delta) reasons.push('no factory:delta block (cannot show that nothing is declared)');
  else for (const c of policy.delta.flaggingCategories) if (delta[c]?.length) reasons.push(`declares ${c}: ${delta[c].length} entr${delta[c].length === 1 ? 'y' : 'ies'}`);
  if (hasDeviations(markdown)) reasons.push('declares a deviation from the specification');
  const high = parseTickets(markdown).filter((t) => t.tier === 'high').map((t) => t.key);
  if (high.length) reasons.push(`tickets in tier high: ${high.join(', ')}`);
  return { flagged: reasons.length > 0, reasons };
}
