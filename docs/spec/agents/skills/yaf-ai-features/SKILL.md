---
name: yaf-ai-features
description: Building a feature that calls an AI model in a YAF service through YAF.AI (a package planned for a release after 1.0) - declaring features, the IAiGateway, versioned prompt templates, data minimisation, structured output validation, tools as dispatcher requests, human-oversight decision records, transparency markers, quotas, retention and tests with a fake chat client. Load BEFORE adding any model call, prompt, embedding, vector search or AI tool; before injecting IChatClient or a provider SDK; before persisting or returning model output; and before changing Ai configuration or a prompt template.
license: MIT
metadata:
  owner: yaf
  version: "1"
---

# YAF AI features

`YAF.AI` is planned for a later release and is not part of 1.0. This skill applies to a repository in which the package exists. Until then a service adds no model call: do not inject `IChatClient` or a provider SDK as a stopgap.

Every model call goes through the gateway so it is authorised, minimised, bounded, validated and recorded. The package supports record-keeping, transparency and oversight; it does not make a model accurate, and it does not decide a system's risk class.

## When to load

- Adding or changing an AI-backed use case, prompt template, tool or embedding index.
- Wiring a model provider.
- Storing or returning anything a model produced.
- Deciding whether a human must approve a model-proposed action.

## Where does this go?

| Concern | Place |
|---|---|
| Provider SDK and `IChatClient` registration (keyed) | Composition root only |
| Feature declaration (models, purpose, policy, quota, retention) | `builder.AddYafAi(ai => ai.AddFeature("name", …))` |
| Prompt text | Versioned template resource in the service (`Prompts/<id>.v<n>.md`) |
| The call | A handler, through `IAiGateway` naming the feature |
| Something the model may do | A YAF request on the feature's tool allow-list |
| A proposed side effect awaiting a person | `DecisionRecord` + `ApproveDecision` command |
| Vector search | `ITenantVectorStore` |

## Rules

1. **REQUIRED** Application code injects `IAiGateway`, never `IChatClient` or a provider SDK (YAF13001).
2. **REQUIRED** Every call names a declared feature with an exact model allow-list (`provider/model-id@version`), a `Purpose`, a quota and a retention period.
3. **REQUIRED** Prompts are `PromptTemplate(id, version)` with typed variables. A changed template is a new version and a reviewed diff. Templates contain no secrets.
4. **REQUIRED** System instructions and user/retrieved content are separate channels. Retrieved or user-supplied text is passed as data, never concatenated into instructions.
5. **REQUIRED** Variables are classified. `[SecretData]` is refused; personal data follows the feature's `PersonalDataPolicy` — `Refuse` by default; `Pseudonymise` or `Allow` need a written justification in the feature declaration.
6. **REQUIRED** Structured results use `CompleteAsync<T>` with a strict schema. Model output is never executed, never rendered unencoded, never used as a query, command or file path without validation.
7. **REQUIRED** Tools are YAF requests run through the dispatcher under the **caller's** execution context (the caller is the `Actor`); they keep their authorization markers. No tool is a `[SystemOnly]` request and none runs with a system principal, so a model call made from a job or consumer has no tools.
8. **REQUIRED** A tool that is a command is an `AiEffect.SideEffect`. Where the feature says `RequireHumanApprovalFor(AiEffect.SideEffect)`, return the pending `DecisionRecord`; do not execute.
9. **REQUIRED** Responses containing model output implement `IAiGenerated`; persisted model output stores provenance (`IAiProvenanceMemento`). Conversational endpoints declare `.WithAiInteractionNotice()`.
10. **REQUIRED** Handle the gateway's `Result`: quota, disallowed model, invalid output and refusal are declared errors, not exceptions. Provide a non-AI fallback or a clear failure — never a silent empty answer.
11. **REQUIRED** Vector queries go through `ITenantVectorStore`; personal documents are not indexed unless the feature's policy allows it, and the type's eraser removes its vectors.
12. **REQUIRED** Content storage (`StoreContent`) is off unless needed; when on, it is encrypted and redacted by the framework. Do not log prompts or outputs yourself.
13. **REQUIRED** AI call retention never goes below the six-month floor.
14. **REQUIRED** Tests use `FakeChatClient`; no test calls a real provider.
15. **REQUIRED** Do not describe a feature as "compliant with the AI Act", "unbiased" or "human-supervised" in code, docs or UI text. An approval record is not, by itself, human oversight.
16. **RECOMMENDED** Keep the model's job narrow: extraction, classification, summarising. Decisions belong to aggregates and, where required, to a person.
17. **RECOMMENDED** Set a per-feature kill switch default and document who may flip it.

