// Check `factory/human-approval`: tier high needs an approving review by a human maintainer
// who is not the author, given on the current head commit.

/**
 * reviews: GitHub pull request reviews, oldest first. isHuman: (user) => boolean.
 * → the approving human's login, or null.
 */
export function approvingHuman(reviews, { author, headSha, isHuman }) {
  const latest = new Map();
  for (const r of reviews) {
    if (!r.user || r.state === 'COMMENTED' || r.state === 'PENDING') continue;
    latest.set(r.user.login, r);
  }
  for (const r of latest.values()) {
    if (r.state !== 'APPROVED') continue;
    if (r.user.login === author) continue;
    if (r.commit_id !== headSha) continue; // an approval of an older commit does not count
    if (!isHuman(r.user)) continue;
    return r.user.login;
  }
  return null;
}

/**
 * The tier the approval check judges by. A plan in tier low or medium is decided by
 * factory/plan-gate; a plan in tier high needs the approval like any other tier-high pull request.
 */
export function approvalTier(kind, tier) {
  if (kind === 'plan') return tier === 'high' ? 'high' : 'low';
  return tier;
}

export function humanApprovalVerdict(tier, approver) {
  if (tier !== 'high') return { pass: true, title: `tier ${tier}: no human approval required` };
  return approver
    ? { pass: true, title: `approved by ${approver}` }
    : { pass: false, title: 'tier high: waiting for an approving review by a human maintainer on the current commit' };
}

/**
 * Whom to request a review from → { reviewers: logins, teams: slugs }. needed: a human must approve the pull
 * request. approver: approvingHuman's result. requestedUsers, requestedTeams: the logins and team slugs whose
 * review is requested now. humans: the policy's `humans`. Nobody once a human has approved, and nobody while a
 * team's review is requested (GitHub's own request to the code owners). Never the author, anyone already
 * requested, or anyone who has submitted a review of the head commit. With no logins in the policy the team is
 * asked instead, unless somebody other than the author or a bot has reviewed the head commit: who is on the team
 * is not known here.
 */
export function reviewersToRequest({ needed, approver, author, requestedUsers, requestedTeams, reviews, headSha, humans }) {
  if (!needed || approver || requestedTeams.length > 0) return { reviewers: [], teams: [] };
  const onHead = reviews.filter((r) => r.user && r.state !== 'PENDING' && r.commit_id === headSha);
  const reviewed = onHead.map((r) => r.user.login);
  if (humans.logins.length === 0) {
    const byOthers = onHead.some((r) => r.user.login !== author && r.user.type !== 'Bot');
    return { reviewers: [], teams: byOthers ? [] : [humans.team] };
  }
  const reviewers = [...new Set(humans.logins)].filter((login) => login !== author && !requestedUsers.includes(login) && !reviewed.includes(login));
  return { reviewers, teams: [] };
}
