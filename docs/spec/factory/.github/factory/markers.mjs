// What an unattended run records on GitHub, and how it is read back (plan 0003, T6). Pure: no calls, no state.
// The markers are hidden comments on the ticket's issue, written by the orchestrator as the factory app:
//   <!-- factory:local-review <sha> ready|changes-required round=<n> -->   then the findings, in the same comment
//   <!-- factory:awaiting-push <sha> -->                                    the owner was told the push is theirs
//   <!-- factory:references-fix <sha> -->                                   a references fix was handed out
// round=<n> is the number of local fix rounds already used on the ticket when the verdict was given: 0 for the
// first local review. It is apart from `factory:rounds`, which post-review.mjs counts for the pull-request review.
// A marker counts only as the first thing in its comment: every writer puts it there, and a marker quoted further
// down (in evidence, in findings, in a summary) is text.
// comments: issue comments as the REST API gives them ({ user: { login }, body }), oldest first.

const SHA = /^[0-9a-f]{40}$/;
const VERDICT = /^(ready|changes-required) round=\d+$/;
const LOCAL_REVIEW = /^<!-- factory:local-review (\S+) (ready|changes-required) round=(\d+) -->/;
const ROUNDS = /^\r?\n<!-- factory:rounds=(\d+) -->/; // the line after the summary marker, where renderSummary puts it

/** A comment counts only when one of the named logins wrote it; with no login named, nothing counts. */
const writtenBy = (comment, logins) => logins.some((login) => login && comment.user?.login === login);

/**
 * The marker's text. value: `ready round=0` or `changes-required round=1` for local-review, nothing otherwise.
 * Throws on a commit that is not 40 lower-case hex characters, and on a local-review value of another form.
 */
export function marker(kind, sha, value = '') {
  if (!SHA.test(sha)) throw new Error(`marker: not a full commit sha: ${sha}`);
  if (kind === 'local-review' && !VERDICT.test(value)) throw new Error(`marker: not a local-review verdict and round: ${value}`);
  return `<!-- factory:${kind} ${sha}${value ? ` ${value}` : ''} -->`;
}

/**
 * → the newest comment by `author` (the factory bot of the policy) that starts with the marker of that kind for
 * that commit, or null. A local-review marker counts only in its full form, as localReview reads it.
 */
export function findMarker(comments, kind, sha, author) {
  const start = `<!-- factory:${kind} ${sha} `; // the space ends the commit: a prefix of it is another commit
  const full = (body) => kind !== 'local-review' || LOCAL_REVIEW.test(body);
  return comments.findLast((c) => writtenBy(c, [author]) && c.body?.startsWith(start) && full(c.body)) ?? null;
}

/**
 * → { verdict, round, findings, highestRound }: the newest local-review verdict on that commit (`ready`,
 * `changes-required`, or null with round null when there is none), the findings that follow the marker in its
 * comment, and the highest round recorded on the ticket for any commit (null when none is recorded).
 */
export function localReview(comments, sha, author) {
  const result = { verdict: null, round: null, findings: '', highestRound: null };
  for (const c of comments) {
    const m = writtenBy(c, [author]) ? c.body?.match(LOCAL_REVIEW) : null;
    if (!m) continue;
    const round = Number(m[3]);
    result.highestRound = Math.max(result.highestRound ?? 0, round);
    if (m[1] === sha) Object.assign(result, { verdict: m[2], round, findings: c.body.slice(m[0].length).trim() });
  }
  return result;
}

/**
 * → { rounds, body } of the newest comment that starts with the review's summary marker for that check and was
 * written by one of `authors` (the reviewer app or the gates bot), or null. The body goes verbatim to an implementer
 * as findings and the rounds number decides a fix round, so a summary written by anyone else does not count.
 */
export function reviewSummary(comments, check, authors) {
  const start = `<!-- factory:review-summary ${check} -->`;
  const found = comments.findLast((c) => writtenBy(c, authors) && c.body?.startsWith(start));
  return found ? { rounds: Number(found.body.slice(start.length).match(ROUNDS)?.[1] ?? 0), body: found.body } : null;
}
