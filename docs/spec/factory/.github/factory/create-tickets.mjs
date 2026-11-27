// Creates the issues of a merged plan. Idempotent: every issue carries a hidden marker, and an
// existing marker is never created again, so a run that stopped is resumed by running it again.
//   node .github/factory/create-tickets.mjs specs/0003-typed-ids/plan.md [more plans…]
// Needs the factory app's token (GH_TOKEN): the default workflow token cannot write Projects.
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { run as runCommand, repoSlug, loadPolicy } from './lib.mjs';
import { parseTickets, ticketProblems } from './plan.mjs';
import { addItem, setStatus, statuses } from './project.mjs';
import { ghApi } from './ratelimit.mjs';

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
    t.touches.map((p) => (p.includes('`') ? `- ${p}` : `- \`${p}\``)).join('\n') || '- —',
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

/**
 * The first status of a ticket that already exists: the one it would have got when created, or null
 * when it is closed or has a status, so a re-run never moves a ticket. current: Map<number, status>.
 */
export function firstStatus(issue, blockedBy, current) {
  if (issue.state !== 'open' || current.get(issue.number)) return null;
  return blockedBy.length ? 'Backlog' : 'Ready';
}

/**
 * The plan's epic and tickets among listed issues: Map<marker, issue>. Pull requests and the markers
 * of other plans are left out; of two issues with one marker the older one counts.
 */
export function indexByMarker(issues, folder) {
  const index = new Map();
  const ticketPrefix = `<!-- factory:ticket ${folder}#`;
  for (const i of [...issues].sort((a, b) => a.number - b.number)) {
    if (i.pull_request) continue;
    for (const [marker] of (i.body ?? '').matchAll(/<!-- factory:(?:ticket|epic) \S+ -->/g)) {
      const ours = marker === epicMarker(folder) || marker.startsWith(ticketPrefix);
      if (ours && !index.has(marker)) index.set(marker, i);
    }
  }
  return index;
}

/** The REST calls of one run, each with the retry of ratelimit.mjs. `body` is sent as JSON. */
function client(run, retry) {
  return (endpoint, { method = 'GET', body } = {}) => {
    const args = [endpoint, '--method', method, '-H', 'Accept: application/vnd.github+json'];
    if (body !== undefined) args.push('--input', '-');
    const input = body !== undefined ? { input: JSON.stringify(body) } : {};
    return ghApi(args, { ...retry, run: (cmd, a) => run(cmd, a, input) });
  };
}

/** Every issue and pull request of the repository, page by page (`--paginate` does not combine with `-i`). */
function listIssues(call, repo) {
  const all = [];
  for (let page = 1; ; page++) {
    const items = call(`repos/${repo}/issues?state=all&per_page=100&page=${page}`) ?? [];
    all.push(...items);
    if (items.length < 100) return all;
  }
}

function ensureLabel(call, repo, name) {
  try { call(`repos/${repo}/labels/${encodeURIComponent(name)}`); }
  catch { call(`repos/${repo}/labels`, { method: 'POST', body: { name, color: 'ededed' } }); }
}

function titleOf(markdown, folder) {
  return (markdown.match(/^#\s+(.+?)(?::\s*plan)?\s*$/m)?.[1] ?? folder).trim();
}

function createTickets(planFile, folder, markdown, tickets, { repo, api, project: { addItem, setStatus, statuses }, at }) {
  const existing = indexByMarker(listIssues(api, repo), folder);
  let epic = existing.get(epicMarker(folder));
  if (!epic) {
    epic = api(`repos/${repo}/issues`, { method: 'POST', body: { title: titleOf(markdown, folder), labels: ['factory', 'epic'], body: `${epicMarker(folder)}\nPlan: \`${planFile}\`\n\nTickets are the sub-issues of this issue.` } });
    console.log(`epic #${epic.number} created`);
  }
  addItem(epic.html_url);

  const numbers = new Map();
  let current = null; // the Project's statuses: read once per plan, and only when a ticket exists
  for (const t of tickets) {
    at.key = t.key;
    let issue = existing.get(ticketMarker(folder, t.key));
    const blockedBy = t.dependsOn.map((k) => numbers.get(k)).filter(Boolean);
    if (!issue) {
      const labels = ['factory', `tier:${t.tier}`, `type:${t.type}`];
      if (t.area) { ensureLabel(api, repo, `area:${t.area}`); labels.push(`area:${t.area}`); }
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
      current ??= statuses();
      const status = firstStatus(issue, blockedBy, current);
      if (status) setStatus(issue.html_url, status);
    }
    numbers.set(t.key, issue.number);
    numbers.set(t.key + ':id', issue.id);
  }
}

/**
 * Creates the plan's epic and the tickets that do not exist yet. `run` (the `gh` runner), `repo`, `read`,
 * `project` and `retry` (options of ratelimit.mjs) are parameters so that a test passes fakes. A failure,
 * such as a rate limit that outlasts the retry's budget, names the ticket it stopped at and how to resume.
 */
export function createFromPlan(planFile, policy, {
  run = runCommand, repo = repoSlug(), read = (f) => readFileSync(f, 'utf8'),
  project = { addItem, setStatus, statuses }, retry = {},
} = {}) {
  const folder = path.posix.dirname(planFile.replaceAll('\\', '/'));
  const markdown = read(planFile);
  const tickets = parseTickets(markdown);
  if (tickets.length === 0) { console.log(`${planFile}: no Tickets table`); return; }
  const problems = ticketProblems(tickets, policy).filter((p) => /unknown ticket|comes later|duplicate/.test(p));
  if (problems.length) throw new Error(`${planFile}: ${problems.join('; ')}`);
  const at = { key: tickets[0].key };
  try {
    createTickets(planFile, folder, markdown, tickets, { repo, api: client(run, retry), project, at });
  } catch (e) {
    throw new Error(`${planFile}: stopped at ${at.key}: ${e.message.split('\n')[0]}\nResume later with: gh workflow run factory-tickets.yml -f plan=${planFile}`, { cause: e });
  }
}

if (process.argv[1]?.endsWith('create-tickets.mjs')) {
  const policy = loadPolicy();
  const plans = process.argv.slice(2).filter((f) => f.endsWith('plan.md'));
  if (plans.length === 0) { console.log('no plan given'); process.exit(0); }
  for (const plan of plans) createFromPlan(plan, policy);
}
