// Decides whether the pull request of a head commit is armed for automatic merge (squash), disarmed, or left alone.
// To be run by factory-automerge.yml after every gates run, from the default branch, with the factory app's token.
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
 * The result of `factory/plan-gate` on the head commit, or null when the gates have not reported it: the conclusion
 * of the newest check run reported by the gates' app, and the state of the newest commit status the gates bot created.
 * With both, the one that has not passed decides, as GitHub requires both to pass. A check run or status of that
 * name from anybody else is ignored. gatesBot: `bots.gates` of the policy.
 */
export function planGateConclusion(checkRuns, gatesBot, statuses = []) {
  const slug = gatesBot.replace(/\[bot\]$/, '');
  const newest = (list, at) => list.reduce((a, b) => (!a || (at(a) === at(b) ? b.id > a.id : at(b) > at(a)) ? b : a), null); // the same time: the highest id
  const run = newest(checkRuns.filter((c) => c.name === 'factory/plan-gate' && c.app?.slug === slug), (c) => c.started_at ?? '');
  const status = newest(statuses.filter((s) => s.context === 'factory/plan-gate' && s.creator?.login === gatesBot), (s) => s.created_at ?? '');
  const results = [...(run ? [run.conclusion ?? null] : []), ...(status ? [status.state] : [])];
  if (results.length === 0) return null;
  return results.every((r) => r === 'success') ? 'success' : results.find((r) => r !== 'success');
}

/**
 * The command part. gh: (...args) => stdout. → one { number, action, reason } per open pull request with that head.
 * A pull request whose calls fail gets the action 'failed' and does not stop the others.
 */
export function automerge({ repo, headSha, paused, gatesConclusion, policy, gh, log = console.log }) {
  const get = (endpoint) => JSON.parse(gh('api', endpoint));
  // Every pull request whose head is the commit the gates ran on. Read through the API, never from the event's free text.
  const found = JSON.parse(gh('api', `repos/${repo}/commits/${headSha}/pulls`, '--paginate', '--slurp')).flat()
    .filter((p) => p.state === 'open' && p.head.sha === headSha);
  if (found.length === 0) log(`no open pull request for ${headSha}`);
  return found.map((f) => {
    let decision;
    try {
      decision = decideOne({ repo, headSha, paused, gatesConclusion, policy, gh }, get(`repos/${repo}/pulls/${f.number}`));
    } catch (e) {
      decision = { action: 'failed', reason: e.message };
    }
    log(`#${f.number}: ${decision.action} (${decision.reason})`);
    return { number: f.number, ...decision };
  });
}

function decideOne({ repo, headSha, paused, gatesConclusion, policy, gh }, pr) {
  // A push since the list was read: the reviews and checks of headSha say nothing about this head. Its own gates run decides.
  if (pr.state !== 'open' || pr.head.sha !== headSha) return none(`the head is no longer ${headSha}`);
  const list = (endpoint) => JSON.parse(gh('api', endpoint, '--paginate', '--slurp')).flat();
  const reviews = list(`repos/${repo}/pulls/${pr.number}/reviews?per_page=100`);
  const files = list(`repos/${repo}/pulls/${pr.number}/files?per_page=100`).map((f) => f.filename);
  const kind = prKind(files, policy);
  const planGate = kind === 'plan'
    ? planGateConclusion(list(`repos/${repo}/commits/${headSha}/check-runs?check_name=factory%2Fplan-gate&filter=all&per_page=100`).flatMap((p) => p.check_runs), policy.bots.gates,
      list(`repos/${repo}/commits/${headSha}/statuses?per_page=100`))
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
  return decision;
}

if (process.argv[1]?.endsWith('automerge.mjs')) {
  const headSha = process.env.HEAD_SHA;
  if (!headSha) throw new Error('HEAD_SHA is not set');
  const decisions = automerge({
    repo: repoSlug(),
    headSha,
    paused: process.env.FACTORY_PAUSED === 'true',
    gatesConclusion: process.env.GATES_CONCLUSION,
    policy: loadPolicy(),
    gh: (...args) => run('gh', args),
  });
  if (decisions.some((d) => d.action === 'failed')) process.exitCode = 1;
}
