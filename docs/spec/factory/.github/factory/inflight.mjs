// What is in flight: the next action for an open factory pull request (specs/0003-unattended-orchestration, U4),
// and what a ticket's worktree needs (U5b); as a command, the list of both for the orchestrator's tick (U5c):
//   node .github/factory/inflight.mjs   → JSON { pullRequests, worktrees }; it reads only (GITHUB_REPOSITORY, gh, git)
// The functions are pure: everything they decide from is handed in, and so is the time.
import { loadPolicy, run, repoSlug, makeHumanCheck } from './lib.mjs';
import { prKind } from './classify.mjs';
import { approvingHuman } from './human-approval.mjs';
import { parseBody } from './ticket.mjs';
import { findMarker, localReview, reviewSummary, stackedOn } from './markers.mjs';

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
 *       worktree (the one that holds the branch: { locked, ahead (of the pull request's head) }, or null),
 *       stackedWriting (a worktree stacked on this pull request is being written) }
 * policy: the factory policy (limits.fixRounds). now: a Date or milliseconds.
 * → { action: 'none' | 'wait' | 'review' | 'fix' | 'human' | 'gate-failed', reason, round, cause }
 *   round: the fix round, for a failed review. cause: the failed check or checks; for `fix`, 'references' or the review.
 * Beyond the table: a references fix in the making is waited for; a fix waits while a ticket stacked on the pull
 * request is being written (their touches may overlap); a review that ended without a verdict (cancelled,
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
  const fix = (reason, extra) => (pr.stackedWriting ? result('wait', 'a ticket stacked on it is being written') : result('fix', reason, extra));
  const flagged = failed('factory/plan-gate') && (checks.get('factory/plan-gate').title ?? '').startsWith('flagged');

  if (pr.labels.includes('needs-human')) return result('human', 'the label needs-human is set');
  if (pr.draft) return result('none', 'a draft');
  if (!completed('factory/tier')) return result('wait', 'the gates have not reported');

  if (pr.kind === 'code' && failed('references') && writing) return result('wait', `a references fix ${writing}`);
  if (pr.kind === 'code' && failed('references') && !pr.referencesFixTried) {
    return fix('the references are out of step: regenerate them with sync-references.mjs and commit the result', { cause: 'references' });
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
    return fix(`${review} failed`, { round: pr.rounds, cause: review });
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
 *      originHead (the branch's commit on origin, or null),
 *      stack (from the stacked-on marker: { onto (its commit), contains (the head still contains it),
 *             dependency (its pull request: { merged, open }, or null) }, or null) }
 * policy: the factory policy (limits.fixRounds).
 * → { state: 'human' | 'working' | 'remove' | 'local-review' | 'local-fix' | 'stacked' | 'restack' | 'push-owed'
 *       | 'open-pr' | 'in-flight',
 *     round, findings (the marker's, for local-fix), exhausted (local-fix at the limit: needs-human, no further fix),
 *     notified (push-owed: the owner was told about this head), onto (restack only: the commit to rebase off) }
 * A stacked worktree (its head still contains the marker's commit) gets the local review first, so a restack that
 * conflicted is a local fix; reviewed, it waits while its dependency is open and is owed a restack once that merged.
 * A dependency closed unmerged is a human's. After the restack the head lacks the commit: the ordinary states apply.
 */
export function worktreeState(w, policy) {
  const result = (state, extra = {}) => ({ state, round: null, findings: null, exhausted: false, notified: false, ...extra });
  const { verdict, round, findings } = w.review;
  if ([...w.ticketLabels, ...(w.pr?.labels ?? [])].includes('needs-human')) return result('human');
  if (w.worktree.locked) return result('working');
  if (w.pr?.merged) return result('remove');
  const stack = w.stack?.contains ? w.stack : null;
  if (stack && !stack.dependency?.open && !stack.dependency?.merged) return result('human');
  if (verdict === 'changes-required') return result('local-fix', { round, findings, exhausted: round >= policy.limits.fixRounds });
  if (verdict !== 'ready') return result('local-review');
  if (stack) return stack.dependency.merged ? result('restack', { onto: stack.onto }) : result('stacked');
  if (w.originHead !== w.worktree.head) return result('push-owed', { notified: Boolean(w.awaitingPush) });
  return result(w.pr?.open ? 'in-flight' : 'open-pr');
}

const TICKET_BRANCH = /^[a-z]+\/(\d+)-/; // <type>/<issue>-<slug>
const labelNames = (labels) => (labels ?? []).map((l) => (typeof l === 'string' ? l : l.name));
/** The one issue the pull request's description closes, or null. */
const closedIssue = (pr) => { const { closes } = parseBody(pr.body ?? ''); return closes.length === 1 ? closes[0] : null; };

/**
 * REST check runs and commit statuses of a commit → the newest check per name, as nextAction reads them.
 * A run counts only from the app the ruleset accepts it from: the two reviews from the reviewer app (the gates bot
 * where the policy names none), everything else from the gates bot. A status counts only from the gates bot, for a
 * name the ruleset accepts from it. Anybody else's run or status of that name is not read. The newest run of a name
 * and the newest status of a context count (the same time: the highest id); with both, the one that has not passed
 * decides, as GitHub requires both to pass.
 */
function newestChecks(checkRuns, policy, statuses = []) {
  const source = (name) => (REVIEWS.includes(name) && policy.bots.reviewer) || policy.bots.gates;
  const newest = (list, key, at) => {
    const found = new Map();
    for (const c of list) {
      const old = found.get(key(c));
      if (!old || at(c) > at(old) || (at(c) === at(old) && c.id > old.id)) found.set(key(c), c);
    }
    return found;
  };
  const runs = newest(checkRuns.filter((c) => c.app?.slug === source(c.name).replace(/\[bot\]$/, '')), (c) => c.name, (c) => c.started_at ?? '');
  const posted = newest(statuses.filter((s) => s.creator?.login === policy.bots.gates && source(s.context) === policy.bots.gates), (s) => s.context, (s) => s.created_at ?? '');
  const checks = new Map([...runs.values()].map((c) => [c.name, { name: c.name, status: c.status, conclusion: c.conclusion, title: c.output?.title ?? '', startedAt: c.started_at }]));
  const rank = (c) => (c.status !== 'completed' ? 1 : PASSED.includes(c.conclusion) ? 2 : 0); // failed, then running, then passed
  for (const s of posted.values()) {
    const done = s.state !== 'pending';
    const check = { name: s.context, status: done ? 'completed' : 'in_progress', conclusion: done ? s.state : null, title: s.description ?? '', startedAt: s.created_at };
    const run = checks.get(s.context);
    if (!run || rank(check) < rank(run)) checks.set(s.context, check);
  }
  return [...checks.values()];
}

/**
 * One open pull request's row, from what was fetched (U5c). As the REST API gives them: pr, files (the changed
 * paths), reviews, checkRuns and statuses (of the head commit), comments (the pull request's) and ticketComments (those of the
 * issue it closes). worktree: the one that holds the branch ({ locked, ahead }), or null. isHuman: (user) => boolean.
 * stackedWriting: a locked worktree is stacked on this pull request.
 * → { number, branch, issue (the closed one, or null), kind, action, reason, round, cause (as nextAction gives them),
 *     fixRounds, findings (for a fix after a failed review: the review's summary comment, verbatim) }
 * The summary counts from the reviewer app or the gates bot only, a references-fix marker from the factory bot only;
 * a references fix handed out for any commit of the ticket is the one try, and a pull request that closes no single
 * issue has no ticket to mark the try on, so it gets none.
 */
export function pullRequestRow({ pr, files, reviews, checkRuns, statuses = [], comments, ticketComments, worktree, isHuman, stackedWriting = false }, policy, now) {
  const kind = prKind(files, policy);
  const issue = closedIssue(pr);
  // The marker's syntax is markers.mjs's: the first thing in its comment, `<!-- factory:references-fix <sha> -->`.
  const fixTried = ticketComments.some((c) => c.user?.login === policy.bots.factory && c.body?.startsWith('<!-- factory:references-fix '));
  const authors = [policy.bots.reviewer, policy.bots.gates].filter(Boolean);
  const summary = reviewSummary(comments, kind === 'code' ? 'factory/review' : 'factory/doc-review', authors);
  const next = nextAction({
    labels: labelNames(pr.labels),
    draft: pr.draft,
    kind,
    approved: Boolean(approvingHuman(reviews, { author: pr.user.login, headSha: pr.head.sha, isHuman })),
    checks: newestChecks(checkRuns, policy, statuses),
    rounds: summary?.rounds,
    referencesFixTried: issue === null || fixTried,
    worktree,
    stackedWriting,
  }, policy, now);
  return { number: pr.number, branch: pr.head.ref, issue, kind, ...next, fixRounds: policy.limits.fixRounds,
    findings: next.action === 'fix' && next.cause !== 'references' ? summary.body : null };
}

/**
 * One ticket worktree's row. worktree: as parseWorktrees gives it, on a branch <type>/<issue>-<slug>. ticketLabels:
 * the label names of that issue; ticketComments: its comments. pr: the branch's pull request as the REST API gives
 * it, or null. originHead: the branch's commit on origin, or null. The markers are read for the worktree's head.
 * stack: { sha (the stacked-on marker's commit), contains (the head still contains it), pr (the dependency's pull
 * request as the REST API gives it, or null) }, or null for a ticket without the marker.
 * → { path, branch, head, issue, pullRequest (its number, or null), …worktreeState }
 */
export function worktreeRow({ worktree, ticketLabels, ticketComments, pr, originHead, stack = null }, policy) {
  const state = worktreeState({
    worktree,
    ticketLabels,
    pr: pr ? { labels: labelNames(pr.labels), merged: Boolean(pr.merged_at), open: pr.state === 'open' } : null,
    review: localReview(ticketComments, worktree.head, policy.bots.factory),
    awaitingPush: findMarker(ticketComments, 'awaiting-push', worktree.head, policy.bots.factory),
    originHead,
    stack: stack && { onto: stack.sha, contains: stack.contains, dependency: stack.pr && { merged: Boolean(stack.pr.merged_at), open: stack.pr.state === 'open' } },
  }, policy);
  return { path: worktree.path, branch: worktree.branch, head: worktree.head, issue: Number(worktree.branch.match(TICKET_BRANCH)[1]), pullRequest: pr?.number ?? null, ...state };
}

/**
 * The command part: it reads only. gh, git: (...args) => stdout; git runs in the repository and throws on a
 * non-zero exit. → { pullRequests: a row per open pull request labelled `factory` or opened by the factory bot,
 * worktrees: a row per worktree on a ticket branch }
 * A pull request or a worktree whose calls fail gets the row { number | path, branch, action | state: 'failed',
 * reason } and does not stop the others. The three lists everything starts from may throw.
 */
export function inflight({ repo, policy, gh, git, isHuman, now }) {
  const list = (endpoint) => JSON.parse(gh('api', endpoint, '--paginate', '--slurp')).flat();
  const once = (fn, cache = new Map()) => (key) => (cache.has(key) ? cache.get(key) : cache.set(key, fn(key)).get(key));
  const read = once((issue) => list(`repos/${repo}/issues/${issue}/comments?per_page=100`));
  const comments = (issue) => read(String(issue));
  const worktrees = parseWorktrees(git('worktree', 'list', '--porcelain'));
  const ticketWorktrees = worktrees.filter((w) => TICKET_BRANCH.test(w.branch ?? ''));
  // The ticket's newest stacked-on marker ({ sha, pr }) and whether the worktree's head still contains its commit, or
  // null. git exits 1 for "not an ancestor"; any other failure is thrown, so the row fails rather than read unstacked.
  const stackOf = once((worktree) => {
    const on = stackedOn(comments(worktree.branch.match(TICKET_BRANCH)[1]), policy.bots.factory);
    if (!on) return null;
    try { git('merge-base', '--is-ancestor', on.sha, worktree.head); return { ...on, contains: true }; }
    catch (e) { if (e.status === 1) return { ...on, contains: false }; throw e; }
  });
  const origin = new Map(git('ls-remote', '--heads', 'origin').split(/\r?\n/).filter(Boolean)
    .map((line) => line.split('\t')).map(([sha, ref]) => [ref.replace(/^refs\/heads\//, ''), sha]));
  // Ahead: another head that contains the pull request's. A pull request head this repository does not have is not contained.
  const ahead = (head, prHead) => {
    if (head === prHead) return false;
    try { git('merge-base', '--is-ancestor', prHead, head); return true; } catch { return false; }
  };
  const attempt = (id, key, row) => { try { return row(); } catch (e) { return { ...id, [key]: 'failed', reason: e.message }; } };

  const pullRequests = list(`repos/${repo}/pulls?state=open&per_page=100`)
    .filter((pr) => labelNames(pr.labels).includes('factory') || pr.user.login === policy.bots.factory)
    .map((pr) => attempt({ number: pr.number, branch: pr.head.ref }, 'action', () => {
      const issue = closedIssue(pr);
      const held = worktrees.find((w) => w.branch === pr.head.ref);
      return pullRequestRow({
        pr,
        files: list(`repos/${repo}/pulls/${pr.number}/files?per_page=100`).map((f) => f.filename),
        reviews: list(`repos/${repo}/pulls/${pr.number}/reviews?per_page=100`),
        checkRuns: list(`repos/${repo}/commits/${pr.head.sha}/check-runs?filter=all&per_page=100`).flatMap((page) => page.check_runs),
        statuses: list(`repos/${repo}/commits/${pr.head.sha}/statuses?per_page=100`),
        comments: comments(pr.number),
        ticketComments: issue ? comments(issue) : [],
        worktree: held ? { locked: held.locked, ahead: ahead(held.head, pr.head.sha) } : null,
        isHuman,
        stackedWriting: ticketWorktrees.some((w) => w.locked && stackOf(w)?.contains && stackOf(w).pr === pr.number),
      }, policy, now);
    }));

  const rows = ticketWorktrees.map((worktree) => attempt({ path: worktree.path, branch: worktree.branch }, 'state', () => {
    const issue = worktree.branch.match(TICKET_BRANCH)[1];
    // The branch's pull requests, newest first: the open one, else the merged one, else the newest closed one.
    const pulls = list(`repos/${repo}/pulls?state=all&head=${repo.split('/')[0]}:${worktree.branch}&per_page=100`);
    const ticketLabels = labelNames(JSON.parse(gh('api', `repos/${repo}/issues/${issue}`)).labels);
    const ticketComments = comments(issue);
    const on = stackOf(worktree); // the dependency's pull request is read only while the head is stacked on it
    return worktreeRow({
      worktree,
      ticketLabels,
      ticketComments,
      pr: pulls.find((p) => p.state === 'open') ?? pulls.find((p) => p.merged_at) ?? pulls[0] ?? null,
      originHead: origin.get(worktree.branch) ?? null,
      stack: on && { sha: on.sha, contains: on.contains, pr: on.contains ? JSON.parse(gh('api', `repos/${repo}/pulls/${on.pr}`)) : null },
    }, policy);
  }));
  return { pullRequests, worktrees: rows };
}

if (process.argv[1]?.endsWith('inflight.mjs')) {
  const policy = loadPolicy();
  const result = inflight({ repo: repoSlug(), policy, gh: (...args) => run('gh', args), git: (...args) => run('git', args), isHuman: makeHumanCheck(policy), now: Date.now() });
  console.log(JSON.stringify(result, null, 2));
}
