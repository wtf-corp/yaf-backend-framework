// Pull-request size: changed lines of hand-written files (ADR-0029, brief F7).
import { matchesAny } from './lib.mjs';

/** numstat: [{ file, added, deleted }] → { lines, counted, excluded } */
export function countSize(numstat, policy) {
  const counted = [];
  const excluded = [];
  for (const f of numstat) (matchesAny(f.file, policy.size.exclude) ? excluded : counted).push(f);
  const lines = counted.reduce((n, f) => n + f.added + f.deleted, 0);
  return { lines, counted, excluded };
}

/**
 * acceptedByHuman: the `size-accepted` label is applied and was applied by a human maintainer.
 * → { pass, overSoft, overHard, title }
 */
export function sizeVerdict(lines, policy, acceptedByHuman = false) {
  const overSoft = lines > policy.size.soft;
  const overHard = lines > policy.size.hard;
  const pass = !overHard || acceptedByHuman;
  let title = `${lines} changed lines (soft ${policy.size.soft}, hard ${policy.size.hard})`;
  if (overHard) title += acceptedByHuman ? ' — accepted by a maintainer' : ' — over the hard limit: split it, or a maintainer applies size-accepted';
  else if (overSoft) title += ' — over the soft limit: the reviewer must say why it could not be split';
  return { pass, overSoft, overHard, title };
}

// CLI for people and local agents:  node .github/factory/size.mjs <base> [head=HEAD]
if (process.argv[1]?.endsWith('size.mjs')) {
  const { loadPolicy, git, parseNumstat } = await import('./lib.mjs');
  const [base, head = 'HEAD'] = process.argv.slice(2);
  if (!base) { console.error('usage: size.mjs <base> [head]'); process.exit(2); }
  const policy = loadPolicy();
  const size = countSize(parseNumstat(git('.', 'diff', '--numstat', '-M', `${base}...${head}`)), policy);
  console.log(sizeVerdict(size.lines, policy).title);
  for (const f of size.counted) console.log(`  ${f.file}: +${f.added} -${f.deleted}`);
}
