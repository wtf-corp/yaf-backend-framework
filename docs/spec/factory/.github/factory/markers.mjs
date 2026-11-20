// What an unattended run records on GitHub, and how it is read back (plan 0003, T6). Pure: no calls, no state.
// The markers are hidden comments on the ticket's issue, written by the orchestrator as the factory app:
//   <!-- factory:local-review <sha> ready|changes-required round=<n> -->   then the findings, in the same comment
//   <!-- factory:awaiting-push <sha> -->                                    the owner was told the push is theirs
//   <!-- factory:references-fix <sha> -->                                   a references fix was handed out
// round=<n> is the number of local fix rounds already used on the ticket when the verdict was given: 0 for the
// first local review. It is apart from `factory:rounds`, which post-review.mjs counts for the pull-request review.
// comments: issue comments as the REST API gives them ({ user: { login }, body }), oldest first.

const LOCAL_REVIEW = /<!-- factory:local-review (\S+) (ready|changes-required) round=(\d+) -->/;
const ROUNDS = /<!-- factory:rounds=(\d+) -->/;

/** A comment counts only when one of the named logins wrote it; with no login named, nothing counts. */
const writtenBy = (comment, logins) => logins.some((login) => login && comment.user?.login === login);

/** The marker's text. value: `ready round=0` or `changes-required round=1` for local-review, nothing otherwise. */
export function marker(kind, sha, value = '') {
  return `<!-- factory:${kind} ${sha}${value ? ` ${value}` : ''} -->`;
}

/** → the newest comment by `author` (the factory bot of the policy) with the marker of that kind for that commit, or null. */
export function findMarker(comments, kind, sha, author) {
  const start = `<!-- factory:${kind} ${sha} `; // the space ends the commit: a prefix of it is another commit
  return comments.findLast((c) => writtenBy(c, [author]) && c.body?.includes(start)) ?? null;
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
    if (m[1] === sha) Object.assign(result, { verdict: m[2], round, findings: c.body.slice(m.index + m[0].length).trim() });
  }
  return result;
}

/**
 * → { rounds, body } of the newest comment that carries the review's summary marker for that check and was written
 * by one of `authors` (the reviewer app or the gates bot), or null. The body goes verbatim to an implementer as
 * findings and the rounds number decides a fix round, so a summary written by anyone else does not count.
 */
export function reviewSummary(comments, check, authors) {
  const found = comments.findLast((c) => writtenBy(c, authors) && c.body?.includes(`<!-- factory:review-summary ${check} -->`));
  return found ? { rounds: Number(found.body.match(ROUNDS)?.[1] ?? 0), body: found.body } : null;
}
