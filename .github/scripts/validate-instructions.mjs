// Validates the agent instructions: skills, agent definitions, links in AGENTS.md and SKILL.md files,
// the vendored-skills lock, and the format of every spec and plan (17 §2 `process`, ADR-0024, ADR-0025, ADR-0028).
//
//   node .github/scripts/validate-instructions.mjs                 exit 1 and list the problems, if any
//   --root <dir>                                                   repository root to check (default: current directory)
//
// Node 22+, no dependencies.
import { readdirSync, readFileSync, existsSync } from 'node:fs';
import path from 'node:path';
import { frontMatter } from './sync-references.mjs';
import { parseDelta, parseTickets, ticketProblems } from '../../docs/spec/factory/.github/factory/plan.mjs';

const SKILLS = 'docs/spec/agents/skills';
const KIT_AGENTS = 'docs/spec/factory/.claude/agents';
const STUB_AGENTS = '.claude/agents';
const POLICY = 'docs/spec/factory/.github/factory/policy.json';
const VENDORED = '.agents/skills';
const LOCK = '.agents/skills.lock';
const MAX_SKILL_LINES = 500;
const SKIP_DIRS = new Set(['.git', 'node_modules', 'bin', 'obj', 'artifacts', 'TestResults']);
const SKIP_PATHS = new Set(['.claude/worktrees']);

// The headings of the templates in yaf-writing-specs and yaf-writing-plans (references/).
const SPEC_HEADINGS = ['Problem', 'Goals', 'Non-goals', 'Requirements', 'Acceptance scenarios', 'Impact checklist', 'Open questions', 'Out of scope', 'Decision log'];
const PLAN_HEADINGS = ['Approach', 'Affected packages', 'Public API, schema and error-code delta', 'Schema and migration plan', 'Test strategy', 'Risks and rollback', 'Verification commands', 'Decision log', 'Tickets'];

const lf = (text) => text.replaceAll('\r\n', '\n');
const dirs = (root, dir) => (existsSync(path.join(root, dir)) ? readdirSync(path.join(root, dir), { withFileTypes: true }).filter((e) => e.isDirectory()).map((e) => e.name).sort() : []);
const mdFiles = (root, dir) => (existsSync(path.join(root, dir)) ? readdirSync(path.join(root, dir)).filter((f) => f.endsWith('.md')).sort() : []);

