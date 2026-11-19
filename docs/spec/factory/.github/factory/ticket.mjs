// Check `factory/ticket`: a code pull request closes exactly one factory ticket whose plan is on the base branch.
import { existsSync } from 'node:fs';
import path from 'node:path';

/** → { closes: number[], plan: { file, key } | null, trivial: string | null } */
export function parseBody(body = '') {
  const text = body.replace(/<!--[\s\S]*?-->/g, '');
  const closes = [...text.matchAll(/^\s*(?:closes|fixes|resolves)\s+#(\d+)\s*$/gim)].map((m) => Number(m[1]));
  const p = text.match(/^\s*Plan:\s*`?(specs\/[\w.-]+\/plan\.md)(?:#(\w+))?`?\s*$/m);
  const t = text.match(/^\s*Trivial because:\s*(\S.*)$/m);
  return { closes: [...new Set(closes)], plan: p ? { file: p[1], key: p[2] ?? null } : null, trivial: t ? t[1].trim() : null };
}

/**
 * kind: 'spec' | 'plan' | 'code'; tier: computed tier; changedLines: size count;
 * issue: the GitHub issue the body closes (or null); planExistsOnBase: boolean; touchesPlan: code PR also edits a plan.
 * → { pass, title }
 */
export function ticketVerdict({ kind, tier, changedLines, parsed, issue, planExistsOnBase, touchesPlan }) {
  if (kind !== 'code') return { pass: true, title: `${kind} pull request: it is the approval request, no ticket needed` };
  if (touchesPlan) return { pass: false, title: 'a plan changes in its own pull request, not together with code' };
  if (parsed.trivial) {
    if (tier === 'high') return { pass: false, title: 'a change in tier high is never trivial' };
    if (changedLines > 20) return { pass: false, title: `trivial changes are at most about 20 lines; this one has ${changedLines}` };
    return { pass: true, title: `trivial: ${parsed.trivial}` };
  }
  if (parsed.closes.length !== 1) return { pass: false, title: `the description must close exactly one ticket ("Closes #n"); found ${parsed.closes.length}` };
  if (!issue) return { pass: false, title: `issue #${parsed.closes[0]} not found` };
  if (issue.pull_request) return { pass: false, title: `#${issue.number} is a pull request, not a ticket` };
  if (issue.state !== 'open') return { pass: false, title: `ticket #${issue.number} is not open` };
  const labels = (issue.labels ?? []).map((l) => (typeof l === 'string' ? l : l.name));
  if (!labels.includes('factory')) return { pass: false, title: `ticket #${issue.number} does not carry the factory label` };
  if (labels.includes('epic')) return { pass: false, title: `#${issue.number} is an epic; a pull request closes one ticket` };
  if (!parsed.plan) return { pass: false, title: 'the description needs a "Plan: specs/NNNN-slug/plan.md#Tn" line' };
  if (!planExistsOnBase) return { pass: false, title: `${parsed.plan.file} is not on the base branch: the plan must be merged first` };
  const marker = `factory:ticket ${path.posix.dirname(parsed.plan.file)}#${parsed.plan.key}`;
  if (parsed.plan.key && !(issue.body ?? '').includes(marker)) return { pass: false, title: `ticket #${issue.number} was not created from ${parsed.plan.file}#${parsed.plan.key}` };
  return { pass: true, title: `closes #${issue.number} (${parsed.plan.file}${parsed.plan.key ? '#' + parsed.plan.key : ''})` };
}

export function planOnBase(baseDir, planFile) {
  return existsSync(path.join(baseDir, planFile));
}
