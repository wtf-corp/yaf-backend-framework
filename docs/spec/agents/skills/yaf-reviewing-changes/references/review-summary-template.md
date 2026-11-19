<!-- factory:review-summary -->
<!-- The pull-request reviewer keeps exactly one comment with the marker above and edits it on every run. Findings themselves are review comments on the lines. -->

## Review of #<pr> — ticket #<issue> (<plan path>#T<n>)

**Reviewer** <agent and model> · **Run** yyyy-MM-dd HH:mm UTC · **Diff** <base sha>..<head sha>
**Inputs** spec (or the cited sections of specs/0000-framework), plan.md as on the base branch, the ticket, the diff

**Verdict** ready | changes required
**Blockers** n · **Major** n · **Minor** n · **Nits** n · **Size** n changed hand-written lines · **Tier** low | medium | high (raised by reviewer: no / yes — why)

<!-- Two or three sentences: what the change does and the state it is in. -->

### Pass results

<!-- In order. A later pass is "not started" while an earlier one has an open Blocker. -->

| Pass | Result | Notes |
|---|---|---|
| 1 Spec conformance | pass / fail | |
| 2 Correctness | pass / fail / not started | |
| 3 Security and privacy | pass / fail / not applicable — why | |
| 4 Architecture and public API | pass / fail / not started | |
| 5 Simplicity and size | pass / fail / not started | |

### Requirement coverage

<!-- Every requirement of the ticket (initial build: every acceptance item it cites, e.g. "05 §11 item 3"). "Verified" says how you confirmed the test fails without the change. -->

| Requirement | Scenario | Test | Verified |
|---|---|---|---|
| R1 | S1 | `Namespace.Class.Test_name` | reverted lines 40–52 of X.cs → test fails |

### Plan and ticket conformance

<!-- Files outside the ticket's `touches`, work of another ticket, delta not in the plan's factory:delta block, plan changed in this pull request. "none" if none. -->

### Size

<!-- Only above the soft limit: why the change could not be split, or the finding that says it could. -->

### Findings

<!-- One line each, linking the review comment. Severity: Blocker | Major | Minor | Nit. -->

| # | Severity | Title | Location | State |
|---|---|---|---|---|
| F1 | | | `path/to/file.cs:123` | open / fixed in <commit> / shown wrong / accepted by <maintainer> |

### Questions

<!-- Suspicions you could not verify, and things the spec, plan or ticket left unclear. -->

- …

### Commands run

| Command | Exit | Result |
|---|---|---|
| `dotnet build YAF.slnx -c Release` | | |
| `dotnet test YAF.slnx` | | passed / failed / skipped |

<!-- Say plainly what could not be run and why. -->
