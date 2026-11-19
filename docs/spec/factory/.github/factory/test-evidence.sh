#!/usr/bin/env bash
# Report-only evidence that the tests a pull request adds or changes fail without its code change.
# Restores every non-test file the pull request changed to its base version (files it added are
# removed), runs the test projects that contain changed tests, and expects them to FAIL.
# A build error counts as a failure: the tests cannot pass without the change.
# Always exits 0. Runs the pull request's code: no secrets, read-only token.
set -uo pipefail

base="${1:?base commit}"
summary="${GITHUB_STEP_SUMMARY:-/dev/stdout}"

mapfile -t changed < <(git diff --name-only --diff-filter=ACMR "$base"...HEAD)
tests=(); code=()
for f in "${changed[@]}"; do
  case "$f" in
    tests/*) tests+=("$f") ;;
    src/*)   code+=("$f") ;;
  esac
done

{
  echo "### factory/test-evidence (report only)"
  echo
} >> "$summary"

if [ "${#tests[@]}" -eq 0 ]; then echo "No test file changed: nothing to show." >> "$summary"; exit 0; fi
if [ "${#code[@]}" -eq 0 ]; then echo "Only tests changed: there is no code change to take away." >> "$summary"; exit 0; fi

# The test projects that own the changed test files.
declare -A projects=()
for f in "${tests[@]}"; do
  d=$(dirname "$f")
  while [ "$d" != "." ] && [ "$d" != "/" ]; do
    p=$(find "$d" -maxdepth 1 -name '*.csproj' | head -n 1)
    if [ -n "$p" ]; then projects["$p"]=1; break; fi
    d=$(dirname "$d")
  done
done

# Take the code change away.
for f in "${code[@]}"; do
  if git cat-file -e "$base:$f" 2>/dev/null; then git checkout -q "$base" -- "$f"; else rm -f "$f"; fi
done

{
  echo "Code restored to the base version: ${#code[@]} file(s). Test projects run: ${#projects[@]}."
  echo
  echo "| Test project | Result without the change | Meaning |"
  echo "|---|---|---|"
} >> "$summary"

for p in "${!projects[@]}"; do
  if dotnet test "$p" -c Release > "evidence.log" 2>&1; then
    echo "| \`$p\` | passed | **No evidence**: the changed tests pass without the change |" >> "$summary"
  else
    if grep -q "error CS" evidence.log; then why="does not build"; else why="tests fail"; fi
    echo "| \`$p\` | $why | The tests depend on the change |" >> "$summary"
  fi
done
exit 0
