import test from 'node:test';
import assert from 'node:assert/strict';
import { loadPolicy, parseNumstat } from '../lib.mjs';
import { countSize, sizeVerdict } from '../size.mjs';

const policy = loadPolicy();

test('generated, lock, approved and public API files are not counted', () => {
  const numstat = parseNumstat([
    '120\t10\tsrc/Core/Result.cs',
    '50\t0\ttests/Core.Tests/ResultTests.cs',
    '900\t0\tpackage-lock.json',
    '40\t0\tsrc/Core/PublicAPI.Unshipped.txt',
    '12\t0\ttests/Core.Tests/errors.approved.txt',
    '700\t0\tsrc/Persistence.EfCore/Migrations/001_Init.Designer.cs',
    '300\t20\tsrc/Persistence.EfCore/Migrations/AppDbContextModelSnapshot.cs',
    '-\t-\tdocs/images/a.png',
  ].join('\n'));
  const size = countSize(numstat, policy);
  assert.equal(size.lines, 180);
  assert.equal(size.excluded.length, 5);
});

test('a NuGet lock file is counted', () => {
  const size = countSize(parseNumstat('900\t0\tsrc/Core/packages.lock.json'), policy);
  assert.equal(size.lines, 900);
  assert.deepEqual(size.excluded, []);
});

test('renames are attributed to the new path', () => {
  const [a, b] = parseNumstat('3\t1\tsrc/{Old => New}/A.cs\n2\t2\tsrc/a.cs => src/b.cs');
  assert.equal(a.file, 'src/New/A.cs');
  assert.equal(b.file, 'src/b.cs');
});

test('soft and hard limits', () => {
  assert.deepEqual(pick(sizeVerdict(policy.size.soft, policy)), { pass: true, overSoft: false, overHard: false });
  assert.deepEqual(pick(sizeVerdict(policy.size.soft + 1, policy)), { pass: true, overSoft: true, overHard: false });
  assert.deepEqual(pick(sizeVerdict(policy.size.hard + 1, policy)), { pass: false, overSoft: true, overHard: true });
});

test('over the hard limit passes only when a human accepted it, and is still over the limit', () => {
  const v = sizeVerdict(policy.size.hard + 1, policy, true);
  assert.equal(v.pass, true);
  assert.equal(v.overHard, true); // the classifier still makes it tier high
});

function pick({ pass, overSoft, overHard }) {
  return { pass, overSoft, overHard };
}
