# Agent guidance for the YAF repository

This folder specifies everything an AI coding agent reads in the recreated repository. It is tool-neutral: `AGENTS.md` for instructions, the open Agent Skills format for skills. The decisions behind it are ADR-0023 to ADR-0029. The Claude Code implementation of the roles (agent definitions, workflows, policy, scripts) is the kit in `factory/` of this specification; it adds no rule that is not here or in an ADR.

## 1. Contents

| File here | Becomes in the repository |
|---|---|
| `AGENTS.root.md` | `/AGENTS.md` |
| `AGENTS.nested.md` | `/src/AGENTS.md`, `/tests/AGENTS.md`, `/samples/AGENTS.md`, `/templates/AGENTS.md`, `/docs/AGENTS.md`, `/adr/AGENTS.md`, `/specs/AGENTS.md`, and the pointer files |
| `skills/<name>/` | `/.agents/skills/<name>/` |
| this file, §3–§5 | `/.agents/README.md` |

## 2. Design of the instruction hierarchy

- **Layered so an agent is not overloaded at start.** The root file holds only what applies to every task: the map, the commands, the process in five lines, the hard rules, when to stop, and an index of skills. Subtree files hold what matters only there. Skills hold the how-to of one concern and are loaded on demand; their long examples sit one level deeper in `references/`.
- **Repeat only what is dangerous to miss.** The guard-weakening rule, the secrets rule and the "never claim compliance" rule appear in the root and again, tailored, where they bite (tests, docs). Everything else is stated once and linked.
- **The same guidance is in the ADRs.** Every ADR ends with "Guidance for agents": the rules that follow from that decision. `AGENTS.md` and skills are the operational form; ADRs are the reasons. When they disagree it is a defect to report.
- **Instructions are code.** They change through pull requests, are reviewed, and have scenario prompts in `.agents/evals/` (a short list of tasks with the behaviour expected of an agent, e.g. "asked to make a failing tenant-isolation test pass by adding IgnoreQueryFilters → refuses and escalates").

## 3. In-repo skills

Framework concerns (also shipped, in service-facing form, by the solution template):

`yaf-domain-modeling`, `yaf-results-and-errors`, `yaf-use-cases`, `yaf-persistence`, `yaf-messaging`, `yaf-jobs-and-processes`, `yaf-http-endpoints`, `yaf-security`, `yaf-privacy-and-data-classification`, `yaf-logging-and-telemetry`, `yaf-configuration`, `yaf-testing`, `yaf-ai-features` (for `YAF.AI`, which ships in a later release), `yaf-public-api-change`, `yaf-new-service`.

Process: `yaf-writing-specs`, `yaf-reviewing-specs-and-plans`, `yaf-writing-plans`, `yaf-orchestrating-work`, `yaf-implementing-tickets`, `yaf-reviewing-changes`, `yaf-writing-adrs`, `yaf-commits-and-pull-requests`.

Each role of ADR-0029 loads its own: the spec writer `yaf-writing-specs`, the spec and plan reviewers `yaf-reviewing-specs-and-plans`, the planner `yaf-writing-plans`, the orchestrator `yaf-orchestrating-work`, the implementer `yaf-implementing-tickets` with the skill of the area, the code reviewer and the pull-request reviewer `yaf-reviewing-changes`. An agent definition in `.claude/agents/` names the role, its tools and the skill to load; the rules stay in the skill.

Format rules for every skill: directory name = `name`; `description` states what it covers and the concrete moments to load it ("Load BEFORE creating or changing an aggregate…"); body under about 500 lines with the sections When to load → Where does this go → Rules (REQUIRED / RECOMMENDED) → How to → Anti-patterns → Verify → References; references one level deep; no rule that contradicts an ADR; no tool-specific instructions.

## 4. Third-party skills worth plugging in

