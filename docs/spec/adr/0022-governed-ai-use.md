# ADR-0022 — Governed AI use

> **Status** Accepted · **Date** 2026-11 · **Deciders** maintainers

> **Release.** The decision is accepted; the package it describes, `YAF.AI`, and the `DecisionRecord` aggregate are planned for a later release and are not part of YAF 1.0. Nothing in 1.0 implements or references them.

## Context

Services increasingly call AI models. A model call is an outbound data flow, a source of untrusted output and, when tools are involved, an actor that can change state. Regulation asks for record-keeping, transparency and human oversight in defined situations. Left to each service, these are implemented unevenly or not at all.

## Drivers

- Every model call is attributable, bounded and recorded without effort from the feature author.
- Personal and secret data do not reach a provider by accident.
- Model output is treated as untrusted input.
- A model never has more access than the user it acts for.
- People can review, override and stop AI-driven actions.
- Services that do not use AI pay nothing.

## Options considered

| Option | For | Against |
|---|---|---|
| Guidance only (an ADR listing duties) | No code | Uneven implementation; no evidence |
| Provider SDKs used directly with logging conventions | Flexible | Records, limits and minimisation depend on discipline |
| A full agent framework in the core | Feature-rich | Large dependency, fast-moving, outside the framework's scope |
| **Optional thin gateway over the platform's AI abstractions** | Uniform controls, provider-neutral, opt-in | Another package to maintain; features must go through it |
| Ship the gateway in the first release | AI features governed from day one | Widens the first release with a package over fast-moving abstractions and unstable conventions, before the core it relies on is proven |

## Decision

1. **`YAF.AI` is optional** and built on `Microsoft.Extensions.AI`. Provider SDKs are the app's choice. Nothing else in YAF depends on it.
2. **Named features.** Application code calls `IAiGateway` with a feature name. A feature declares its chat client, an **allow-list of exact model ids**, its purpose, its personal-data policy, quotas, retention and whether side effects need approval.
3. **Per call the gateway**: checks the feature flag and per-tenant kill switch, the allow-list and the quota; minimises input by classification (secret refused; personal data refused by default, pseudonymised or allowed with justification); applies size, token and time limits and the resilience pipeline; calls the model in a span; validates structured output against a schema with at most one repair attempt; records the call; attaches provenance to the result.
4. **Prompts** are versioned templates stored in the service and covered by an approval test. Instructions and user or retrieved content are separate channels.
5. **Call records** are append-only, written in the caller's transaction and sealed into a hash chain afterwards by the audit trail's sealer (ADR-0010): context (tenant, actor, initiator, correlation), feature, purpose, provider, model and version, template id and version, input and output hashes, optional encrypted content, parameters, tool calls, usage, guardrail verdicts, outcome, decision link. Retention is per feature with a **floor of six months** that purge respects; erasure works through subject-key shredding.
6. **Transparency.** Response contracts with model output implement `IAiGenerated`; the HTTP layer adds a response header and an OpenAPI extension; conversational endpoints carry an interaction notice flag; persisted generated content stores its provenance; provider content credentials pass through untouched; `/capabilities` lists AI features.
7. **Human oversight.** A `DecisionRecord` with states `Proposed → Approved | Overridden | Rejected | Expired`. When a feature requires approval for side effects, a side-effecting tool is returned as a pending decision and executed only by an approval command under the reviewer's context.
8. **Tools** are YAF requests sent through the dispatcher under the **caller's execution context**, from a per-feature allow-list. Permissions and tenant isolation apply to the model as to the user.
9. **Retrieval.** Vector stores are accessed through `ITenantVectorStore`, which always applies the tenant filter; a subject's vectors are removed on erasure.

### What this does not claim

It does not determine whether a system is high-risk, does not establish conformity with the AI Act, cannot make output accurate or unbiased, and an approval record is not human oversight unless competent, empowered people perform it. Marking generated media is the generating system's responsibility.

### Regulatory timing

Obligations and application dates of the AI Act have shifted since adoption, including a postponement of the high-risk requirements and grace periods for content marking. The article in the docs states the dates as of its writing and must be re-checked against the official text at each release; the controls here are useful regardless of those dates.

## Consequences

- AI features gain audit, limits and kill switches by configuration.
- Refusing personal data by default will block naive prompts; authors must choose a policy consciously.
- Storing content is opt-in; with hashes only, a call can be proven but not replayed.
- Streaming responses are recorded at completion; partial output is not validated against a schema.
- The gateway is deliberately not an agent orchestration framework.
- Until the package ships, a service that calls a model has no framework support for these duties and must meet them itself; the 1.0 mechanisms it would build on (classification, redaction, key rings, retention, security events) are available.

## Enforcement

The following applies from the release that ships `YAF.AI`.

- Analyzer `YAF13001`: `IChatClient` injected outside composition roots when `YAF.AI` is referenced.
- Tests with a fake chat client: record completeness and chain verification; allow-list, kill switch and quota refusals without a provider call; minimisation policies; invalid output handling; approval flow; tools cannot cross tenants or exceed the caller's permissions; retention floor.
- Security events `ai.model_not_allowed`, `ai.quota_exceeded`, `ai.output_invalid`, `ai.tool_denied`, `ai.kill_switch`, `ai.personal_data_refused`.
- Prompt template approval test including a secret scan.
- Controls `C-AI-1…5` in the generated control matrix.

## Guidance for agents

- Do not build `YAF.AI`, `DecisionRecord` or any AI sample feature as part of the 1.0 milestones; they have their own spec in a later release.
- Once it exists: call models only through `IAiGateway` and a declared feature; never resolve a chat client in application code.
- Pin exact model ids in the allow-list; do not use "latest" aliases.
- Classify prompt variables. Do not switch a feature's personal-data policy to `Allow` without a written justification from a human.
- Bind output to a typed schema and handle the failed result; never execute, render raw or interpolate model output into queries or commands.
- Expose tools as existing requests with their own authorization markers; mark state-changing tools as side effects.
- Change prompts through their versioned templates and update the approved file.
- Do not write claims of AI Act compliance into code, docs or commit messages.

## Related

0004, 0005, 0009, 0011, 0014, 0015, 0016, 0017, 0018, 0021
