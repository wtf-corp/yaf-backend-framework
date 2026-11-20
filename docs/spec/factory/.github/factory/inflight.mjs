// What is in flight: the next action for an open factory pull request (specs/0003-unattended-orchestration, U4),
// and what a ticket's worktree needs (U5b).
// Pure: everything it decides from is handed in, and so is the time.

const REVIEWS = ['factory/review', 'factory/doc-review'];
const GATES = ['factory/tier', 'factory/ticket', 'factory/size', 'factory/delta', 'factory/guards'];
const JOBS = ['references', 'factory-self-test', 'factory/test-evidence'];
const STALE_MS = 30 * 60_000; // a review in progress for longer was left behind by a script that died
const PASSED = ['success', 'neutral', 'skipped'];

/**
 * The next action for one open pull request: the first row that applies of the plan's next-action table.
 * pr: { labels, draft, kind ('code' | 'plan' | 'spec'), approved (a human maintainer approved the head commit),
 *       checks (the newest check run per name on the head commit: { name, status, conclusion, title, startedAt }),
 *       rounds (the rounds number of the review's summary comment), referencesFixTried,
 *       worktree (the one that holds the branch: { locked, ahead (of the pull request's head) }, or null) }
 * policy: the factory policy (limits.fixRounds). now: a Date or milliseconds.
 * → { action: 'none' | 'wait' | 'review' | 'fix' | 'human' | 'gate-failed', reason, round, cause }
 *   round: the fix round, for a failed review. cause: the failed check or checks; for `fix`, 'references' or the review.
 * Beyond the table: a references fix in the making is waited for; a review that ended without a verdict (cancelled,
 * timed out) is owed again; a failed review with the fix rounds used up is a human's.
 */
export function nextAction(pr, policy, now) {
  const result = (action, reason, extra = {}) => ({ action, reason, round: null, cause: null, ...extra });
  const checks = new Map((pr.checks ?? []).map((c) => [c.name, c]));
  const completed = (name) => checks.get(name)?.status === 'completed';
  const failed = (name) => completed(name) && !PASSED.includes(checks.get(name).conclusion);
  const running = (name) => checks.has(name) && !completed(name);
  const reviewed = (name) => completed(name) && [...PASSED, 'failure'].includes(checks.get(name).conclusion);
  const writing = pr.worktree?.locked ? 'is being written' : pr.worktree?.ahead ? 'is committed and not pushed' : null;
  const fresh = (name) => now - Date.parse(checks.get(name).startedAt) < STALE_MS;
  const flagged = failed('factory/plan-gate') && (checks.get('factory/plan-gate').title ?? '').startsWith('flagged');

  if (pr.labels.includes('needs-human')) return result('human', 'the label needs-human is set');
  if (pr.draft) return result('none', 'a draft');
  if (!completed('factory/tier')) return result('wait', 'the gates have not reported');

  if (pr.kind === 'code' && failed('references') && writing) return result('wait', `a references fix ${writing}`);
  if (pr.kind === 'code' && failed('references') && !pr.referencesFixTried) {
    return result('fix', 'the references are out of step: regenerate them with sync-references.mjs and commit the result', { cause: 'references' });
  }
  const broken = [...GATES, ...JOBS].filter(failed);
  if (failed('factory/plan-gate') && !flagged) broken.push('factory/plan-gate');
  if (broken.length) return result('gate-failed', `failed: ${broken.join(', ')}`, { cause: broken.join(', ') });

  if (REVIEWS.some((name) => running(name) && fresh(name))) return result('wait', 'the review runs');
  const owed = REVIEWS.find((name) => !reviewed(name));
  if (owed) {
    const why = !checks.has(owed) ? 'is missing on the head commit'
      : completed(owed) ? `ended without a verdict (${checks.get(owed).conclusion})`
      : checks.get(owed).startedAt ? 'has been in progress for 30 minutes or longer' : 'is in progress with no start time';
    return result('review', `${owed} ${why}`);
  }

  const review = REVIEWS.find((name) => checks.get(name).conclusion === 'failure');
  if (review) {
    if (pr.kind !== 'code') return result('human', 'a plan or spec fix is attended');
    if (!Number.isInteger(pr.rounds) || pr.rounds < 1) return result('human', 'no counted review summary');
    if (pr.rounds > policy.limits.fixRounds) return result('human', 'fix rounds used up');
    if (writing) return result('wait', `a fix ${writing}`);
    return result('fix', `${review} failed`, { round: pr.rounds, cause: review });
  }

  if (pr.labels.includes('tier:high') && !pr.approved) return result('human', 'tier high: waiting for a human maintainer\'s approval of the head commit');
  if (failed('factory/human-approval')) return result('human', 'factory/human-approval failed');
  if (flagged) return result('human', 'a flagged plan: waiting for a human maintainer\'s approval');
  const pending = [...GATES, ...JOBS, 'factory/human-approval', 'factory/plan-gate'].filter((name) => !completed(name));
  return result('wait', pending.length ? `not reported yet: ${pending.join(', ')}` : 'the checks are green: GitHub merges');
}

/**
 * `git worktree list --porcelain` → [{ path, branch, head, locked }], the main checkout included.
 * branch: the short name, or null for a detached worktree.
 */
export function parseWorktrees(porcelain) {
  const worktrees = [];
  for (const line of porcelain.split(/\r?\n/)) {
    const [key, value] = line.split(/ (.*)/s);
    if (key === 'worktree') worktrees.push({ path: value, branch: null, head: null, locked: false });
    else if (key === 'HEAD') worktrees.at(-1).head = value;
    else if (key === 'branch') worktrees.at(-1).branch = value.replace(/^refs\/heads\//, '');
    else if (key === 'locked') worktrees.at(-1).locked = true;
  }
  return worktrees;
}

/**
 * What a ticket's worktree needs: the first row that applies of the plan's worktree-state table (U5b).
 * w: { worktree ({ head, locked }, as parseWorktrees gives it), ticketLabels,
 *      pr (the branch's pull request: { labels, merged, open }, or null),
 *      review (markers.mjs localReview for the ticket's comments and the worktree's head),
 *      awaitingPush (markers.mjs findMarker 'awaiting-push' for that head: the comment or null),
 *      originHead (the branch's commit on origin, or null) }
 * policy: the factory policy (limits.fixRounds).
 * → { state: 'human' | 'working' | 'remove' | 'local-review' | 'local-fix' | 'push-owed' | 'open-pr' | 'in-flight',
 *     round, findings (the marker's, for local-fix), exhausted (local-fix at the limit: needs-human, no further fix),
 *     notified (push-owed: the owner was told about this head) }
 */
export function worktreeState(w, policy) {
  const result = (state, extra = {}) => ({ state, round: null, findings: null, exhausted: false, notified: false, ...extra });
  const { verdict, round, findings } = w.review;
  if ([...w.ticketLabels, ...(w.pr?.labels ?? [])].includes('needs-human')) return result('human');
  if (w.worktree.locked) return result('working');
  if (w.pr?.merged) return result('remove');
  if (verdict === 'changes-required') return result('local-fix', { round, findings, exhausted: round >= policy.limits.fixRounds });
  if (verdict !== 'ready') return result('local-review');
  if (w.originHead !== w.worktree.head) return result('push-owed', { notified: Boolean(w.awaitingPush) });
  return result(w.pr?.open ? 'in-flight' : 'open-pr');
}
