// Ticket status.
//   node .github/factory/status.mjs set <issue-number> "In progress"     (the orchestrator, locally)
//   node .github/factory/status.mjs sync                                 (factory-status.yml, on issue events)
//
// `sync` moves closed tickets to Done and tickets whose blockers are all closed from Backlog to Ready.
// Blockers are read from the "Blocked by: #1, #2" line that create-tickets.mjs writes into the issue body,
// so the automation does not depend on the dependency API.
import { api, repoSlug } from './lib.mjs';
import { setStatus, statuses } from './project.mjs';

/** Issue numbers in the "Blocked by:" line of a ticket body. */
export function blockers(body = '') {
  const m = body.match(/^Blocked by:\s*(.*)$/m);
  return m ? [...m[1].matchAll(/#(\d+)/g)].map((x) => Number(x[1])) : [];
}

/**
 * tickets: [{ number, state, body, labels: string[] }], current: Map<number, status>
 * → [{ number, status }] changes to make
 */
export function plannedChanges(tickets, current) {
  const state = new Map(tickets.map((t) => [t.number, t.state]));
  const changes = [];
  for (const t of tickets) {
    if (t.labels.includes('epic')) continue;
    const now = current.get(t.number) ?? null;
    if (t.state === 'closed') {
      if (now !== 'Done') changes.push({ number: t.number, status: 'Done' });
      continue;
    }
    const open = blockers(t.body).filter((b) => state.get(b) !== 'closed');
    if (open.length === 0 && (now === 'Backlog' || now === null) && !t.labels.includes('blocked')) changes.push({ number: t.number, status: 'Ready' });
    if (open.length > 0 && now === 'Ready') changes.push({ number: t.number, status: 'Backlog' });
  }
  return changes;
}

if (process.argv[1]?.endsWith("status.mjs")) {
  const [command, a, b] = process.argv.slice(2);
  const repo = repoSlug();
  const url = (n) => `https://github.com/${repo}/issues/${n}`;
  if (command === 'set') {
    setStatus(url(Number(a)), b);
  } else if (command === 'sync') {
    const issues = api(`repos/${repo}/issues?labels=factory&state=all&per_page=100`, { paginate: true })
      .filter((i) => !i.pull_request)
      .map((i) => ({ number: i.number, state: i.state, body: i.body ?? '', labels: i.labels.map((l) => l.name) }));
    for (const c of plannedChanges(issues, statuses())) setStatus(url(c.number), c.status);
  } else {
    console.error('usage: status.mjs set <issue> <status> | status.mjs sync');
    process.exit(2);
  }
}
