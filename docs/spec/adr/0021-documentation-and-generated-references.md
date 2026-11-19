# ADR-0021 — Documentation and generated references

> **Status** Accepted · **Date** 2026-11 · **Deciders** maintainers

## Context

A framework is used through its documentation and its sample. Documentation written by hand next to fast-changing code rots; documentation that agents read and trust must be true. Consumers read the version they pin, not the latest.

## Drivers

- The site matches the code of each released version.
- Reference material (settings, telemetry names, error codes, controls) cannot drift.
- Code shown in articles compiles and is tested.
- A public API change is not complete until it is documented.
- Humans and agents read the same sources.

## Options considered

| Option | For | Against |
|---|---|---|
| README and wiki | Low effort | Unversioned, detached from code, no API reference |
| Hand-written site only | Flexible | Reference tables drift; snippets rot |
| Everything generated from XML docs | Never stale | No concepts, no guidance |
| **Static site with concept articles + API reference from XML docs + generated reference pages + included sample code** | Each kind of content has one source | A docs build in CI and generators to maintain |

## Decision

1. **Site.** A DocFX site under `docs/`, built in CI with warnings as errors, published to GitHub Pages per release under `vX.Y.Z/` and `latest/`, with a version switcher.
2. **Articles** (`docs/articles/`): introduction, quick start, one article per building block (domain, results and errors, application pipeline, persistence, messaging, jobs and cron, process managers, HTTP, security, privacy and compliance, observability, configuration, testing, templates, upgrading). Each states what it is for, how to set it up, the rules, and how to test it. A package planned for a later release (ADR-0006) gets its article in the pull request that adds it, not before.
3. **API reference** from XML doc comments. In `src`, a public member without a comment or a documented member missing a parameter is a build error. Comments say what the member is for, what the caller must do first and what failure looks like; entry points carry an `<example>`.
4. **Same-pull-request rule.** A change that adds, removes, renames or alters the behaviour of a public type or member, a setup extension, an extension point, a configuration key, an error code or a wire name updates the XML docs, the affected articles and the sample in the same pull request. A change with no public effect says so in the pull request.
5. **Generated references**, produced by the `yaf-compliance` tool and analyzers, committed, and checked for drift in CI:

   | File | Source |
   |---|---|
   | `configuration-reference.md` | Options classes |
   | `telemetry-reference.md` | `TelemetryCatalog` |
   | `error-catalogue.md` | `IErrorCatalog` declarations |
   | `compliance/data-inventory.md` | Classification, retention and erasure attributes, including every member stored in clear with its stated reason |
   | `compliance/controls.md` | `[Control]`-tagged tests and their last result |
   | `compliance/authorization-inventory.md` | Endpoint metadata and request markers |
   | `compliance/unsafe-options.md` | `UnsafeOptionsRegistry` |

6. **Code by reference.** Articles include code from the sample and from compiled example tests by file and region, never pasted. A moved or renamed file breaks the docs build.
7. **The sample is documentation.** Its README maps every framework concept to the files that show it; each bounded context keeps a ubiquitous-language file and its own service-level ADRs.
8. **Honest wording.** The compliance page opens with what the framework does not claim (ADR-0005).
9. **Agent-facing docs** (`AGENTS.md` files, skills) link to these sources instead of restating them (ADR-0028).

## Consequences

- Reviewers see documentation and reference diffs next to code diffs.
- The docs build adds minutes to CI and fails on broken links and includes; it cannot detect a sentence that became untrue, which remains review's job.
- Generators are part of the product and need tests.
- Old versions stay published; a tag from before a page existed lacks it.

## Enforcement

- Build: CS1591 and CS1573 as errors in `src`.
- CI: docs build with warnings as errors on pull requests and `main`; drift checks for every generated file; publish on release.
- Pull request template checkbox for the same-pull-request rule; review checklist item (ADR-0027).
- Tests for the generators (given types → expected Markdown).
- Link checker for external links on a schedule, non-blocking.

## Guidance for agents

- Treat docs as part of the change: update XML docs, the article and the sample together, or state "no public API change".
- Never hand-edit a generated file; change the source and run the generator.
- Do not paste code into articles; include it from the sample or an example test.
- Write for someone who has not read the code: purpose, setup, rules, failure modes. No marketing words, no claims of compliance.
- When you rename a public type, fix its cross-references and run the docs build locally.
- If you find documentation that contradicts the code, report it and fix it in the same change when it is in scope.

## Related

0001, 0005, 0006, 0008, 0016, 0017, 0018, 0020, 0027, 0028
