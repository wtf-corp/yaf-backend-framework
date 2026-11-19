# ADR-0026 — Coding rules for agents and humans

> **Status** Accepted · **Date** 2026-11 · **Deciders** maintainers

## Context

With a plan merged to `main` and its tickets created (ADR-0025, ADR-0029), implementation is where a change either keeps the framework's guarantees or quietly erodes them. The rules here apply to whoever writes the code, agent or person. They are few on purpose: style is the formatter's and the analyzers' job.

## Drivers

- "Done" must mean verified, not believed.
- The framework's guards exist to catch mistakes; a guard that is switched off to get a green build protects nothing.
- Diffs must be small enough to review properly: one ticket, one pull request, within the size limits.
- An agent's knowledge of third-party APIs may be out of date or wrong.

## Options considered

1. **Rely on review.** Catches problems late and inconsistently.
2. **A long coding standard.** Unread, and mostly duplicates what tooling enforces.
3. **A short set of behavioural rules plus a definition of done, with everything mechanical left to tooling.**

## Decision

Option 3.

### The loop

One ticket at a time: one worktree, one branch `<type>/<issue>-<slug>`, one pull request (ADR-0029). Within the ticket:

1. Write the test the ticket names.
2. Run it. See it **fail for the expected reason**. A test that passes at once, or fails for another reason, proves nothing yet — fix the test first.
3. Implement the minimum that makes it pass.
4. Run it green.
5. Run the verification command the ticket names, then the wider suite.
6. Post the evidence as a comment on the ticket.

If the ticket turns out to need more than its size estimate or to touch files outside its declared `touches`, stop and say so on the ticket; do not grow the pull request.

### Rules

1. **Surgical diffs.** Every changed line traces to the ticket. No drive-by refactoring, renaming or reformatting.
2. **No speculative abstractions.** No interface with one implementation unless the spec requires pluggability; no option nobody asked for; no handling of cases that cannot happen.
3. **Never weaken a guard to get green.** Stop and ask instead. Weakening a guard means any of:
   - suppressing or downgrading an analyzer diagnostic (`#pragma`, `NoWarn`, `.editorconfig` severity, `[SuppressMessage]`);
   - skipping, deleting or commenting out a test;
   - loosening an assertion;
   - widening a permission, a role grant or a query filter;
   - turning on an unsafe option (`Allow…`, `Disable…`) or acknowledging one;
   - deleting an `*.approved.*` file, or a lock file, to make a check pass;
   - adding an entry to a banned-API, secret-scan or architecture-test allow-list;
   - lowering a coverage, audit or validation threshold;
   - applying `guard-change` or `size-accepted` yourself, or editing a status line, `CODEOWNERS`, the factory policy or a workflow so that a gate passes;
   - arranging a change so that it is classified in a lower tier than its content deserves (ADR-0029).

   When a guard really has to change, a human maintainer applies the `guard-change` label to the pull request and a code owner reviews the guard file. An agent proposes the change and explains it; it does not authorise it.
4. **Approved files change deliberately.** Updating an `*.approved.*` file (error catalogue, authorization inventory, public API, pipeline order, OpenAPI document, contract schema) is a decision; the pull request explains each change.
5. **Do not invent facts about third-party APIs.** Check the documentation or the source of the version in `Directory.Packages.props`. If you could not check, say so.
6. **A new dependency needs a decision** (ADR-0003). Stop and ask.
7. **Secrets never appear in code,** tests, configuration files, logs or commit messages. Test keys are generated at run time or are obviously fake and allow-listed by file.
8. **Ask when ambiguous.** Do not pick an interpretation silently.
9. **Keep the sample, the templates and the docs in step** with the change, in the same pull request (ADR-0021).
10. **Record evidence** as you go, on the ticket.
11. **Stay within the size limits** (ADR-0029): 200 changed lines of hand-written code and tests is the soft limit, 400 the hard one. A change that does not fit is a planning problem; raise it, do not squeeze or split it silently.