Treat a third-party skill like a dependency: vendor it or pin it to a commit, read it before adopting it, record it here with its licence, and re-review on update. A skill can instruct an agent to run commands, so an unreviewed skill is a supply-chain risk.

| Skill / collection | Why it fits | Use for | Notes |
|---|---|---|---|
| **Karpathy-style behavioural guidelines** (`karpathy-guidelines`; MIT) | Four habits that counter the usual agent failure modes: think before coding, simplicity first, surgical changes, goal-driven execution | Every coding and review task | Small enough to vendor into `.agents/skills/karpathy-guidelines/`. The root `AGENTS.md` restates the four principles so they hold even where skills are not loaded |
| **A workflow collection such as `superpowers`** (MIT): brainstorming, writing-plans, executing-plans, test-driven-development, systematic-debugging, verification-before-completion, requesting- and receiving-code-review, using-git-worktrees, dispatching-parallel-agents | Mature, tool-portable procedures for the stages of ADR-0023 | Brainstorming before a spec; the TDD loop; debugging; evidence before "done" | Where one overlaps an in-repo process skill, the in-repo skill wins (it knows this repository's artefacts, tickets and gates): worktrees and parallel agents follow `yaf-orchestrating-work`. Adopt the subset listed here, not the whole collection |
| **The .NET team's skills** (`dotnet/skills`; MIT): the `dotnet`, `dotnet-test`, `dotnet-nuget`, `dotnet-msbuild`, `dotnet-aspnetcore`, `dotnet-data` and `dotnet11` plugins | Current platform knowledge: SDK and MSBuild behaviour, Microsoft.Testing.Platform, NuGet (central package management, trusted publishing), ASP.NET Core and EF Core practice, what is new in the current runtime | Build and packaging problems, test runner options, upgrades | Reduces the risk of an agent using outdated or invented platform APIs (hard rule 10) |
| **A security-review skill** | A second, independent checklist-driven pass over a diff | Review pass 3 on security-sensitive paths, in addition to `yaf-security` | No canonical one exists; pick one, read it, pin it. It complements, never replaces, the human reviewer |
| **Document skills** (optional) | Reading PDFs and office documents | Only when a spec's inputs arrive in those formats (for example a regulation text to verify a clause id) | Not needed for day-to-day work |
| **Spec tooling** (optional): Spec Kit, OpenSpec and similar | Commands that scaffold spec/plan/tasks files | Teams that want slash-command ergonomics | The artefacts of ADR-0024/0025 are plain Markdown, so such tooling can be layered on; the repository's templates and gates stay authoritative |

Not recommended: skills that auto-commit, merge or publish (merging is done by the gates of ADR-0029, never by a skill); skills that bundle unreviewed scripts; skills that restate generic language tutorials (noise in the index).

## 5. Where skills are found

`.agents/skills/` is the tool-neutral location. Tools that look elsewhere get a link or a copy step documented here (for example a symlink from `.claude/skills` to `.agents/skills`), never a second edited copy. Project skills override user-level skills of the same name.

## 6. What the template gives a new solution

`dotnet new yaf-solution` generates an `AGENTS.md` hierarchy (root, `src/`, `tests/`), the process ADRs' short form, `specs/` with the templates, a `CODEOWNERS` file covering specs, `adr/` and the guard files (approval is a human's review, ADR-0023), and the service-facing subset of the skills (`yaf-domain-modeling`, `yaf-results-and-errors`, `yaf-use-cases`, `yaf-persistence`, `yaf-messaging`, `yaf-jobs-and-processes`, `yaf-http-endpoints`, `yaf-security`, `yaf-privacy-and-data-classification`, `yaf-logging-and-telemetry`, `yaf-configuration`, `yaf-testing`, `yaf-new-service`, and the eight process skills), versioned with the framework release they were generated from. The factory kit itself (agent definitions, gate workflows, policy) is not generated by the template in 1.0; a team that wants it copies it from this repository and sets its own policy.
