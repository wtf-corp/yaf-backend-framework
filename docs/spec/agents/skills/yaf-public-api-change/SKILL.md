---
name: yaf-public-api-change
description: What counts as public API in YAF (public types and members, setup extensions, extension points, configuration keys, error codes, integration event wire names, job and consumer names, telemetry names, database schema of framework tables, template output) and what a change to it obliges in the same pull request - XML docs, article, sample, templates, PublicAPI.Unshipped.txt, approved files, package validation, and breaking-change commit rules. Load BEFORE adding, removing, renaming or changing the signature or behaviour of anything under src/ that is public, any configuration key, error code, wire name or telemetry name; before editing PublicAPI.*.txt or CompatibilitySuppressions.xml; and before writing a commit or PR title for such a change.
license: MIT
metadata:
  owner: yaf
  version: "1"
---

# Changing YAF's public API

Apps pin a released version and read the docs of that version. A public change that ships without docs, sample and templates is a broken release.

## When to load

- You are about to make anything `public` in `src/`, or change something that already is.
- You are renaming a namespace, type, option, error, event, job, span or metric.
- A build fails on RS0016/RS0017 (public API analyzer), CS1591 (missing XML doc) or package validation.
- You are choosing between `feat`, `fix`, `refactor` and their `!` forms.

## Is it public API?

Any "yes" means this skill applies.

| Surface | Examples | Recorded in |
|---|---|---|
| Public types and members of a package | `Result<T>`, `IDispatcher`, `AggregateRoot<…>` | `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` |
| Setup extensions and their order requirements | `AddYafHttp`, `UseYafHttp`, `AddYafMessaging` | Same + article |
| Extension points | `IPrivilegeSource`, `IApiKeyStore`, `IUserIdResolver`, `IMarkupSanitizer`, `IMessageTransport`, `IKeyProvider` | Same + article |
| Attributes and markers read by analyzers/generators | `[RequiresPermission]`, `[PersonalData]`, `[TypedId]` | Same |
| Analyzer ids, severities and behaviour | `YAF2001` | Analyzer release notes file |
| Configuration sections and keys, defaults | `Outbox:PollInterval` | Generated `configuration-reference.md` |
| Error codes, kinds, message templates | `core.authorization.undeclared` | `errors.approved.txt` |
| Wire names and envelope fields | `[IntegrationEvent("…", version)]`, `yaftenant`, `yafinitiator` | Contract schema approvals, envelope golden file |
| Job, cron and consumer names | `[Job("…")]`, `[CronJob("…")]`, `[Consumer("…")]` | Stored rows depend on them |
| Stored formats | `yaf1:<key-id>:…`, framework table schemas, the `UserId` derivation | Migration snippets, article |
| Field ids bound into ciphertext | `Attendee.IMemento.Email` | `fields.approved.txt` |
| Supported target frameworks | `net11.0` | `Directory.Build.props`, README |
| HTTP contract of framework endpoints | `/errors`, problem details shape | OpenAPI snapshot |
| Telemetry names | spans, metrics, log event ids | `TelemetryCatalog`, generated reference |
| Template output | what `dotnet new yaf-service` generates | `tests/Templates.Tests` |

Not public: `internal` types, test projects, benchmark code, CI. A change that touches none of the above says "no public API change" in the pull request.

## Rules

