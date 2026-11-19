# ADR-0020 — Versioning and release automation

> **Status** Accepted · **Date** 2026-11 · **Deciders** maintainers

## Context

YAF ships as a set of NuGet packages from a GitHub repository. Consumers pin versions, clients match on error codes and wire names, and much of the code is written by agents. Version numbers must therefore follow from what changed, and releases must be reproducible and attributable. The maintainers want releasing to be **part of the build pipeline**: a merge that passes the pipeline is released by it, with no separate manual release step.

## Drivers

- Versions computed from history, not typed by hand.
- Release is the last stage of the same pipeline that built and tested the commit.
- One gate, in the place review already happens: the pull request merge. Whether a person or the checks pass that gate depends on the risk tier of the change (ADR-0029).
- Breaking changes detected mechanically as well as declared.
- Supply-chain evidence per release: SBOM and provenance; no long-lived publishing secrets.
- Simple for consumers: one version across all YAF packages.

## Options considered

| Option | For | Against |
|---|---|---|
| **semantic-release in the pipeline** | Fully automatic on merge; version, changelog, tag, GitHub release and publish in one job that depends on the pipeline's gates; dry run on pull requests shows what a merge will release; rich plugins | Commits the changelog back to a protected branch, so a narrowly scoped app token must be allowed to bypass protection; Node toolchain in a .NET repository; every releasable merge is a release, so majors need a rule of their own (point 10) |
| The same, with breaking merges to `main` releasing a major each time | No extra rule | A new major of every lockstep package per breaking merge; version numbers lose meaning and users face frequent majors |
| `0.x` versions until stable | Familiar convention | semantic-release has no `0.x` mode; needs custom release rules that are removed again at 1.0.0 |
| Any dependency advisory fails every restore in CI | One simple rule; nothing vulnerable is ever built | A new advisory blocks every open pull request and every release, whoever caused it and whatever its severity |
| release-please | A release pull request is a second human gate; nothing is pushed to `main` outside pull requests | A manual step per release; release is not part of the build pipeline; no .NET release type |
| MinVer / Nerdbank.GitVersioning | No commit back; simple (tag or height based) | No commit analysis: a person decides the bump and pushes the tag |
| GitVersion | Branch-strategy aware | Heavy configuration; implicit rules |

## Decision

1. **SemVer, one lockstep version** for every YAF package; packages are built, tested and released together.
2. **Conventional Commits.** Pull requests are squash-merged; the pull request title becomes the commit and is checked. `fix` and `perf` → patch, `feat` → minor, `!` or a `BREAKING CHANGE:` footer → major; `docs`, `test`, `ci`, `chore`, `refactor`, `build` release nothing.
3. **semantic-release runs as the `release` job of `ci.yml`** on pushes to `main` (and `next`), after every other job of the pipeline succeeded for that commit. When the commits since the last tag warrant a release it: computes the version; packs with that version and package validation against the previous one; generates the SBOM; creates artifact attestations; pushes the packages to nuget.org through trusted publishing (OIDC, short-lived key); commits `CHANGELOG.md`, the new package-validation baseline and the shipped public API files as `chore(release): X.Y.Z [skip ci]`; tags `vX.Y.Z`; creates the GitHub release with packages and SBOM attached. Configuration and scripts are specified in `spec/17-ci-release-docs.md` §3–4.
4. **The pull request is the gate.** A `release-preview` job runs semantic-release in dry-run mode on every pull request and reports the version and notes the merge would produce. Whatever lets the pull request merge lets the release happen: for tier high a human maintainer's approval, for tier low and medium the required checks alone (ADR-0029). A release can therefore follow a merge that no person clicked; before 1.0.0 that is a prerelease, after it a patch or a minor, never a major (point 10).
5. **The commit back to `main`** is pushed with a GitHub App token. The app is installed on this repository only, holds `contents: write` only, and is the single entry on the branch-protection bypass list. Its credentials live in the `release` environment, which is restricted to `main` and `next`.
6. `<Version>` in source stays `0.0.0`; the released version exists in the tag, the packages and the assemblies built by the release job. A local or pull-request build is therefore always recognisable as unreleased.
7. **Docs** for a version are published by `docs.yml` on the GitHub release event (the release commit carries `[skip ci]`).
8. **What is breaking**: removing or changing a public type or member; a new required extension point; changing an error code, kind or integration event wire name; changing a configuration key or telemetry name; changing a default that weakens or tightens behaviour visibly; raising the minimum target framework. Package validation and the approved public API, error catalogue and contracts files detect most of these; a detected break without a `!` title fails CI.
9. **Before 1.0.0, `main` publishes prereleases only**: `1.0.0-alpha.N`, later `1.0.0-beta.N`, to the same feed, never marked latest. Breaking changes are free in that period and a `!` title is allowed on `main`. `1.0.0` is cut after the initial build is complete, by a human-approved pull request that switches the release configuration so that `main` becomes the release branch. semantic-release needs one release branch at all times: until the switch the configuration lists `stable` as the release branch and `main` as a prerelease branch (to be verified against its documentation when the release job is built).
10. **After 1.0.0, majors are batched.** `main` accepts `fix`, `perf` and `feat` only: the title check rejects a breaking-change title (`!` or a `BREAKING CHANGE:` footer) on a pull request to `main`. Breaking changes collect on the `next` branch, which publishes `N.0.0-rc.N`; `next` is merged into `main` as one major at a time the maintainers plan, and that merge is the only pull request to `main` allowed a breaking title (a maintainer applies the `major-release` label, checked like `guard-change`, ADR-0023). Adding a target framework is a `feat`; dropping one is breaking (ADR-0003).
11. **Dependency advisories are checked on a schedule, not on every pull request.** A pull request fails only when it adds or moves to a package version with a known advisory (dependency review). A daily `audit.yml` restores `main` and `next` with advisories as errors and keeps one `dependency-advisory` issue open while any exists. The `release` job refuses to publish with a high or critical advisory unless a time-boxed, code-owner-approved suppression is in `Directory.Packages.props`.

