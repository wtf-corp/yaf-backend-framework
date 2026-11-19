// Daily digest: what the factory merged, how large, how many review rounds, what waits for a human.
//   node .github/factory/digest.mjs [hours=24]
import { api, repoSlug } from './lib.mjs';

/** prs: [{ number, title, labels: string[], mergedBy, lines, rounds }] → markdown */
export function renderDigest(prs, waiting, since, bots) {
  const by = (t) => prs.filter((p) => p.labels.includes(`tier:${t}`));
  const auto = prs.filter((p) => bots.includes(p.mergedBy));
  const sizes = prs.map((p) => p.lines).sort((a, b) => a - b);
  const median = sizes.length ? sizes[Math.floor((sizes.length - 1) / 2)] : 0;
  const lines = [
    `### Factory digest — since ${since}`,
    '',
    `Merged: **${prs.length}** (low ${by('low').length}, medium ${by('medium').length}, high ${by('high').length}); merged without a human click: **${auto.length}**.`,
    `Size (raw additions + deletions): median ${median}, largest ${sizes.at(-1) ?? 0}.`,
    `Review rounds that ended in "changes required": ${prs.reduce((n, p) => n + p.rounds, 0)}.`,
    '',
  ];
  if (prs.length) lines.push('| PR | Tier | Lines | Rounds | Merged by |', '|---|---|---|---|---|', ...prs.map((p) => `| #${p.number} ${p.title.replaceAll('|', '\\|')} | ${p.labels.find((l) => l.startsWith('tier:'))?.slice(5) ?? '?'} | ${p.lines} | ${p.rounds} | ${p.mergedBy} |`), '');
  lines.push(waiting.length ? `Waiting for a human (\`needs-human\`): ${waiting.map((n) => `#${n}`).join(', ')}` : 'Nothing is waiting for a human.');
  return lines.join('\n');
}

if (process.argv[1]?.endsWith('digest.mjs')) {
  const repo = repoSlug();
  const hours = Number(process.argv[2] ?? 24);
  const since = new Date(Date.now() - hours * 3600 * 1000).toISOString();
  const q = encodeURIComponent(`repo:${repo} is:pr is:merged merged:>=${since}`);
  const found = api(`search/issues?q=${q}&per_page=100`).items ?? [];
  const prs = found.map((i) => {
    const pr = api(`repos/${repo}/pulls/${i.number}`);
    const comments = api(`repos/${repo}/issues/${i.number}/comments?per_page=100`, { paginate: true });
    const rounds = Number(comments.map((c) => c.body?.match(/<!-- factory:rounds=(\d+) -->/)?.[1]).find(Boolean) ?? 0);
    return { number: i.number, title: i.title, labels: i.labels.map((l) => l.name), mergedBy: pr.merged_by?.login ?? '?', lines: pr.additions + pr.deletions, rounds };
  });
  const waitingQ = encodeURIComponent(`repo:${repo} is:open label:needs-human`);
  const waiting = (api(`search/issues?q=${waitingQ}&per_page=100`).items ?? []).map((i) => i.number);
  const body = renderDigest(prs, waiting, since.slice(0, 16).replace('T', ' ') + ' UTC', ['yaf-factory[bot]', 'github-actions[bot]', 'github-merge-queue[bot]']);
  let issue = api(`repos/${repo}/issues?labels=factory-digest&state=open&per_page=1`)[0];
  if (!issue) issue = api(`repos/${repo}/issues`, { method: 'POST', body: { title: 'Factory digest', labels: ['factory-digest'], body: 'The factory posts one comment here per day.' } });
  api(`repos/${repo}/issues/${issue.number}/comments`, { method: 'POST', body: { body } });
}