1. **REQUIRED** Prefer not adding public surface. Implementations are `internal` behind an interface and an `Add*` / `Use*` / `Map*` extension. `InternalsVisibleTo` names only the package's own test project.
2. **REQUIRED** Every new or changed public member has XML docs saying what it is for, what the caller must do first, and what it returns on failure; entry points have an `<example>` (CS1591/CS1573 are errors).
3. **REQUIRED** Add new members to `PublicAPI.Unshipped.txt` (the analyzer's code fix does it). Never edit `PublicAPI.Shipped.txt` by hand except through the release process.
4. **REQUIRED** In the same pull request update: the concept article in `docs/articles/`, the quick start when a service's setup changes, `samples/conference`, the `templates/`, and the relevant skill in `.agents/skills/` when the rule an agent must follow changed.
5. **REQUIRED** Regenerate and commit the generated references that the change touches (configuration, telemetry, error catalogue, data inventory, OpenAPI).
6. **REQUIRED** A decision that changes how the framework works gets an ADR (new, or the existing one amended with a dated note).
7. **REQUIRED** Breaking = removing or renaming a public type/member/key/code/wire name; changing a signature, default or behaviour callers rely on; adding a required extension point or required option; raising an analyzer to error; changing a stored or wire format without reading the old one; changing a recorded field id; dropping a target framework. Breaking changes use `feat!:` / `fix!:` / `refactor!:` **and** a `BREAKING CHANGE:` footer that says what to do.
8. **REQUIRED** Do not silence package validation. `CompatibilitySuppressions.xml` changes only for an intended, announced break.
9. **REQUIRED** Prefer the additive path: add the new member, mark the old `[Obsolete("Use X. Will be removed in vN.")]`, remove it in the next major.
10. **REQUIRED** Stored and wire formats stay readable across at least one release: ship the reader before the writer (expand, then contract).
11. **REQUIRED** The pull request title is the squash commit and decides the version: `fix` → patch, `feat` → minor, `!`/`BREAKING CHANGE` → major; `docs`, `test`, `ci`, `chore`, `refactor` release nothing.
12. **REQUIRED** Target the right branch. Until 1.0.0, `main` publishes prereleases only (`1.0.0-alpha.N`, then `1.0.0-beta.N`) and a breaking change may go to `main`, still marked with `!` and the footer. From 1.0.0 on, `main` takes `fix` and `feat` only (the title check rejects a breaking title there); a breaking change goes to `next`, is published as `N.0.0-rc.N`, and reaches `main` with the other breaks of that major at a planned time.
13. **REQUIRED** A new .NET target is added in a minor release and an old one dropped only in a major. Code that needs a newer runtime API lives in a file named `*.NetNN.cs` behind `#if`; no other target-framework conditionals. Only `YAF.Core` is marked AOT-compatible and trimmable: keep reflection and expression compilation out of it.
14. **RECOMMENDED** Put new public types in the lowest package that can own them; never make a core package reference a vendor SDK for convenience. A third-party engine goes in an adapter package named after it (`YAF.Markup.HtmlSanitizer`, `YAF.Http.Versioning`).
15. **RECOMMENDED** Name setup extensions for what they add, make them idempotent, and fail at startup with a message naming the missing piece.

## How to

1. Decide: additive or breaking? Write it in the plan.
2. Test first (in `tests/<Package>.Tests`), then the code with XML docs.
3. Accept the analyzer fix into `PublicAPI.Unshipped.txt`.
4. Update article → sample → templates → skill; regenerate references.
5. Build the docs site with warnings as errors.
6. Title the pull request.

```csharp
/// <summary>Publishes integration events through the transactional outbox.</summary>
/// <remarks>Call inside a unit of work; throws <see cref="InvalidOperationException"/> outside one.</remarks>
public interface IIntegrationEventPublisher
{
    /// <summary>Queues <paramref name="integrationEvent"/> for delivery after the current transaction commits.</summary>
    /// <param name="integrationEvent">The event; its type must carry <c>[IntegrationEvent]</c>.</param>
    /// <param name="options">Partition key, delay and deduplication id; <c>null</c> for defaults.</param>
    void Publish<T>(T integrationEvent, PublishOptions? options = null) where T : IIntegrationEvent;
}
```

```text
feat(messaging)!: require a partition key for ordered topics

BREAKING CHANGE: PublishOptions.PartitionKey is now required when the topic is declared ordered.
Set it to the aggregate id, or set Messaging:AllowUnordered=true to keep the previous behaviour.
```

## Pull request checklist

- [ ] Tests first, in the package's own test project.
- [ ] XML docs on everything public that changed.
- [ ] `PublicAPI.Unshipped.txt` updated; package validation green without new suppressions (or an announced break).
- [ ] Article(s), quick start, sample, templates, skill updated — or "no public API change" stated.
- [ ] Generated references and approved files updated on purpose.
- [ ] ADR added or amended when a decision changed.
- [ ] Title is a Conventional Commit; `!` and footer for a break; a break after 1.0.0 targets `next`.

## Anti-patterns

| Wrong | Why | Instead |
|---|---|---|
| `public` on a helper "in case someone needs it" | Permanent surface to support | `internal` |
| `InternalsVisibleTo("Acme.App")` | The app now depends on internals | Public extension point |
| Renaming an option key in a `fix:` | Deployed apps stop starting | `feat!:` + footer, or keep the old key as an alias |
| Changing a default quietly | Behaviour change without a signal | Breaking change, documented |
| Editing `PublicAPI.Shipped.txt` to quiet RS0017 | Hides a removal | Decide: deprecate or break |
| Adding a `CompatibilitySuppressions.xml` entry to get CI green | Ships an unannounced break | Additive path or `!` |
| Docs "in a follow-up PR" | The release publishes the gap | Same pull request |
| Updating the sample but not the templates | New services are generated wrong | Both; `Templates.Tests` proves it |
| Renaming a `[Job("…")]` name | Rows already stored cannot be handled | Keep the name; rename only the class |
| Renaming a protected memento member and accepting the new line in `fields.approved.txt` | Stored ciphertext is bound to the old field id and stops decrypting | Keep the recorded id with `[FieldId("…")]` |
| A `feat!` pull request to `main` after 1.0.0 | Would publish a major for one change | Target `next` |

## Verify

```bash
dotnet build YAF.slnx -c Release                 # CS1591, RS0016/RS0017, analyzers
dotnet pack YAF.slnx -c Release                  # package validation against the baseline
dotnet test --solution YAF.slnx
dotnet test --project tests/Templates.Tests      # when setup or templates changed
dotnet docfx metadata docs/docfx.json && dotnet docfx build docs/docfx.json --warningsAsErrors
```

- `git status` shows no stray `*.received.*`; generated references are in sync.

## References

- `docs/articles/contributing-public-api.md` — the long form of this skill.
- `adr/0020-versioning-and-release-automation.md` — how titles become versions; prereleases before 1.0.0, `next` for breaking changes after it.
- `adr/0021-documentation-and-generated-references.md` — what is generated and when.
- `adr/0006-repository-structure-and-packaging.md` — package boundaries and allowed dependencies.
- Skills: `yaf-results-and-errors` (codes), `yaf-configuration` (keys), `yaf-messaging` (wire names), `yaf-logging-and-telemetry` (telemetry names).