### GitHub Actions outline

```yaml
# .github/workflows/ci.yml
on: { pull_request: { branches: [main, next] }, push: { branches: [main, next] }, merge_group: {} }
permissions: { contents: read }
jobs:
  pr-title:        # Conventional Commits check of the pull request title; after 1.0.0 no breaking title on a pull request to main
  build-test:      # dotnet restore --locked-mode ; build -c Release ; test ; pack (package validation) ; no expired advisory suppression
  generated-in-sync: # references, OpenAPI documents, public API files, approved files
  docs:            # docfx metadata + build, warnings as errors
  security:        # gitleaks ; CodeQL ; dependency review (pull requests)
    permissions: { contents: read, security-events: write }
  # the factory's checks run in their own workflows (ADR-0029): factory-gates.yml (factory/tier, ticket, size, delta, guards,
  # plan-gate, human-approval), factory-pr-review.yml (factory/review), factory-doc-review.yml (factory/doc-review)
  process:         # spec and plan format, agent definitions and skills valid (ADR-0024, 0025, 0028)
  release-preview: # pull_request: npx semantic-release --dry-run → job summary
    needs: build-test
  release:         # push only
    if: github.event_name == 'push'
    needs: [build-test, generated-in-sync, docs, security]      # plus the matrix jobs listed in spec/17
    environment: release
    concurrency: { group: release-${{ github.ref }}, cancel-in-progress: false }
    permissions: { contents: read, id-token: write, attestations: write }
    steps:
      # GitHub App token → checkout with it (fetch-depth: 0)
      # setup-dotnet ; setup-node ; npm ci ; dotnet restore --locked-mode
      - uses: NuGet/login@<pinned>            # OIDC → short-lived key
        id: login
        with: { user: ${{ vars.NUGET_USER }} }
      - run: npx semantic-release
        env:
          GITHUB_TOKEN: ${{ steps.app.outputs.token }}
          NUGET_API_KEY: ${{ steps.login.outputs.NUGET_API_KEY }}

# .github/workflows/docs.yml
on: { release: { types: [published] } }      # build DocFX at the tag, publish under vX.Y.Z/ and latest/
```

Actions are pinned by commit SHA; semantic-release and its plugins are pinned in `package.json` with a committed lock file and installed with `npm ci`. Verification for consumers: `gh attestation verify <file> -R <owner>/<repo>` against the artefact attached to the GitHub release (the feed re-signs packages, which changes their hash).

### Branch rules on `main` and `next`

