---
name: yaf-commits-and-pull-requests
description: Conventional Commits, branch names with the ticket number, the ticket references (Closes, Plan, Refs) and the pull request description for YAF, how commit types drive the released version, and how tier labels and automatic merge work. Load BEFORE writing a commit message, naming a branch, or opening or updating a pull request; WHEN choosing between feat, fix, refactor and the other types; WHEN a change is or might be breaking; and WHEN asked to merge, approve, label, tag, release, edit the changelog or the version (agents do none of those).
license: MIT
metadata:
  owner: yaf
  version: "1"
---

# Commits and pull requests

Pull requests are squash-merged, so **the pull request title becomes the commit on `main`, and that commit decides the next version and its release note**. One ticket is one pull request is one squash commit, and that commit names its ticket. Decision records: ADR-0020 (versioning and release), ADR-0023 (process), ADR-0026 (evidence), ADR-0029 (tickets, tiers, automatic merge).

## When to load

- Before a commit, a branch, or a pull request.
- When deciding whether a change is breaking.
- When someone asks you to merge, approve, label, tag or release.

## Rules

1. **REQUIRED** — Commit messages and pull request titles follow Conventional Commits: `type(scope)!: summary`.
2. **REQUIRED** — The type reflects the effect on users of the packages, because it drives the version.
3. **REQUIRED** — A breaking change has `!` after the type or scope **and** a `BREAKING CHANGE:` footer saying what breaks and how to adapt.
4. **REQUIRED** — The pull request description has all six sections of the template, with real evidence.
5. **REQUIRED** — A pull request closes **exactly one** open ticket whose plan is on the base branch: the body has `Closes #<issue>` and `Plan: specs/NNNN-<slug>/plan.md#T<n>`. Check `factory/ticket` fails otherwise. The exceptions are a spec or plan pull request, and a trivial one, which says `Trivial because: …`.
6. **REQUIRED** — Agents never: merge by hand (`gh pr merge`, the merge button, the API); approve a pull request; apply `guard-change`, `major-release` or `size-accepted`; lower a `tier:*` label; push to `main`, `next` or `stable`; tag; publish packages; edit `CHANGELOG.md`, the version or the release configuration; force-push a shared branch; rewrite published history.
7. **REQUIRED** — After `1.0.0`, a pull request to `main` is `fix`, `feat` or a non-releasing type. A breaking change targets `next`; the title check rejects `!` or a `BREAKING CHANGE:` footer on a pull request to `main`. The one exception is the planned merge of `next` into `main`, which a human maintainer marks with the `major-release` label.
8. **REQUIRED** — A spec and a plan each travel in a pull request of their own, titled `docs(specs): …`, containing nothing but the files under `specs/` (and an ADR the spec needs).
9. **REQUIRED** — No secrets, personal data or internal hostnames in messages, titles, descriptions, issues or comments.
10. **REQUIRED** — Tier labels (`tier:low`, `tier:medium`, `tier:high`) are set by the classifier from the policy file and the diff. Do not set them. You may ask for a **higher** tier; nothing lowers the computed one. A guard file change that the merged plan on the base branch declares exactly (`.editorconfig`, `Directory.Packages.props`, ADR-0034) is tier medium, with the reason "guard change declared by the plan", and needs no `guard-change` label; any other reason for tier high keeps it high.
11. **REQUIRED** — Merging is a workflow's job or a human's: `factory-automerge.yml` enables auto-merge (squash) for tier low and medium once the required checks are green; a tier-high pull request waits for a human maintainer's approving review. Green checks are not your cue to merge.
12. **REQUIRED** — Small pull requests: one ticket each. By default, above 200 changed hand-written lines (`size.soft`) the reviewer asks why it was not split; above 400 (`size.hard`) check `factory/size` fails and a human decides. This repository uses 400 and 1000 during the initial build, until `1.0.0`. Generated code, lock files, approved files, public API files, migration designer files and snapshots do not count.
13. **REQUIRED** — Commits made by agents are authored by the `yaf-factory` app identity, never by the human owner's account: a human cannot approve a pull request that carries their own name as author.
14. **RECOMMENDED** — Commit after each green step; one concern per commit; each commit message carries the trailer `Refs: #<issue>`.

## Types and the version

| Type | Use for | Version bump |
|---|---|---|
| `feat` | New behaviour or public API a user can use | minor |
| `fix` | Corrects behaviour that was wrong | patch |
| `perf` | Faster or lighter, same behaviour | patch |
| `refactor` | Restructuring, same behaviour, same public API | none |
| `docs` | Articles, XML docs, ADRs, specs, `AGENTS.md`, skills | none |
| `test` | Tests only | none |
| `build` | Build system, package references, SDK | none |
| `ci` | Workflows and CI configuration | none |
| `chore` | Housekeeping that fits nothing above | none |
| any type with `!` | A breaking change | **major** |

