// Creates the issues of a merged plan. Idempotent: every issue carries a hidden marker, and an
// existing marker is never created again.
//   node .github/factory/create-tickets.mjs specs/0003-typed-ids/plan.md [more plans…]
// Needs the factory app's token (GH_TOKEN): the default workflow token cannot write Projects.
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { api, repoSlug, loadPolicy } from './lib.mjs';
import { parseTickets, ticketProblems } from './plan.mjs';
import { addItem, setStatus } from './project.mjs';

export const ticketMarker = (folder, key) => `<!-- factory:ticket ${folder}#${key} -->`;
export const epicMarker = (folder) => `<!-- factory:epic ${folder} -->`;

/** The issue body of one ticket. blockedBy: issue numbers. */
export function ticketBody(folder, t, blockedBy, epicNumber) {
  return [
    ticketMarker(folder, t.key),
    `**Plan** \`${folder}/plan.md#${t.key}\` · **Epic** #${epicNumber}`,
    `**Requirements** ${t.requirements || '—'}`,
    `Blocked by: ${blockedBy.length ? blockedBy.map((n) => `#${n}`).join(', ') : 'nothing'}`,
    '',
    '### Touches',
    t.touches.map((p) => `- \`${p}\``).join('\n') || '- —',
    '',
    '### Failing test first',
    t.failingTest || '—',
    '',
    '### Verify',
    t.verify ? '```\n' + t.verify.replace(/^`|`$/g, '') + '\n```' : '—',
    '',
    `Expected tier: **${t.tier}** · estimate: ${t.size || '?'} changed lines`,
    '',
    'Status, evidence and deviations are recorded on this issue and its pull request.',
  ].join('\n');
}

function findByMarker(repo, marker) {
  const q = encodeURIComponent(`repo:${repo} is:issue in:body "${marker.replace(/<!--\s*|\s*-->/g, '')}"`);
  const hits = api(`search/issues?q=${q}&per_page=5`).items ?? [];
  return hits.find((i) => (i.body ?? '').includes(marker)) ?? null;
}

function ensureLabel(repo, name) {
  try { api(`repos/${repo}/labels/${encodeURIComponent(name)}`); }
  catch { api(`repos/${repo}/labels`, { method: 'POST', body: { name, color: 'ededed' } }); }
}

function titleOf(markdown, folder) {
  return (markdown.match(/^#\s+(.+?)(?::\s*plan)?\s*$/m)?.[1] ?? folder).trim();
}

function createFromPlan(planFile, policy) {
  const repo = repoSlug();
  const folder = path.posix.dirname(planFile.replaceAll('\\', '/'));
  const markdown = readFileSync(planFile, 'utf8');
  const tickets = parseTickets(markdown);
  if (tickets.length === 0) { console.log(`${planFile}: no Tickets table`); return; }
  const problems = ticketProblems(tickets, policy).filter((p) => /unknown ticket|comes later|duplicate/.test(p));
  if (problems.length) throw new Error(`${planFile}: ${problems.join('; ')}`);

  let epic = findByMarker(repo, epicMarker(folder));
  if (!epic) {
    epic = api(`repos/${repo}/issues`, { method: 'POST', body: { title: titleOf(markdown, folder), labels: ['factory', 'epic'], body: `${epicMarker(folder)}\nPlan: \`${planFile}\`\n\nTickets are the sub-issues of this issue.` } });
    console.log(`epic #${epic.number} created`);
  }
  addItem(epic.html_url);

  const numbers = new Map();
  for (const t of tickets) {
    let issue = findByMarker(repo, ticketMarker(folder, t.key));
    const blockedBy = t.dependsOn.map((k) => numbers.get(k)).filter(Boolean);
    if (!issue) {
      const labels = ['factory', `tier:${t.tier}`, `type:${t.type}`];
      if (t.area) { ensureLabel(repo, `area:${t.area}`); labels.push(`area:${t.area}`); }
      issue = api(`repos/${repo}/issues`, { method: 'POST', body: { title: t.title, labels, body: ticketBody(folder, t, blockedBy, epic.number) } });
      console.log(`${t.key} → #${issue.number}`);
      // Native links are a convenience for people; the automation reads the "Blocked by:" line.
      try { api(`repos/${repo}/issues/${epic.number}/sub_issues`, { method: 'POST', body: { sub_issue_id: issue.id } }); } catch (e) { console.warn(`sub-issue link failed for #${issue.number}: ${e.message.split('\n')[0]}`); }
      for (const k of t.dependsOn) {
        const blocker = numbers.get(k + ':id');
        if (!blocker) continue;
        try { api(`repos/${repo}/issues/${issue.number}/dependencies/blocked_by`, { method: 'POST', body: { issue_id: blocker } }); } catch (e) { console.warn(`dependency link failed for #${issue.number}: ${e.message.split('\n')[0]}`); }
      }
      setStatus(issue.html_url, blockedBy.length ? 'Backlog' : 'Ready');
    } else {
      console.log(`${t.key} exists as #${issue.number}`);
    }
    numbers.set(t.key, issue.number);
    numbers.set(t.key + ':id', issue.id);
  }
}

if (process.argv[1]?.endsWith('create-tickets.mjs')) {
  const policy = loadPolicy();
  const plans = process.argv.slice(2).filter((f) => f.endsWith('plan.md'));
  if (plans.length === 0) { console.log('no plan given'); process.exit(0); }
  for (const plan of plans) createFromPlan(plan, policy);
}