## How to

1. Tests first with `FakeChatClient`: happy path, invalid output, quota exceeded, personal data refused, side effect pending approval.
2. Declare the feature; add the template; write the handler.
3. Expose provenance in the response.

```csharp
// composition root
builder.Services.AddKeyedChatClient("default", /* provider registration */);
builder.AddYafAi(ai => ai.AddFeature("session-summary", f => f
    .UseChatClient("default")
    .AllowModels("provider/model-id@2026-10")
    .Purpose("summarise-session-feedback")
    .PersonalData(PersonalDataPolicy.Refuse)
    .Quota(perTenantPerDay: 500)
    .Retention("P6M")));
```

```csharp
[RequiresPermission("sessions", "manage")]
public sealed record SummariseFeedbackQuery(SessionId SessionId) : IQuery<Result<FeedbackSummaryResponse>>;

public sealed record FeedbackSummary(string Headline, IReadOnlyList<string> Themes, int Sentiment);

public sealed record FeedbackSummaryResponse(FeedbackSummary Summary, AiProvenance? Ai) : IAiGenerated;

internal sealed class SummariseFeedbackHandler(IAiGateway ai, IFeedbackReadModel feedback)
    : IRequestHandler<SummariseFeedbackQuery, Result<FeedbackSummaryResponse>>
{
    public async ValueTask<Result<FeedbackSummaryResponse>> HandleAsync(SummariseFeedbackQuery q, CancellationToken ct)
    {
        var comments = await feedback.AnonymousCommentsAsync(q.SessionId, limit: 200, ct);   // no author data

        var prompt = new AiPrompt(new PromptTemplate("session-summary", version: 3))
            .WithData("comments", comments);                                                   // data channel

        var result = await ai.CompleteAsync<FeedbackSummary>("session-summary", prompt, ct);
        if (result.IsFailure) return result.Errors.ToArray();

        return new FeedbackSummaryResponse(result.Value.Value, result.Value.Provenance);
    }
}
```

```csharp
[Fact]
public async Task Summarise_InvalidModelOutput_FailsAfterOneRepair()
{
    var chat = new FakeChatClient().Returns("not json").Returns("still not json");
    var handler = TestAi.Handler(chat);

    var result = await handler.HandleAsync(new SummariseFeedbackQuery(SessionId.New()), CancellationToken.None);

    result.IsFailure.Should().BeTrue();
    chat.Calls.Should().HaveCount(2);
    TestAi.CallRecords.Should().ContainSingle().Which.Outcome.Should().Be("invalid_output");
}
```

## Anti-patterns

| Wrong | Why | Instead |
|---|---|---|
| `IChatClient` injected into a handler | No record, quota, allow-list or minimisation (YAF13001) | `IAiGateway` + feature |
| `$"Summarise: {userText}"` as the system prompt | Prompt injection | Template + data channel |
| Sending attendee names/emails "for context" | Personal data to a third party | Ids or pseudonyms; policy `Refuse` |
| `"model": "latest"` | Unreviewed model change | Exact, allow-listed version |
| Executing a model-suggested command directly | Excessive agency | Tool allow-list + decision record |
| Tool handler using a system principal | Model gains more rights than the user | Caller's execution context |
| `JsonSerializer.Deserialize<T>(text)` on raw output, loosely | Accepts junk and extra members | `CompleteAsync<T>` strict schema |
| Returning model text without `IAiGenerated` | No transparency marker | Implement it; store provenance |
| Logging the prompt to debug | Leaks content; duplicates the record | Read the AI call record |
| Catching the failure and returning `""` | Silent wrong answer | Declared error or real fallback |

## Verify

- `dotnet build YAF.slnx` (YAF13001, YAF2001 on variables and responses).
- `dotnet test --project <service>/tests/Application.Tests` — fake-client tests for each outcome in the list above; a tool cannot cross tenants or exceed the caller's permissions.
- API test: `AI-Generated` header and `x-yaf-ai-generated` in the committed OpenAPI document.
- Prompt template approval file updated on purpose; data inventory regenerated if content is stored.

## References

- `docs/articles/ai.md` — gateway behaviour, call record fields, retention, events.
- `adr/0022-governed-ai-use.md` — scope and limits of what the package claims.
- `samples/conference/src/Services/Catalog/Application/` — the session-feedback summary feature and its tests, added to the sample together with the package.
- Skills: `yaf-privacy-and-data-classification`, `yaf-security`, `yaf-use-cases`.
