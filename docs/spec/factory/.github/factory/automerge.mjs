// Decides whether the pull request of a head commit is armed for automatic merge (squash), disarmed, or left alone.
// Run by factory-automerge.yml after every gates run, from the default branch, with the factory app's token.
//   HEAD_SHA=<sha> FACTORY_PAUSED=<true|false> GATES_CONCLUSION=<conclusion> node .github/factory/automerge.mjs
import { loadPolicy, repoSlug, run, makeHumanCheck } from './lib.mjs';
import { prKind, TIERS } from './classify.mjs';
import { approvingHuman } from './human-approval.mjs';

const none = (reason) => ({ action: 'none', reason });

/**
 * approver: the login `approvingHuman` found on the head commit, or null. planGate: the conclusion of the gates'
 * `factory/plan-gate` on the head commit. armed: automatic merge is enabled. → { action: 'arm' | 'disarm' | 'none', reason }
 */
export function armDecision({ labels, draft, kind, approver, planGate, armed, paused, gatesConclusion }) {
  const tier = TIERS.findLast((t) => labels.includes(`tier:${t}`)) ?? null; // the highest label decides
  const needsHuman = labels.includes('needs-human');
  if (draft) return none('a draft');
  if (!tier && !needsHuman) return none('no tier label yet');

  let held = null;
  if (needsHuman) held = 'needs-human: a question is open';
  else if (tier === 'high' && !approver) held = 'tier high: no approving review by a human maintainer on the head commit';
  else if (tier === 'high' && kind === 'plan' && planGate !== 'success') held = 'tier high plan: factory/plan-gate has not passed on the head commit';
  // Taking automatic merge off needs neither a running factory nor green gates.
  if (held) return { action: armed ? 'disarm' : 'none', reason: held };

  const why = tier === 'high' ? `tier high, approved by ${approver}` : `tier ${tier}`;
  if (armed) return none(`${why}: already armed`);
  if (paused) return none(`${why}: not armed while the factory is paused`);
  if (gatesConclusion !== 'success') return none(`${why}: not armed, the gates run concluded ${gatesConclusion}`);
  return { action: 'arm', reason: why };
}

/**
 * The conclusion of the newest `factory/plan-gate` check run reported by the gates' app, or null.
 * A check of that name from any other app is ignored. gatesBot: `bots.gates` of the policy.
 */
export function planGateConclusion(checkRuns, gatesBot) {
  const slug = gatesBot.replace(/\[bot\]$/, '');
  const mine = checkRuns.filter((c) => c.name === 'factory/plan-gate' && c.app?.slug === slug);
  if (mine.length === 0) return null;
  return mine.reduce((a, b) => ((b.started_at ?? '') >= (a.started_at ?? '') ? b : a)).conclusion ?? null;
}

/** The command part. gh: (...args) => stdout. → the decision, or null when no open pull request has that head. */
export function automerge({ repo, headSha, paused, gatesConclusion, policy, gh, log = console.log }) {
  const get = (endpoint) => JSON.parse(gh('api', endpoint));
  const list = (endpoint) => JSON.parse(gh('api', endpoint, '--paginate', '--slurp')).flat();

  // The pull request whose head is the commit the gates ran on. Read through the API, never from the event's free text.
  const found = list(`repos/${repo}/commits/${headSha}/pulls`).find((p) => p.state === 'open' && p.head.sha === headSha);
  if (!found) {
    log(`no open pull request for ${headSha}`);
    return null;
  }
  const pr = get(`repos/${repo}/pulls/${found.number}`);
  const reviews = list(`repos/${repo}/pulls/${pr.number}/reviews?per_page=100`);
  const files = list(`repos/${repo}/pulls/${pr.number}/files?per_page=100`).map((f) => f.filename);
  const kind = prKind(files, policy);
  const planGate = kind === 'plan'
    ? planGateConclusion(list(`repos/${repo}/commits/${headSha}/check-runs?check_name=factory%2Fplan-gate&filter=all&per_page=100`).flatMap((p) => p.check_runs), policy.bots.gates)
    : null;

  const decision = armDecision({
    labels: pr.labels.map((l) => l.name),
    draft: pr.draft,
    kind,
    approver: approvingHuman(reviews, { author: pr.user.login, headSha, isHuman: makeHumanCheck(policy) }),
    planGate,
    armed: Boolean(pr.auto_merge),
    paused,
    gatesConclusion,
  });
  // Arming names the commit the decision was made on: a push in between is not armed by it.
  if (decision.action === 'arm') gh('pr', 'merge', String(pr.number), '--repo', repo, '--auto', '--squash', '--match-head-commit', headSha);
  if (decision.action === 'disarm') gh('pr', 'merge', String(pr.number), '--repo', repo, '--disable-auto');
  log(`#${pr.number}: ${decision.action} (${decision.reason})`);
  return decision;
}

if (process.argv[1]?.endsWith('automerge.mjs')) {
  const headSha = process.env.HEAD_SHA;
  if (!headSha) throw new Error('HEAD_SHA is not set');
  automerge({
    repo: repoSlug(),
    headSha,
    paused: process.env.FACTORY_PAUSED === 'true',
    gatesConclusion: process.env.GATES_CONCLUSION,
    policy: loadPolicy(),
    gh: (...args) => run('gh', args),
  });
}