What the bump turns into depends on the branch and on whether `1.0.0` exists:

| When | Branch | What a releasable merge publishes |
|---|---|---|
| Until `1.0.0` (the initial build) | `main` | A prerelease only: `1.0.0-alpha.N`, later `1.0.0-beta.N`. Breaking changes are free here; still mark them with `!` and the footer so the notes say so |
| Cutting `1.0.0` | — | A human-approved pull request that switches the release configuration. Not an agent's task |
| After `1.0.0` | `main` | `fix`/`perf` → patch, `feat` → minor. No breaking changes |
| After `1.0.0` | `next` | Release candidates of the next major, `N.0.0-rc.N`; breaking changes collect here and are merged to `main` as one major at a time the maintainers plan |

Choosing:

- A user gets something new → `feat`. A user's bug goes away → `fix`. Nothing changes for a user → one of the others.
- A dependency bump that fixes a vulnerability users are exposed to is `fix`. A routine bump is `build`.
- A change that needs a release and is typed `refactor` or `chore` will not be released. Type it by its effect.

**Scope** is the package or area, lower-case: `core`, `domain`, `application`, `infrastructure`, `persistence`, `http`, `messaging`, `messaging-rabbitmq`, `messaging-awssqs`, `privacy`, `testing`, `specs`, `analyzers`, `templates`, `sample`, `docs`, `adr`, `agents`, `release`.

**Summary**: imperative, lower-case start, no full stop, at most about 70 characters, says what changes for the reader of the changelog.

```
feat(messaging): park outbox rows after the attempt limit
fix(persistence): keep the tenant filter when soft-deleted rows are included
refactor(application)!: rename IRequestMiddleware.InvokeAsync to HandleAsync

BREAKING CHANGE: custom middleware must rename InvokeAsync to HandleAsync. No behaviour changes.
```

## Ticket references

| Where | What | Example |
|---|---|---|
| Branch | `<type>/<issue>-<slug>` | `feat/123-typed-id-generator` |
| Commit on the branch | Conventional Commit + trailer | `feat(core): generate typed id converters` … `Refs: #123` |
| Pull request title | The Conventional Commit that becomes the squash commit | `feat(core): generate typed id converters` |
| Pull request body | Closing keyword and plan row | `Closes #123` · `Plan: specs/0003-typed-ids/plan.md#T3` |
| Squash commit on `main` | Title ends with the pull request number; body keeps the trailer | `feat(core): generate typed id converters (#131)` … `Refs: #123` |

Merging the pull request closes the ticket and a workflow moves it to `Done`. Statuses on the Project (`Backlog` → `Ready` → `In progress` → `In review` → `Done`) are changed by the orchestrator and the workflows, not by editing labels by hand.

## What is breaking

Any of these makes an app built on the previous release change code, configuration or data:

- a public type or member removed, renamed, or changed in signature or in documented behaviour;
- a new required extension point or required configuration key;
- an error code changed or removed;
- a wire contract changed incompatibly (an integration event, the envelope, an HTTP contract);
- a migration that is not compatible with the previous release's code;
- a default that becomes stricter in a way that refuses what worked before.

If you think a change might be breaking and the spec does not say so, **stop and ask** before opening the pull request. After `1.0.0`, a change that is breaking is based on and targets `next`.

## Branches

- `spec/NNNN-<slug>` for the pull request that carries a spec, and again for the one that carries its plan — `spec/0042-outbox-parking`. Title `docs(specs): 0042 outbox parking — spec` / `… — plan`.
- `<type>/<issue>-<slug>` for a ticket — `feat/123-typed-id-generator`, where `123` is the issue number. Cut it from a `main` that contains the merged plan, in a worktree of its own. A ticket that depends on an unmerged one may be **stacked**, one level deep: its branch is cut from the head of that ticket's branch, and its own pull request targets `main` and is opened only after the dependency has merged and the branch is rebased onto `main` (`git rebase --onto origin/main <the dependency's head>`). `Stacked on` names the dependency's pull request.
- `<type>/<slug>` for trivial work. Types: `feat`, `fix`, `refactor`, `docs`, `test`, `chore`.
- Long-lived branches belong to the maintainers and the pipeline: `main`, `next` (after `1.0.0`), and `stable` (listed in the release configuration as the release branch until `1.0.0` is cut).

## Pull request description

```markdown
## Why

The problem, in two or three sentences.

## What

What changes, by package. What deliberately does not.

## Ticket

Closes #<issue>
Plan: specs/NNNN-<slug>/plan.md#T<n>
Spec: specs/NNNN-<slug>/spec.md (initial build: the sections of specs/0000-framework the plan cites)
Stacked on: #<pr> / —
<!-- or, with no ticket: Trivial because: <reason — and that it has no public API, security, privacy or schema impact> -->

