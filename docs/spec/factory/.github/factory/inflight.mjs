// What is in flight: the next action for an open factory pull request (specs/0003-unattended-orchestration, U4).
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
 * policy: the factory policy; no row of the table reads it yet. now: a Date or milliseconds.
 * → { action: 'none' | 'wait' | 'review' | 'fix' | 'human' | 'gate-failed', reason, round, cause }
 *   round: the fix round, for a failed review. cause: the failed check or checks; for `fix`, 'references' or the review.
 */
export function nextAction(pr, policy, now) {
  const result = (action, reason, extra = {}) => ({ action, reason, round: null, cause: null, ...extra });
  const checks = new Map((pr.checks ?? []).map((c) => [c.name, c]));
  const completed = (name) => checks.get(name)?.status === 'completed';
  const failed = (name) => completed(name) && !PASSED.includes(checks.get(name).conclusion);
  const running = (name) => checks.has(name) && !completed(name);
  const fresh = (name) => now - Date.parse(checks.get(name).startedAt) < STALE_MS;
  const flagged = failed('factory/plan-gate') && (checks.get('factory/plan-gate').title ?? '').startsWith('flagged');

  if (pr.labels.includes('needs-human')) return result('human', 'the label needs-human is set');
  if (pr.draft) return result('none', 'a draft');
  if (!completed('factory/tier')) return result('wait', 'the gates have not reported');

  if (pr.kind === 'code' && failed('references') && !pr.referencesFixTried) {
    return result('fix', 'the references are out of step: regenerate them with sync-references.mjs and commit the result', { cause: 'references' });
  }
  const broken = [...GATES, ...JOBS].filter(failed);
  if (failed('factory/plan-gate') && !flagged) broken.push('factory/plan-gate');
  if (broken.length) return result('gate-failed', `failed: ${broken.join(', ')}`, { cause: broken.join(', ') });

  if (REVIEWS.some((name) => running(name) && fresh(name))) return result('wait', 'the review runs');
  const owed = REVIEWS.find((name) => !completed(name));
  if (owed) return result('review', checks.has(owed) ? `${owed} has been in progress for 30 minutes or longer` : `${owed} is missing on the head commit`);

  const review = REVIEWS.find(failed);
  if (review) {
    if (pr.kind !== 'code') return result('human', 'a plan or spec fix is attended');
    if (pr.worktree?.locked) return result('wait', 'a fix is being written');
    if (pr.worktree?.ahead) return result('wait', 'a fix is committed and not pushed');
    return result('fix', `${review} failed`, { round: pr.rounds, cause: review });
  }

  if (pr.labels.includes('tier:high') && !pr.approved) return result('human', 'tier high: waiting for a human maintainer\'s approval of the head commit');
  if (failed('factory/human-approval')) return result('human', 'factory/human-approval failed');
  if (flagged) return result('human', 'a flagged plan: waiting for a human maintainer\'s approval');
  return result('wait', 'the checks are green: GitHub merges');
}
