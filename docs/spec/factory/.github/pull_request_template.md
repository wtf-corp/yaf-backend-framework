<!--
Title: a Conventional Commit, e.g. `feat(core): typed id contract`. It becomes the squash commit and decides the release.
One ticket, one pull request. The lines "Closes" and "Plan" are read by the factory/ticket check: keep their form.
For a spec or plan pull request, delete the Ticket section.
-->

## Ticket

Closes #
Plan: specs/NNNN-slug/plan.md#T

<!-- Only for a change that needs no ticket (typo, comment, a fix under about 20 lines with no API, security, privacy or schema impact):
Trivial because: …
-->

## What changed

<!-- Two or three sentences: what the change does for a user of the framework. Not a list of files. -->

## Evidence

| Command | Exit | Result |
|---|---|---|
| `dotnet build YAF.slnx -c Release` | | |
| `dotnet test YAF.slnx` | | |

Failing test first: `<TestClass.Test_name>` failed with … before the change.

## Impact

- [ ] Public API, error codes, wire contracts, configuration keys, telemetry names: none / exactly what the plan's `factory:delta` declares
- [ ] Schema: none / expand-contract migration, described in the plan
- [ ] Personal data and security tenets: not touched / touched — which
- [ ] Docs, sample, templates updated where the change shows
- [ ] Guards weakened: none / explained below, `guard-change` requested from a maintainer

## Deviations from the plan

none

<!--
The tier label, auto-merge and the status of the ticket are set by workflows. Do not set them by hand.
Agents never merge, approve, or apply `guard-change`, `major-release` or `size-accepted`.
-->
