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
