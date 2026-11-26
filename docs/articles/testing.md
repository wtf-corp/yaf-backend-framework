# Testing

`YAF.Testing` holds the helpers a domain test needs: assertions on results, approval files, memento round trips, raised events, aggregate invariants and state machines, and a clock the test moves by hand. A domain test project references `YAF.Testing` and, through it, `YAF.Domain` and `YAF.Core`. It needs no host, no container and no database.

The helpers depend on no test framework and no assertion library. When an assertion fails they throw <xref:YAF.Testing.YafAssertionException>, which any test framework reports as a failed test. Its message names what was expected and what was found by error codes, type names, ids and member paths, never by error messages or values, which may contain personal data. The approval helper is the one that shows text: it lists the lines that differ, which is why an approved file holds codes and names, never personal data.

What the package holds today is what the domain needs. The fakes of the ports (execution context, unit of work, publishers, repositories, key and field protection) come with the milestone of the port they fake, from the application pipeline on; the architecture rules, deterministic test ids and synthetic data generators follow, and so do `[Control]` and `LogAssert`. `YAF.Testing.Integration`, with the web application factory and the database containers, comes with the first service that runs over HTTP and PostgreSQL.

The snippets come from the tests of `YAF.Testing` itself, which use a small booking model: the aggregate `Booking` with the child entity `BookingLine`, and the data classes `BookingData` and `BookingLineData` that implement their memento interfaces.

## Result assertions

<xref:YAF.Testing.ResultAssert> adds extension methods to <xref:YAF.Core.Results.Result> and `Result<T>`:

- `ShouldSucceed()` passes on a success and otherwise fails listing the error codes. On a `Result<T>` it returns the value, for further assertions.
- `ShouldFailWith(descriptor)` passes when the result is a failure and one of its errors has the code of the descriptor, usually a field of the error catalogue under test. Otherwise it fails listing the codes it found.

Both compare **codes**. An error message is text for people, which can change without breaking anyone; a code is the contract.