## Evidence

| Command | Exit | Result |
|---|---|---|
| `dotnet build YAF.slnx -c Release` | 0 | 0 warnings |
| `dotnet test YAF.slnx` | 0 | n passed, 0 failed, n skipped (unchanged from main) |

Size: n changed hand-written lines. Could not run: <what and why, or "nothing">

## Impact checklist

- [ ] Public API: none / described; XML docs, articles, sample, templates updated
- [ ] Security (ADR-0004 tenets): none / S…
- [ ] Privacy (classification, retention, erasure): none / described
- [ ] Wire contracts and error codes: none / described
- [ ] Migration: none / expand-contract described
- [ ] Approved files changed: none / each one explained below, and every addition is in the plan's `factory:delta` block
- [ ] Sensitive paths touched: none / listed (the classifier decides the tier)
- [ ] Guards weakened: none / guard change declared by the plan / guard file changed — explained, `guard-change` label requested from a maintainer
- [ ] Breaking: no / yes — title has `!`, footer explains; after `1.0.0` the pull request targets `next`

## Reviewer notes

Where to start reading. What is generated. Deviations from the plan (also commented on the ticket). Open questions.
```

## Steps

1. Check the branch name against the ticket number.
2. Make sure the definition of done holds (`yaf-implementing-tickets`) and the local code review returned `ready` (`yaf-reviewing-changes`).
3. Choose type and scope by effect on users; decide breaking or not.
4. Write the title as the changelog line you want users to read.
5. Fill the description: `Closes #<issue>`, the `Plan:` line, real evidence.
6. Open the pull request (the orchestrator does this, as the `yaf-factory` identity). The gates then run on their own: `factory/tier`, `factory/ticket`, `factory/size`, `factory/delta`, `factory/guards`, `factory/review`.
7. Push fixes as new commits while under review; do not force-push over review comments.
8. Leave the merge alone. Tier low and medium merge automatically when the checks are green; tier high waits for a human maintainer. The merge is the release: the pipeline's release job versions and publishes it (a prerelease until `1.0.0`). State the expected version effect in the description and check the `release-preview` job summary.

## Anti-patterns

| Wrong | Why | Instead |
|---|---|---|
| `chore: add retry to the relay` | New behaviour typed as housekeeping ships in no release and no changelog | `feat(messaging): …` |
| `feat: updates` | The release note says nothing | Say what a user gets |
| Breaking rename under `refactor:` without `!` | A major change goes out as nothing | `refactor(scope)!:` + footer |
| Editing `CHANGELOG.md` or the version by hand | The release automation owns them; edits conflict or are overwritten | Let the pipeline's release job do it |
| Evidence: "tests pass" | A claim | Command, exit code, counts |
| One pull request with a feature, a refactoring and a dependency bump | Cannot be reviewed, typed or reverted cleanly | Three pull requests |
| Force-pushing after review started | Reviewers lose their place | Add commits; the merge squashes |
| Merging because checks are green (`gh pr merge`) | The workflow merges tier low and medium; a human merges tier high | Leave it |
| A pull request that closes two tickets, or none | `factory/ticket` fails; two concerns cannot be reviewed or reverted apart | One ticket, one pull request |
| Setting `tier:low` on your pull request, or removing `tier:high` | The classifier recomputes the floor and the check fails; it looks like an attempt to pass a gate | Leave tier labels to the classifier |
| Opening the pull request under the human owner's account | They can then not approve it | The `yaf-factory` identity |
| Squeezing a 600-line change into one pull request with `size-accepted` in mind | The label is a human's decision, not a plan | Split the ticket; update the plan |
| `feat(http)!: …` opened against `main` after `1.0.0` | One merge would publish a new major of every package | Target `next`; majors are batched |
| Spec, plan and code in one pull request | The plan is gated together with the code it was meant to gate; `factory/ticket` fails | Separate pull requests, in order |
| Applying `guard-change` or `size-accepted` to your own pull request | The label must come from a human maintainer; CI checks the actor | Explain and ask |

## Done when

- [ ] Title is a valid Conventional Commit that reads well as a changelog line.
- [ ] Breaking changes carry `!` and a `BREAKING CHANGE:` footer, and after `1.0.0` target `next`.
- [ ] Branch is `<type>/<issue>-<slug>`; the body has `Closes #<issue>` and the `Plan:` line; commits carry `Refs: #<issue>`.
- [ ] The pull request closes exactly one ticket, and its plan is on the base branch.
- [ ] Description has Why, What, Ticket, Evidence, Impact checklist, Reviewer notes.
- [ ] No merge, approval, human label, tier change, tag, version or changelog edit was made by the agent.