### Verification evidence

Before claiming a ticket done, the agent runs the commands and writes on the ticket and in the pull request description, for each: the command, the exit code, and the counts (tests passed, failed, skipped; warnings).

- "Should work", "looks correct" and "tests would pass" are not evidence.
- If something could not be run (no container runtime, no network), say exactly what and why. Do not present it as passed.
- Report failures as failures, with the output.

### Style pointers

Enforced by tooling, listed so nobody argues with the build: file-scoped namespaces; one public type per file; `internal` implementations behind public interfaces; `Add*` / `Use*` / `Map*` setup extensions; async methods take a `CancellationToken` last; time through `TimeProvider`; ids through `Guid.CreateVersion7()` or a typed id; log through generated log methods; errors declared in an error catalogue, never inline; XML docs on public members. Match the code around you where tooling is silent.

### Definition of done

For a ticket:

- [ ] The pull request closes exactly one ticket (`Closes #n`), names its plan row, and is within the size limits.
- [ ] Every requirement and acceptance scenario the ticket serves (for an initial-build milestone: of the cited sections of `specs/0000-framework/`) has a test that would fail without the change.
- [ ] `dotnet build YAF.slnx -c Release` — zero warnings.
- [ ] `dotnet test YAF.slnx` — zero failures, no newly skipped tests.
- [ ] No guard weakened; every changed approved file explained.
- [ ] Public API change: XML docs, articles, sample and templates updated; docs build clean.
- [ ] Migrations follow expand/contract; no pending model changes.
- [ ] No secret, personal data or internal detail in logs, errors or test output.
- [ ] Changes to the approved files are within the plan's declared delta; deviations are on the ticket, and in a plan update when they change the approach.
- [ ] The local code review ran and its findings are resolved (ADR-0027).
- [ ] The pull request description has Why, What, `Closes #n`, the plan link, Evidence, Impact checklist, Reviewer notes.

A spec or a milestone is done when all its tickets are `Done` and every requirement and acceptance scenario has its test.

## Consequences

- The loop is slower per ticket than writing code straight through and faster per merged change.
- One ticket per pull request means more pull requests, more rebases and stacked branches; that is the price of reviews that find things.
- Rule 3 will stop work more often than feels comfortable. Each stop is a question a human should answer once, not a decision an agent should make silently many times.
- Humans are held to the same rules, which keeps the rules honest.

## Enforcement

- Warnings are errors; analyzers and banned APIs run in every build.
- CI fails on newly skipped tests (a count compared with the base branch) and on new suppressions (a diff check over `#pragma warning disable`, `NoWarn` and `SuppressMessage` that requires a `guard-change` label; check `factory/guards`). The label counts only when a CI job, reading the label event, confirms that the actor is a human member of the maintainers team; a label applied by a bot or an agent's token fails the check.
- `CODEOWNERS` covers the guard files (approved files, banned-API lists, analyzer configuration, architecture tests, CI workflows, analyzers), so a change to any of them needs a code owner's review.
- CI fails when an approved file differs from the generated one.
- The pull request template's evidence section is required; a check rejects an empty one.
- `factory/ticket`, `factory/size` and `factory/delta` hold a pull request to one ticket, the size limits and the declared delta (ADR-0029).
- Secret scanning runs before commit (hook) and in CI.

## Guidance for agents

- Load the `yaf-implementing-tickets` skill before starting a ticket.
- If the test you were told to write cannot fail, say so on the ticket; the ticket is wrong.
- When a guard fires, read what it protects (its ADR or article) before anything else. Most of the time the code is wrong, not the guard.
- If the same problem has beaten you twice, stop. Label the ticket `needs-human` and report what you tried, what you saw, and what you think the cause is.
- Never write "all tests pass" unless you ran them in this session and are quoting the result.

## Related

ADR-0003, ADR-0004, ADR-0018, ADR-0020, ADR-0021, ADR-0023, ADR-0025, ADR-0027, ADR-0029.