- Pull request required; **zero general approvals**; code-owner review required on `CODEOWNERS` paths (the sensitive paths of ADR-0029). The human requirement for other tier-high changes comes from the check `factory/human-approval`.
- Required checks: `pr-title`, `build-test`, `generated-in-sync`, `docs`, `security`, `process`, and the factory's `factory/tier`, `factory/ticket`, `factory/size`, `factory/delta`, `factory/guards`, `factory/review`, `factory/doc-review`, `factory/plan-gate`, `factory/human-approval`.
- Linear history (squash only); no force pushes or deletions; conversation resolution; administrators included; bypass list: the release app only. The factory's app (`yaf-factory`) is not on it.
- **Merge queue** where the hosting plan allows it: automatic merges enter the queue, and each is built and tested on top of the one before, so two pull requests that are green apart cannot break `main` together. The `release` job runs on what the queue lands.
- Automatic merge is enabled per pull request by `factory-automerge.yml` for tier low and medium, and not while `FACTORY_PAUSED` is set.

## Consequences

- Releasing needs no action: a merged, releasable pull request is published by the pipeline that verified it. Nobody pushes tags or edits versions.
- Merging is releasing. Review of a `feat`/`fix` pull request is review of a release; the preview job makes that visible.
- Most merges are automatic, so most releases follow a merge no person approved. What a person approved is the specification and, where flagged, the plan; the tier rules keep security, contracts, guards and the release configuration itself behind a human (ADR-0029). The daily digest lists what was merged and released that way, and `FACTORY_PAUSED` stops it.
- Releases are frequent and small. Work that should not ship yet stays behind a feature flag.
- Until 1.0.0 consumers get prereleases only and must expect breaking changes between them.
- After 1.0.0 a breaking change waits on `next` for the next planned major; `next` must be kept merged up from `main`, and a fix needed on both is made on `main` first.
- A security fix that cannot be made without a break is the one case for an unplanned major; it goes through `next` like any other.
- One bot may push one kind of commit to `main`. That exception is narrow (one app, one permission, one environment) and visible in the branch rules.
- The changelog is only as good as pull request titles; the title check and review carry that.
- Trusted publishing ties publishing to `ci.yml` and the `release` environment; renaming either needs a policy update on the feed.
- An advisory against an existing dependency is known within a day rather than at the next pull request, and low or moderate ones can ship in a release while their issue is open; in exchange an advisory nobody in the pull request caused no longer blocks unrelated work.
- A Node toolchain is needed in CI only; nothing in the product depends on it.
- Lockstep versioning releases unchanged packages too, which is accepted for simplicity.

## Enforcement

- The branch rules above, with the factory's checks required (ADR-0029); the factory's app has no bypass.
- PR title check, which after 1.0.0 also fails a breaking title on a pull request to `main` unless a job confirms a human maintainer applied `major-release`; squash-only merges; protected `main` and `next`; `release` environment restricted to `main` and `next`.
- Dependency review on pull requests; `audit.yml` daily with the `dependency-advisory` issue; the audit step of the `release` job; the expiry check on `NuGetAuditSuppress` entries.
- The `release` job `needs` every gate, so an unverified commit cannot be released.
- Package validation with baseline; public API analyzer files; approval files for errors and contracts.
- Workflow permissions are minimal per job; OIDC instead of stored feed keys; the app token is created only in the release job.
- CODEOWNERS on `.github/workflows`, `.releaserc.json`, `package.json`, `build/release/`, `Directory.Build.props`, `Directory.Packages.props`.

## Guidance for agents

- Title every pull request as a Conventional Commit that matches the real change: the title decides whether and how the merge is released. Use `!` and a `BREAKING CHANGE:` footer when any rule in point 8 applies. After 1.0.0 such a pull request targets `next`, never `main`; do not split or reword a breaking change to make it pass as a `feat`.
- Read the `release-preview` summary on your pull request and state the expected version effect in the description ("releases a minor version" / "releases nothing").
- Never push or move tags, never edit `CHANGELOG.md`, the version or the validation baseline, never run semantic-release outside dry-run mode, never use or request the release app's credentials.
- Never change `.releaserc.json`, `build/release/` or the `release` job without an approved spec and a code-owner review. Cutting `1.0.0` and merging `next` into `main` are maintainers' actions; never open or prepare them on your own initiative.
- Do not add `[skip ci]` to a commit or pull request title; it is reserved for the release commit.
- Never bypass or weaken a required check, and never add secrets to workflows; ask a maintainer.
- Never merge by hand or enable automatic merge yourself; `factory-automerge.yml` does that when the tier allows it. A pull request that merges automatically is released automatically, so the title and the evidence must be right before the checks go green, not after.
- If package validation or an approval file reports a break you did not intend, fix the code, not the baseline.
- Pin any action you add by commit SHA and give its job the least permissions it needs.

## Related

0001, 0003, 0005, 0006, 0008, 0018, 0021, 0023, 0026, 0027, 0029
