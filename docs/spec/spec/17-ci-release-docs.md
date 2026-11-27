# 17 — CI, versioning, release and documentation (GitHub)

The destination repository is on GitHub. Workflow files below are outlines; action versions are pinned to commit SHAs in the real files (Dependabot keeps them current).

## 1. Branching and commits

- `main` is protected (the release app's `chore(release)` commit is the only bypass, §3): pull requests only, squash merge, linear history, the required checks of §2 and §2.2, code-owner review required for the paths in `CODEOWNERS` (§6), **no general approval count**, no force push, signed commits recommended. `next` (the branch on which breaking changes collect after 1.0.0, §3) has the same rules. Merges go through the merge queue, so each automatic merge is tested on top of the one before it.
- **Who has to approve depends on the risk tier** (ADR-0029, §2.2). A pull request in tier low or medium merges automatically once its checks are green. A pull request in tier high needs an approving review by a human maintainer who is not its author: on the paths in `CODEOWNERS` the branch rule demands it, elsewhere the check `factory/human-approval` does.
- **A human approval is a GitHub review, not a line in a file.** A spec is approved by a code owner's review on its own pull request; a plan merges after the plan reviewer's check, and needs a human maintainer's review when it is flagged (§2.2, ADR-0025). The `Status:` line inside `spec.md` and `plan.md` is informative only. An agent never submits an approving review and never applies a human label (`guard-change`, `major-release`, `size-accepted`); the reviewing agents report **checks**.
- Branch names: `<type>/<issue>-<slug>` for a ticket (`feat/123-typed-id-generator`, `fix/…`, `refactor/…`, `docs/…`, `chore/…`), `spec/NNNN-<slug>` for a spec or plan pull request. One ticket is one branch, one pull request and one squash commit.
- **References.** The pull request body has `Closes #<issue>` and `Plan: specs/NNNN-slug/plan.md#T3`; the squash commit message ends with `(#<pr>)` and carries `Refs: #<issue>`. Merging closes the ticket.
- **Conventional Commits**; the pull request title is the squash commit message and therefore the release note. `pr-title.yml` validates it.

| Type | Version effect | Example |
|---|---|---|
| `fix:` | patch | `fix(messaging): release claims on shutdown` |
| `feat:` | minor | `feat(http): idempotency key replay` |
| `feat!:` / `fix!:` / footer `BREAKING CHANGE:` | major; after 1.0.0 accepted only on a pull request to `next` (§3) | `feat(core)!: rename ErrorKind.Unauthorized` |
| `docs`, `test`, `refactor`, `chore`, `ci`, `build`, `perf` | none (`perf` → patch) | |

What counts as breaking: removed or changed public API (caught by package validation), a changed error code, wire name, configuration key, telemetry name, database schema that is not expand/contract, default that tightens behaviour for existing apps, a new required extension point, a dropped target framework (adding one is a `feat`).

## 2. Pull request workflow — `ci.yml`

```yaml
on: { pull_request: { branches: [main, next] }, push: { branches: [main, next] }, merge_group: {} }   # merge_group: the merge queue tests each automatic merge on top of the previous one
permissions: { contents: read }
concurrency: { group: ci-${{ github.ref }}, cancel-in-progress: true }
jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - checkout (fetch-depth: 0)
      - setup-dotnet (global.json)
      - dotnet restore YAF.slnx                                            # NU190x stay warnings here; they are listed in the job summary (§2.1)
      - dotnet build YAF.slnx -c Release --no-restore                      # warnings as errors, analyzers; every target framework
      - dotnet test --solution YAF.slnx -c Release --no-build              # Docker available; coverage + TRX; every target framework
      - check: "#if NET" appears only in files named *.NetNN.cs
      - coverage report + gate (Core, Domain, Application ≥ 85 %)
      - dotnet pack -c Release --no-build -o artifacts                     # package validation vs baseline
      - upload test results, coverage, packages
  generated-in-sync:      # configuration/telemetry/error/compliance references, OpenAPI documents, public API files
    steps: [ run generators, git diff --exit-code ]
  ef-model:               # dotnet ef migrations has-pending-model-changes for sample and template contexts
  templates:              # matrix http: [minimal, controllers] × transport: [rabbitmq (default), --aws (build only without the LocalStack token)]; instantiate, build, test
  sample-on-packages:     # build samples against the packed nupkgs from build-test
  docs:                   # docfx metadata + build --warningsAsErrors; wording lint: fails on "compliant with", "certified", "tamper-proof" in docs/ and XML docs outside the page that lists what must not be claimed
  transports:             # matrix: [inmemory, rabbitmq, awssqs-localstack]; the last runs only when the LocalStack token secret
                          # is present (never on forks) and is reported as skipped otherwise, not as passed
  security:
    steps: [ gitleaks, dependency-review-action (PRs; fails when the pull request itself adds or moves to a package version with a known advisory), CodeQL (csharp, actions), actionlint ]
  pr-title:               # conventional commit check (pull_request only); after 1.0.0 fails a breaking title on a pull
                          # request to main unless a human maintainer applied `major-release` (the merge of next, §3)
  process:                # spec and plan format (ADR-0024, 0025); AGENTS.md and skill validation (frontmatter, name = directory, size limits, links resolve);
                          # vendored third-party skills match .agents/skills.lock; agent definitions in .claude/agents parse
```

> **Amended 2026-11-24**, by the owner's decision of 2026-11-24 (`specs/0007-no-package-lock-files/plan.md`, decision log; ADR-0032): `build-test` restores without the locked restore; there are no NuGet lock files.

The process gates are not jobs of `ci.yml`: they are the factory workflows of §2.2, which run on the same pull requests and report their own required checks.

Rules: workflows declare least-privilege `permissions`; third-party actions pinned by SHA; no secrets in pull requests from forks; `pull_request_target` is not used.

### 2.1 Dependency advisories

A new advisory against a package the repository already uses is not caused by the pull request that happens to be open that day, so it does not fail that pull request. Three checks cover it instead:

| Check | When | Fails on | Effect |
|---|---|---|---|
| `dependency-review` in the `security` job | Every pull request | A package version the pull request adds or moves to has a known advisory (any severity) | The pull request cannot merge |
| `audit.yml` | Daily, and on demand | `dotnet restore -p:AuditPipeline=true` on `main` and on `next`: any advisory, direct or transitive (NU1901–NU1904 as errors) | Opens or updates one issue labelled `dependency-advisory` listing package, advisory and severity, and closes it when the restore is clean again. Maintainers are notified; no pull request is blocked |
| Audit step of the `release` job | Every release | An advisory of severity **high or critical** in what would be published | Nothing is published until the dependency is updated or the advisory is suppressed |

> **Amended 2026-11-24**, by the owner's decision of 2026-11-24 (`specs/0007-no-package-lock-files/plan.md`, decision log; ADR-0032): `audit.yml` restores without the locked restore.

A suppression is a `<NuGetAuditSuppress Include="<advisory URL>" />` entry in `Directory.Packages.props` with a comment giving the reason and an expiry date at most 30 days ahead. `Directory.Packages.props` is under CODEOWNERS, so a human approves it; a step in `build-test` fails when a suppression has expired or has no expiry. Low and moderate advisories do not block a release; the open `dependency-advisory` issue tracks them, and the release notes list every advisory open at release time.

Scheduled workflows: `audit.yml` (daily, above), `nightly.yml` (end-to-end, all conformance suites, template matrix on the latest SDK patch and, before a new .NET is added as a target, on its release candidates), `mutation.yml` (weekly), `dependabot.yml` (NuGet grouped by family, GitHub Actions, weekly; security updates immediately).

### 2.2 Factory gates (ADR-0029)

Authoring is local (an orchestrator session with role agents, implementers each in a worktree); gating is here. The gates treat every pull request alike, whoever wrote it. The policy is one file, `.github/factory/policy.json` (validated against `policy.schema.json`): the path globs of the sensitive list and of the size exclusions, `size.soft` (200) and `size.hard` (400), `limits.parallelImplementers` (3), `limits.fixRounds` (2), and the agent and model per role. The scripts are Node 22 modules without dependencies in `.github/factory/`.

| Workflow | Runs on | Does |
|---|---|---|
| `factory-gates.yml` | Pull request events, review events, label events | The deterministic checks below, by script |
| `factory-pr-review.yml` | Implementation pull requests | Runs the `pr-reviewer` agent with a fresh context on the ticket, the cited spec sections, the plan and the diff: the five passes of ADR-0027. Findings become review comments, the summary one comment kept up to date, the verdict the check `factory/review`. The review is never an approving GitHub review |
| `factory-doc-review.yml` | Spec and plan pull requests | Runs the spec reviewer or the plan reviewer against its quality checklist; reports `factory/doc-review` |
| `factory-automerge.yml` | After the gates | Enables auto-merge (squash) for tier low and medium when the factory is not paused. Never for tier high |
| `factory-tickets.yml` | A plan merged to `main` | Creates one issue per row of the plan's Tickets table, idempotently (marker `<!-- factory:ticket specs/NNNN-slug#T3 -->`), links them as sub-issues of the epic and by "blocked by", adds them to the Project in `Backlog` |
| `factory-status.yml` | Branch, pull request, merge and issue events | Moves the Project status: `Ready` when all blockers are closed, `In progress` when the ticket's branch exists, `In review` when its pull request opens, `Done` on merge |
| `factory-digest.yml` | Daily | Comments on the issue labelled `factory-digest`: pull requests merged automatically by tier, size distribution, fix rounds, tickets in `needs-human`, time in each status |

Required checks on `main` and `next` (names are exact):

| Check | Passes when |
|---|---|
| `factory/tier` | Always reports; `classify.mjs` computes the tier from the policy and the diff and sets the label `tier:low`, `tier:medium` or `tier:high`. A label may raise the tier; nothing lowers it below the computed floor. Fails only on a policy error |
| `factory/ticket` | The pull request closes exactly one open ticket whose plan is on the base branch; or it is a spec or plan pull request; or its body has a valid "Trivial because:" line (ADR-0023) |
| `factory/size` | Changed lines (added + deleted) of hand-written files are within `size.hard`, or `size-accepted` was applied by a human maintainer. Excluded by policy globs: generated code, lock files, `*.approved.*`, `PublicAPI.*.txt`, migration designer files and model snapshots, OpenAPI documents, `docs/api/**`. Over the hard limit the tier becomes high |
| `factory/delta` | Tier low: the approved files (public API, error codes, wire contracts, configuration keys, telemetry names) and the schema show no change. Tier medium: their change equals the `factory:delta` block of the merged plan. Tier high: reports only |
| `factory/guards` | No guard is weakened (new analyzer suppressions, `NoWarn` entries, skipped tests, removed `[Control]` tags, edited or deleted lines of `*.approved.*` files), or `guard-change` was applied by a human maintainer. Findings in a change to `.editorconfig` or `Directory.Packages.props` that the merged plan declares exactly, read from the plan on the base branch and outside `neverLowered`, are reported, not counted (ADR-0034) |
| `factory/review` | The pull-request reviewer's verdict is "ready", with no open Blocker or Major |
| `factory/doc-review` | Spec and plan pull requests: the reviewer's verdict is "pass" |
| `factory/plan-gate` | Plan pull requests: the plan is not flagged, or a human maintainer approved it. A plan is flagged when it declares a public API change, a schema change, a new dependency, a deviation from the spec, a ticket in tier high, or an `editorconfig` entry (ADR-0034) |
| `factory/human-approval` | The tier is low or medium, or there is an approving review by a human maintainer who is not the author |

What makes these controls rather than conventions:

- **The floor is deterministic.** The tier, the size and the delta come from scripts and the diff, not from an agent's opinion. The sensitive paths are always high, except a change to `.editorconfig` or `Directory.Packages.props` that the merged plan declares exactly, read from the plan on the base branch and outside `neverLowered`, which is medium (ADR-0034): authentication and authorization, execution context and tenancy, field protection and keys, audit, privacy, redaction, analyzers and generators, migrations, `.github/**`, `.claude/**`, `.agents/**`, `AGENTS.md` files, guard files, release configuration, `Directory.*.props` and `.targets`, `CODEOWNERS`, `specs/**/spec.md`, `specs/0000-framework/**`, `adr/**`. The exact globs are in the policy file.
- **The gates read the base branch.** The gate jobs take the policy, the scripts and the reviewer prompts from the base branch, never from the pull request; a pull request that changes any of them is tier high.
- **Label and review actors are checked.** `guard-change`, `major-release` and `size-accepted` count only when the label event's actor is a human member of the maintainers team; an approving review counts for `factory/human-approval` and `factory/plan-gate` only from such a member who is not the author. Both are read from the pull request timeline through the API; a bot or an app does not count.
- **Identities are separate.** Agents authenticate as the GitHub App `yaf-factory` (contents: write on non-protected branches; pull requests, issues, projects, checks), never as a human, so the human can review what the agents wrote. Its tokens cannot approve reviews, manage the three human labels, bypass branch rules, push to `main`, `next` or `stable`, or change settings; `.claude/settings.json` denies the same commands locally.
- **The CI reviewer holds little.** Its job has `contents: read`, `pull-requests: write`, `checks: write` and one secret, the model API key with a spending limit, in the environment `factory-review`. It has no publish or release credentials.
- **Pause.** The repository variable `FACTORY_PAUSED=true` stops ticket pickup and auto-merge; every check still runs.

Above `size.soft` the pull-request reviewer must state why the change could not be split, and reports a finding if it could. The numbers and limits are starting values; changing the policy is a tier-high pull request.

## 3. Automatic versioning

One version for all packages (they are built and tested together). SemVer 2.0.

**Mechanism: semantic-release, as the last job of the build pipeline.** Every push to `main` (and, after 1.0.0, to `next`) that passes the gates of `ci.yml` is released without a further manual step: semantic-release reads the Conventional Commits since the last tag, decides the bump, and — when there is one — versions, packs, publishes, tags and writes the changelog. A merge of only `docs`/`test`/`chore`/`ci`/`refactor`/`build` commits releases nothing.

The gate is therefore the **pull request merge**: the required checks, and for tier high a human maintainer's review (§2.2). A merge releases, whether a human or the auto-merge made it. The pull request shows what the merge will release: the `release-preview` job runs semantic-release in dry-run mode and writes the next version and the release notes to the job summary.

Configuration (`.releaserc.json`; semantic-release and its plugins are pinned in `package.json` with a committed lock file and installed with `npm ci`):

```json
{
  "branches": [ "stable", { "name": "main", "prerelease": "alpha" } ],
  "tagFormat": "v${version}",
  "plugins": [
    ["@semantic-release/commit-analyzer", { "preset": "conventionalcommits",
      "releaseRules": [ { "type": "perf", "release": "patch" } ] }],
    ["@semantic-release/release-notes-generator", { "preset": "conventionalcommits" }],
    "@semantic-release/changelog",
    ["@semantic-release/exec", {
      "verifyConditionsCmd": "./build/release/verify.sh",
      "prepareCmd": "./build/release/prepare.sh ${nextRelease.version} ${lastRelease.version}",
      "publishCmd": "./build/release/publish.sh ${nextRelease.version}" }],
    ["@semantic-release/git", {
      "assets": [ "CHANGELOG.md", "src/Directory.Build.props", "src/**/PublicAPI.Shipped.txt", "src/**/PublicAPI.Unshipped.txt" ],
      "message": "chore(release): ${nextRelease.version} [skip ci]\n\n${nextRelease.notes}" }],
    ["@semantic-release/github", { "assets": [ "artifacts/*.nupkg", "artifacts/*.snupkg", "artifacts/sbom.cdx.json" ] }]
  ]
}
```

| Script (`build/release/`) | Does |
|---|---|
| `verify.sh` | Fails early when the feed login or the app token is not available, so nothing is half-released |
| `prepare.sh <next> <last>` | Moves `PublicAPI.Unshipped.txt` entries to `Shipped`; `dotnet pack -c Release -p:Version=<next> -p:PackageValidationBaselineVersion=<last> -p:ContinuousIntegrationBuild=true -o artifacts`; then writes `PackageValidationBaselineVersion` = `<next>` into `src/Directory.Build.props` for the builds that follow. `<Version>` in source stays `0.0.0`: the released version exists only in the tag, the packages and the assemblies built by this job (package validation against the baseline, SPDX SBOM embedded); generates `artifacts/sbom.cdx.json` |
| `publish.sh <next>` | Creates the artifact attestations (`gh attestation` / the attest action's CLI equivalent) for `artifacts/*.nupkg` with the SBOM; pushes the packages with the short-lived feed key |

Order inside semantic-release: analyze → verify → generate notes → **prepare** (version, pack) → **publish** (attest, push) → commit `CHANGELOG.md`, the new validation baseline and the public API files back to `main` → tag `vX.Y.Z` → GitHub release with the packages and the SBOM attached.

- **The commit back to `main`** (`chore(release): X.Y.Z [skip ci]`) is pushed with a **GitHub App token**; the app is the only actor on the branch-protection bypass list, is installed on this repository only, and has `contents: write` and nothing else. `[skip ci]` stops the release commit from starting another pipeline.
- **Versions before 1.0.0**: `main` publishes prereleases only, `1.0.0-alpha.N` and later `1.0.0-beta.N` (a maintainer changes the `prerelease` identifier in a reviewed pull request); the README says so. Breaking changes are free and a `!` title is allowed on `main`. The configuration above is the one for this period: semantic-release needs one release branch, so `stable` is listed as the release branch (it need not receive commits) and `main` as a prerelease branch. Verify this arrangement against the semantic-release documentation when the release job is built, and record a deviation in the plan.
- **Cutting 1.0.0** is a deliberate step after the last milestone: a pull request, approved by a code owner, that changes `branches` to `[ "main", { "name": "next", "prerelease": "rc" } ]`. Its merge releases `1.0.0`.
- **Versions after 1.0.0**: `main` accepts `fix`, `perf` and `feat` only; the `pr-title` job fails a breaking-change title on a pull request to `main`. Breaking changes are merged to `next`, which publishes `N.0.0-rc.N`, never marked latest. At a time the maintainers plan, `next` is merged into `main` as one major: that pull request carries a breaking title and the `major-release` label, which counts only when a human maintainer applied it (checked like `guard-change`, §2.2); it is tier high. `next` is kept merged up from `main`.
- **A failed release is safe to re-run**: nothing is tagged until publish succeeded, the feed push uses `--skip-duplicate`, and a re-run of the job computes the same version.
- Alternatives are recorded in ADR-0020: a release pull request (release-please) adds a second human gate at the cost of a manual step and a release that is not part of the pipeline; tag-driven tools (MinVer, Nerdbank.GitVersioning) need no commit back but do not read commit types.

## 4. The release job in `ci.yml`

```yaml
  release-preview:          # pull_request only
    needs: build-test
    permissions: { contents: read }
    steps: [ checkout (fetch-depth: 0), setup-node, npm ci, npx semantic-release --dry-run --no-ci → job summary ]

  release:                  # push to main / next only
    if: github.event_name == 'push'
    needs: [build-test, generated-in-sync, ef-model, templates, sample-on-packages, docs, transports, security, instructions]
    runs-on: ubuntu-latest
    environment: release                       # scopes the app credentials; named in the NuGet trusted publishing policy
    concurrency: { group: release-${{ github.ref }}, cancel-in-progress: false }
    permissions: { contents: read, id-token: write, attestations: write }
    steps:
      - create GitHub App token (app id from vars, private key from the environment's secrets)
      - checkout with that token (fetch-depth: 0, persist-credentials for the push back)
      - setup-dotnet, setup-node, npm ci
      - dotnet restore -p:AuditPipeline=true -p:NuGetAuditLevel=high   # high and critical advisories stop the release (§2.1)
      - NuGet login via OIDC (trusted publishing) → short-lived key in the job environment
      - npx semantic-release                    # GITHUB_TOKEN = the app token

# .github/workflows/docs.yml
on: { release: { types: [published] } }         # the release commit carries [skip ci], so docs publish from the release event
jobs:
  publish-docs:
    permissions: { contents: write }            # or pages: write + id-token: write with the Pages actions
    steps: [ checkout at the tag, build DocFX, publish to gh-pages under vX.Y.Z/ and latest/, update the version switcher ]
```

> **Amended 2026-11-24**, by the owner's decision of 2026-11-24 (`specs/0007-no-package-lock-files/plan.md`, decision log; ADR-0032): the `release` job restores without the locked restore.

- The release job runs only after every gate of the pipeline is green for that exact commit; it does not rebuild from a different state.
- No long-lived feed key exists; the trusted publishing policy names this repository, `ci.yml` and the `release` environment. The feed key is requested immediately before semantic-release runs (it is short-lived).
- The `release` environment has no required reviewers (that would break "release is part of the pipeline"); it exists to scope secrets and the feed policy to pushes on `main` and `next` (environment branch rule).
- Consumers verify provenance with `gh attestation verify <file> -R <owner>/<repo>` against the artefact attached to the GitHub release (the feed re-signs packages, which changes their hash).
- Every assembly carries the version and, in its informational version, the commit SHA. `0.0.0` marks an unreleased local build.

## 5. Documentation site

`docs/` is a DocFX site: `articles/` (hand-written), `api/` (generated from XML docs), `compliance/` and `reference/` (generated, `11` §6, `10` §2, `12` §5).

Articles (one per building block, each ending with "Rules", "Configuration", "Testing it"): introduction, quick start, domain, results and errors, application pipeline, persistence, tenancy, data protection, audit trail, messaging, transports (one page each), jobs, cron, process managers, HTTP (one page per style + REST conventions + API versioning), authentication, authorization, security overview, privacy, compliance, observability, configuration, testing, templates, Aspire dev host, upgrading (per major version), contributing with agents.

Rules:

- Code in articles is **included from the sample or from compiled test files** (`[!code-csharp[](…#region)]`), never typed inline, except in the quick start, whose snippets are compiled by a docs test project.
- A pull request that changes public API (types, setup extensions, configuration keys, error codes, wire names, telemetry names) updates XML docs, the affected articles, the sample and the templates. The PR template has the checkbox; the reviewer checks it; the docs build catches dead links and moved files.
- Published per release under `/vX.Y.Z/` with `/latest/`; a version switcher lists releases.
- Diagrams are Mermaid in Markdown (state machines are generated from code).

## 6. Repository hygiene files

`README.md` (what, quick start, badges: build, coverage, release, docs), `LICENSE` (permissive), `SECURITY.md` (how to report, supported versions, disclosure timeline), `CONTRIBUTING.md` (points to `AGENTS.md` and the process ADRs — the same rules bind humans and agents), `CODEOWNERS` (the maintainers team owns the sensitive paths of the factory policy: `specs/**/spec.md`, `specs/0000-framework/`, `adr/`, `.github/`, `.claude/`, `.agents/` and every `AGENTS.md`, `.releaserc.json`, `package.json`, `build/release/`, `Directory.Build.props` and `Directory.Build.targets` at the root, `src/Directory.Build.props`, `nuget.config`, `global.json` (not `Directory.Packages.props`: an undeclared change to it is tier high, ADR-0034), `BannedSymbols.txt`, the architecture tests, `src/Analyzers/`, every `*.approved.*` and `PublicAPI.Shipped.txt` file, migrations, and the security-sensitive source paths: authentication and authorization, execution context and tenancy, field protection and keys, audit, privacy, redaction. `plan.md` is deliberately not code-owned: `factory/plan-gate` decides whether a plan needs a human; nor is `.editorconfig`: an undeclared change to it is tier high, ADR-0034), `.github/pull_request_template.md` (Why, What, `Closes #n`, `Plan:` link, Evidence, Impact checklist), issue templates (`ticket` and `epic` for the factory; bug, feature → becomes a spec, security → private advisory), `.github/factory/labels.json` with `setup-github.sh` (creates the labels and the `YAF factory` Project with its status field).

## 7. Acceptance criteria

- A dry run of the full chain on a fork: a pull request shows the previewed version in `release-preview`; merging a `feat:` pull request runs the pipeline and its `release` job publishes the next prerelease (`1.0.0-alpha.N`) — packages with SBOM and attestations (to a test feed), tag, GitHub release, changelog commit — and the docs appear under the version; merging a `docs:` pull request releases nothing; re-running a failed release job publishes once.
- On the same fork, with the configuration switched as for 1.0.0: a `feat:` merge to `main` publishes a minor version; a pull request to `main` with a breaking title fails `pr-title`; the same pull request to `next` passes and its merge publishes an `-rc.N` prerelease.
- The factory gates hold against the gated party (§2.2), each proven on the fork with the agents' own identity:
  - a docs-only pull request and a small fix outside the sensitive paths are classified low and merge with no human action once the checks are green; a pull request adding public API exactly as its merged plan declares is classified medium and merges the same way;
  - a pull request touching a sensitive path is classified high and stays unmerged until a human maintainer who is not its author approves; an approving review submitted with the `yaf-factory` identity, or by the author, leaves `factory/human-approval` failing;
  - the tier floor cannot be lowered: replacing `tier:high` by `tier:low` with the app token is reverted by the next run and the check still demands the human; raising a tier by label works;
  - a pull request over `size.hard` fails `factory/size` and becomes high; it passes after a human maintainer applies `size-accepted`, and not when the app applies it;
  - a pull request whose public API, error-code or schema change is not in the plan's `factory:delta` fails `factory/delta`;
  - a guard diff with a `guard-change` label applied by a bot or a non-maintainer fails `factory/guards`;
  - a pull request with no ticket, with a ticket whose plan is not on the base branch, or with two tickets fails `factory/ticket`, whatever the plan's `Status:` line says;
  - a pull request that edits `policy.json`, a gate script or a reviewer prompt is judged by the versions on the base branch and is classified high;
  - a flagged plan stays unmerged until a human maintainer approves; an unflagged plan merges after `factory/doc-review`; a spec never merges without a code owner's review;
  - with `FACTORY_PAUSED=true` no pull request gets auto-merge enabled, and the checks still report.
- Merging a plan creates its tickets once (a re-run creates none), with sub-issue and "blocked by" links; opening and merging a ticket's pull request moves it through `In review` to `Done`, and a ticket becomes `Ready` when its blockers close.
- A deliberate public API break without `!` fails `ci.yml` (package validation and public API analyzer).
- Advisories (§2.1): a pull request that adds a package version with a known advisory fails `security`; an unrelated pull request stays green while an advisory is open against an existing dependency; `audit.yml` opens the `dependency-advisory` issue for it and closes it after the update; the `release` job refuses to publish with a high advisory and publishes with a suppression that has not expired; an expired suppression fails `build-test`.
- A pull request from a fork runs without secrets and passes or reports skipped jobs.