[!code-csharp[](../../tests/Testing.Tests/ResultAssertTests.cs#result-assert)]

## Approval files

<xref:YAF.Testing.Approvals>`.Verify(actualText, name)` compares a text with the committed file `<name>.approved.txt` next to the calling test, line by line. It is for contracts that change only on purpose: the codes of an error catalogue, the stable ids of protected fields, inventories and schemas. Line ends do not matter, and neither does a line end after the last line.

[!code-csharp[](../../tests/Testing.Tests/Approvals/ApprovalsTests.cs#approvals)]

When the texts differ, or the approved file is missing, the helper writes `<name>.received.txt` beside it and fails listing the added lines with `+` and the removed lines with `-`. When a difference is intended, read each listed line, replace the approved file with the received file and commit it with the change that caused it. Never commit a received file; the repository's `.gitignore` excludes `*.received.*`. When the texts are equal again, the helper deletes a stale received file.

### `DeterministicSourcePaths`

The helper finds the directory of the calling test through `[CallerFilePath]`, the path the compiler records for the source file. A build with `ContinuousIntegrationBuild` set to `true` is a deterministic build: it turns on `DeterministicSourcePaths`, and the compiler then maps the repository root to `/_/`. In this repository the root `Directory.Build.props` sets `ContinuousIntegrationBuild` on GitHub Actions. The recorded path becomes `/_/tests/…`, a directory that does not exist on the build machine, and the approved file cannot be found.

A test project that calls `Approvals.Verify` therefore sets the property to `false`, as `tests/Testing.Tests/Testing.Tests.csproj` does. It affects only the test assembly, which is never published, so released packages keep their deterministic paths. When the property is missing, the helper fails with a message that names `DeterministicSourcePaths` instead of reporting a missing approved file.

## Memento round trips

Every entity writes its state into a memento in `Snapshot` and reads it back in `Restore`, both by hand. <xref:YAF.Testing.MementoAssert>`.RoundTrips(entity, mementoFactory)` proves that nothing is lost on the way: it snapshots the entity into a memento from the factory, restores a new entity from it with the static `Restore` of `IOriginator`, snapshots that into a second memento and compares the two.

[!code-csharp[](../../tests/Testing.Tests/MementoAssertTests.cs#memento-round-trip)]

- Every property of the memento interface and of the interfaces it inherits is compared with `Equals`.
- A property of type `IList<TChild>` whose items are child mementos is compared item by item, matched by id, with the same rule.
- A failure names the path of the first member that differs, such as `Lines[<id>].Quantity`, and no value.
- Give the entity a value in every member, so that a member the restore forgets differs from its default. Run the test again with the nullable members `null`.

The factory is usually the data class that infrastructure declares for the memento interface. A parameterless `RoundTrips<T>()` over the generated data class comes with the `[MementoData]` generator, together with the persistence layer.

## Events and invariants

<xref:YAF.Testing.DomainEventAssert>`.Raised<TEvent>(aggregate)` returns the first queued event of that type, for assertions on its members, or fails listing the event types that were raised. It does not dequeue, so the aggregate's queue stays as it was:

[!code-csharp[](../../tests/Testing.Tests/DomainEventAssertTests.cs#domain-event-assert)]

<xref:YAF.Testing.AggregateAssert>`.InvariantsHold(aggregate)` calls `CheckInvariants` and fails listing the codes of the broken invariants. Call it after each behaviour and after a restore:

[!code-csharp[](../../tests/Testing.Tests/AggregateAssertTests.cs#aggregate-assert)]

## State machines

<xref:YAF.Testing.StateMachineAssert>`.IsWellFormed(machine, initial, finals)` checks a definition as a whole: every state is reachable from `initial`, and every state that is not in `finals` has a transition to another state. A transition from a state to itself is not an exit. When the state type is an enum, every value of the enum is checked, so a value the definition forgets is reported as unreachable; otherwise the states are those the definition names. A failure names the unreachable states and the states without an exit.

[!code-csharp[](../../tests/Testing.Tests/StateMachineAssertTests.cs#state-machine-assert)]

## The test clock

Domain code takes time as a value; code outside the domain reads it from a `TimeProvider`. <xref:YAF.Testing.TestClock> serves both: pass `Now` to a behaviour, and `TimeProvider` to code that reads the time. Both report the same instant. The parameterless constructor starts at `2026-01-01T00:00:00Z`; the other takes the start. Time moves only through `Advance` and `SetNow`, never by itself and never backwards.

[!code-csharp[](../../tests/Testing.Tests/TestClockTests.cs#test-clock)]

## Rules

1. Assert on error **codes** with `ResultAssert`, never on error messages.
2. Approved files change only on purpose: read every listed line before replacing one, and commit it with the change that caused it. Never commit a received file.
3. A test project that calls `Approvals.Verify` sets `DeterministicSourcePaths` to `false`.
4. Every aggregate has a memento round-trip test with a value in every member, and one with the nullable members `null`.
5. Call `AggregateAssert.InvariantsHold` after each behaviour under test.
6. Every state machine has an `IsWellFormed` test that names its initial and final states.
7. Time in a test comes from a `TestClock` or a fixed value, never from the system clock.
8. Test data that looks like personal data is obviously fake (`attendee-0001@example.test`).
9. A domain test project references `YAF.Testing` and nothing from ASP.NET Core, EF Core or a container library.

## Configuration

`YAF.Testing` has no options and no configuration keys. The one setting is the MSBuild property `<DeterministicSourcePaths>false</DeterministicSourcePaths>` in a `PropertyGroup` of a test project that uses approval files, explained in [`DeterministicSourcePaths`](#deterministicsourcepaths).

## Testing it

Each helper has tests of its own in `tests/Testing.Tests`, with a passing case and a failing one. The failing cases of the result, memento and event assertions also check that the message carries no error message and no value. One approval test compares with the committed `sample.approved.txt`, so the CI run of every pull request proves that the calling file is found under a CI build. The domain package uses the helpers in `tests/Domain.Tests`, whose round-trip test covers a memento member of every allowed type:

[!code-csharp[](../../tests/Domain.Tests/Mementos/OrderMementoTests.cs#round-trip-test)]