/** Front matter of `file` as { fields, skills }, or a problem string. Top-level `key: value` lines and the `skills:` list. */
function readFrontMatter(root, file) {
  let fm;
  try {
    fm = frontMatter(readFileSync(path.join(root, file), 'utf8'), file);
  } catch (e) {
    return e.message;
  }
  const fields = {};
  const skills = [];
  let list = null;
  for (const line of fm.split('\n').slice(1, -2)) {
    const item = line.match(/^\s+-\s+(.+)$/);
    if (item && list === 'skills') skills.push(item[1].trim());
    const kv = line.match(/^([A-Za-z][\w-]*):\s*(.*)$/);
    if (kv) {
      fields[kv[1]] = kv[2].trim().replace(/^(["'])(.*)\1$/, '$2');
      list = kv[1];
    }
  }
  return { fields, skills };
}

function checkSkills(root, problems) {
  for (const name of dirs(root, SKILLS)) {
    const file = `${SKILLS}/${name}/SKILL.md`;
    if (!existsSync(path.join(root, file))) {
      problems.push(`${file}: missing`);
      continue;
    }
    const fm = readFrontMatter(root, file);
    if (typeof fm === 'string') problems.push(fm);
    else {
      if (fm.fields.name !== name) problems.push(`${file}: name '${fm.fields.name ?? ''}' is not the directory name '${name}'`);
      if (!fm.fields.description) problems.push(`${file}: no description`);
    }
    const text = lf(readFileSync(path.join(root, file), 'utf8'));
    const lines = text.split('\n').length - (text.endsWith('\n') ? 1 : 0);
    if (lines > MAX_SKILL_LINES) problems.push(`${file}: ${lines} lines, over ${MAX_SKILL_LINES}`);
  }
}

function checkAgents(root, dir, checkSkillList, problems) {
  for (const f of mdFiles(root, dir)) {
    const file = `${dir}/${f}`;
    const fm = readFrontMatter(root, file);
    if (typeof fm === 'string') {
      problems.push(fm);
      continue;
    }
    const name = f.slice(0, -3);
    if (fm.fields.name !== name) problems.push(`${file}: name '${fm.fields.name ?? ''}' is not the file name '${name}'`);
    if (!checkSkillList) continue;
    for (const s of fm.skills) if (!existsSync(path.join(root, SKILLS, s, 'SKILL.md'))) problems.push(`${file}: skill '${s}' does not exist in ${SKILLS}/`);
  }
}

/** Repository-relative paths of every AGENTS.md (and the bundle's AGENTS.<part>.md sources) and SKILL.md. */
function instructionFiles(root, dir = '') {
  return readdirSync(path.join(root, dir), { withFileTypes: true }).flatMap((e) => {
    const rel = dir ? `${dir}/${e.name}` : e.name;
    if (e.isDirectory()) return SKIP_DIRS.has(e.name) || SKIP_PATHS.has(rel) ? [] : instructionFiles(root, rel);
    return /^(AGENTS(\.[\w-]+)?|SKILL)\.md$/.test(e.name) ? [rel] : [];
  });
}

/** Markdown with fenced code blocks and code spans removed. */
function withoutCode(text) {
  let fenced = false;
  return lf(text).split('\n').map((line) => {
    if (/^\s*(```|~~~)/.test(line)) {
      fenced = !fenced;
      return '';
    }
    return fenced ? '' : line.replace(/(`+)[^`]*?\1/g, '');
  }).join('\n');
}

function checkLinks(root, problems) {
  for (const file of instructionFiles(root)) {
    const text = withoutCode(readFileSync(path.join(root, file), 'utf8'));
    for (const [, target] of text.matchAll(/\]\(\s*<?([^)\s>]+)>?(?:\s+"[^"]*")?\s*\)/g)) {
      if (/^[a-z][a-z0-9+.-]*:/i.test(target) || target.startsWith('#')) continue;
      const rel = decodeURIComponent(target.split(/[#?]/)[0]);
      const resolved = rel.startsWith('/') ? path.join(root, rel) : path.join(root, path.dirname(file), rel);
      if (!existsSync(resolved)) problems.push(`${file}: link '${target}' does not resolve`);
    }
  }
}

function checkVendoredLock(root, problems) {
  const vendored = dirs(root, VENDORED).filter((name) => !existsSync(path.join(root, SKILLS, name, 'SKILL.md')));
  if (vendored.length && !existsSync(path.join(root, LOCK))) problems.push(`${VENDORED}/: vendored skill(s) ${vendored.join(', ')} without ${LOCK}`);
}

function checkHeadings(file, text, headings, problems) {
  for (const h of headings) if (!new RegExp(`^## ${h.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}\\s*$`, 'm').test(text)) problems.push(`${file}: no heading '## ${h}'`);
}

function checkSpecsAndPlans(root, problems) {
  const policy = JSON.parse(readFileSync(path.join(root, POLICY), 'utf8'));
  for (const dir of dirs(root, 'specs')) {
    const spec = `specs/${dir}/spec.md`;
    if (existsSync(path.join(root, spec))) checkHeadings(spec, lf(readFileSync(path.join(root, spec), 'utf8')), SPEC_HEADINGS, problems);
    const plan = `specs/${dir}/plan.md`;
    if (!existsSync(path.join(root, plan))) continue;
    const text = lf(readFileSync(path.join(root, plan), 'utf8'));
    checkHeadings(plan, text, PLAN_HEADINGS, problems);
    try {
      if (!parseDelta(text)) problems.push(`${plan}: no factory:delta block`);
    } catch (e) {
      problems.push(`${plan}: factory:delta block does not parse: ${e.message}`);
    }
    try {
      for (const p of ticketProblems(parseTickets(text), policy)) problems.push(`${plan}: ${p}`);
    } catch (e) {
      problems.push(`${plan}: Tickets table: ${e.message}`);
    }
  }
}

/** → string[] problems; empty when the instructions are valid. */
export function validate(root) {
  const problems = [];
  checkSkills(root, problems);
  checkAgents(root, KIT_AGENTS, true, problems);
  checkAgents(root, STUB_AGENTS, false, problems);
  checkLinks(root, problems);
  checkVendoredLock(root, problems);
  checkSpecsAndPlans(root, problems);
  return problems;
}

if (process.argv[1]?.endsWith('validate-instructions.mjs')) {
  const args = process.argv.slice(2);
  const root = args.includes('--root') ? args[args.indexOf('--root') + 1] : '.';
  const problems = validate(root);
  for (const p of problems) console.error(p);
  if (problems.length) {
    console.error(`${problems.length} problem(s) in the agent instructions`);
    process.exit(1);
  }
  console.log('agent instructions, skills, agent definitions, specs and plans are valid');
}
