// Tells a review workflow what kind of pull request it looks at and which model the policy assigns.
//   node .github/factory/scope.mjs --pr-dir pr --role prReviewer
// Outputs (GITHUB_OUTPUT): kind = spec | plan | code, agent, model, paused
import { loadPolicy, git, readEvent, parseNumstat, setOutput } from './lib.mjs';
import { prKind } from './classify.mjs';

const args = process.argv.slice(2);
const arg = (name, fallback) => (args.includes(name) ? args[args.indexOf(name) + 1] : fallback);
const policy = loadPolicy();
const event = readEvent();
const prDir = arg('--pr-dir', 'pr');
const role = arg('--role', 'prReviewer');

const range = `${event.pull_request.base.sha}...${event.pull_request.head.sha}`;
const files = parseNumstat(git(prDir, 'diff', '--numstat', '-M', range)).map((f) => f.file);
const kind = prKind(files, policy);
// --role docReviewer means: the spec reviewer for a spec pull request, the plan reviewer for a plan pull request.
const roleName = role !== 'docReviewer' ? role : kind === 'spec' ? 'specReviewer' : kind === 'plan' ? 'planReviewer' : 'prReviewer';
const r = policy.roles[roleName];
if (!r) throw new Error(`the policy has no role "${roleName}"`);

setOutput('kind', kind);
setOutput('agent', r.agent);
setOutput('model', r.model);
setOutput('fix_rounds', policy.limits.fixRounds);
console.log(`kind=${kind} agent=${r.agent} model=${r.model}`);
