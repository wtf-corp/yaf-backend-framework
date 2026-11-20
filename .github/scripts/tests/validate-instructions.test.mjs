// Tests of validate-instructions.mjs. Each case builds a small valid tree in a temporary directory,
// breaks one thing, and looks for the problem; the last case runs the check on this repository.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { validate } from '../validate-instructions.mjs';

const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');

const SPEC = `# 0009 — Thing

## Problem
## Goals
## Non-goals
## Requirements
## Acceptance scenarios
## Impact checklist
## Open questions
## Out of scope
## Decision log
`;

const PLAN = `# 0009 — Thing: plan

## Approach
## Affected packages
## Public API, schema and error-code delta

\`\`\`factory:delta
{ "publicApi": [], "schema": [], "errorCodes": [], "dependencies": [] }
\`\`\`

## Schema and migration plan
## Test strategy
## Risks and rollback
## Verification commands
## Decision log
## Tickets

| Key | Title | Depends on | Touches | Size | Failing test first | Verify |
|---|---|---|---|---|---|---|
| T1 | chore: one | — | \`a.txt\` | 10 | a test | a command |
`;

const skill = (name, body = 'Body.\n') => `---\nname: ${name}\ndescription: What it covers and when to load it.\n---\n\n${body}`;
const agent = (name, skills) => `---\nname: ${name}\ndescription: A role.\nskills:\n${skills.map((s) => `  - ${s}`).join('\n')}\n---\n\nInstructions.\n`;

/** A minimal tree that passes every check. → its root */
function fixture() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'validate-instructions-'));
  const files = {
    'docs/spec/agents/skills/yaf-a/SKILL.md': skill('yaf-a', 'See [the template](references/t.md).\n'),
    'docs/spec/agents/skills/yaf-a/references/t.md': 'Template.\n',
    'docs/spec/factory/.claude/agents/role.md': agent('role', ['yaf-a']),
    'docs/spec/factory/.github/factory/policy.json': JSON.stringify({ size: { soft: 400, hard: 1000 } }),
    '.claude/agents/role.md': agent('role', ['yaf-a']),
    '.agents/skills/yaf-a/SKILL.md': skill('yaf-a'),
    'AGENTS.md': '# AGENTS\n\nThe [plan](specs/0009-thing/plan.md#tickets). In code: `[x](missing.md)`.\n\n```\n[y](missing.md)\n```\n',
    'specs/0009-thing/spec.md': SPEC,
    'specs/0009-thing/plan.md': PLAN,
  };
  for (const [p, content] of Object.entries(files)) write(root, p, content);
  return root;
}

function write(root, p, content) {
  fs.mkdirSync(path.dirname(path.join(root, p)), { recursive: true });
  fs.writeFileSync(path.join(root, p), content);
}

function problemsAfter(change) {
  const root = fixture();
  try {
    change(root);
    return validate(root);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
}

const has = (problems, pattern) => assert.ok(problems.some((p) => pattern.test(p)), `no problem matching ${pattern} in:\n${problems.join('\n')}`);

test('the minimal fixture has no problem', () => {
  assert.deepEqual(problemsAfter(() => {}), []);
});

test('a skill whose name differs from its directory is reported', () => {
  has(problemsAfter((r) => write(r, 'docs/spec/agents/skills/yaf-a/SKILL.md', skill('yaf-b'))), /yaf-a\/SKILL\.md: name 'yaf-b' is not the directory name 'yaf-a'/);
});

test('a skill without front matter or description is reported', () => {
  has(problemsAfter((r) => write(r, 'docs/spec/agents/skills/yaf-a/SKILL.md', '# no front matter\n')), /yaf-a\/SKILL\.md: no front matter/);
  has(problemsAfter((r) => write(r, 'docs/spec/agents/skills/yaf-a/SKILL.md', '---\nname: yaf-a\n---\n')), /yaf-a\/SKILL\.md: no description/);
});

test('a skill over 500 lines is reported', () => {
  has(problemsAfter((r) => write(r, 'docs/spec/agents/skills/yaf-a/SKILL.md', skill('yaf-a', 'line\n'.repeat(500)))), /yaf-a\/SKILL\.md: 505 lines, over 500/);
});

test('a dead relative link outside code is reported, one in code is not', () => {
  const problems = problemsAfter((r) => write(r, 'specs/AGENTS.md', 'See [gone](../nowhere.md) and `[x](also-gone.md)`.\n'));
  has(problems, /specs\/AGENTS\.md: link '\.\.\/nowhere\.md' does not resolve/);
  assert.equal(problems.length, 1);
});

test('a kit agent definition that lists a missing skill or has the wrong name is reported', () => {
  has(problemsAfter((r) => write(r, 'docs/spec/factory/.claude/agents/role.md', agent('role', ['yaf-a', 'yaf-missing']))), /factory\/\.claude\/agents\/role\.md: skill 'yaf-missing' does not exist/);
  has(problemsAfter((r) => write(r, 'docs/spec/factory/.claude/agents/role.md', agent('other', ['yaf-a']))), /factory\/\.claude\/agents\/role\.md: name 'other' is not the file name 'role'/);
});

test('a .claude/agents stub without front matter is reported', () => {
  has(problemsAfter((r) => write(r, '.claude/agents/role.md', 'Read the role.\n')), /^\.claude\/agents\/role\.md: no front matter/);
});

test('a spec or a plan missing a template heading is reported', () => {
  has(problemsAfter((r) => write(r, 'specs/0009-thing/spec.md', SPEC.replace('## Non-goals\n', ''))), /spec\.md: no heading '## Non-goals'/);
  has(problemsAfter((r) => write(r, 'specs/0009-thing/plan.md', PLAN.replace('## Risks and rollback\n', ''))), /plan\.md: no heading '## Risks and rollback'/);
});

test('a plan without a delta block, with a broken one, or with a ticket problem is reported', () => {
  has(problemsAfter((r) => write(r, 'specs/0009-thing/plan.md', PLAN.replace('factory:delta', 'json'))), /plan\.md: no factory:delta block/);
  has(problemsAfter((r) => write(r, 'specs/0009-thing/plan.md', PLAN.replace('"schema": []', '"schema": ['))), /plan\.md: factory:delta block does not parse/);
  has(problemsAfter((r) => write(r, 'specs/0009-thing/plan.md', PLAN.replace('| — |', '| T7 |'))), /plan\.md: T1: depends on unknown ticket T7/);
});

test('vendored skills without a lock are reported, with a lock they are not', () => {
  const vendor = (r) => write(r, '.agents/skills/karpathy-guidelines/SKILL.md', skill('karpathy-guidelines'));
  has(problemsAfter(vendor), /vendored skill\(s\) karpathy-guidelines without \.agents\/skills\.lock/);
  assert.deepEqual(problemsAfter((r) => { vendor(r); write(r, '.agents/skills.lock', '{}\n'); }), []);
});

test('the current tree passes', () => {
  assert.deepEqual(validate(REPO), []);
});
