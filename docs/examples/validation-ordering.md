# Validation, model turns and command admission

Runic owns short serialized model turns. Inject the scheduler returned by
`RunicReactiveSchedulerProvider.For(modelContext)` into the validation model and
its commands. Bind independently presented children to that same context. The
Primitives provider returns an `ISequencer`; the System.Reactive provider returns
an `IScheduler`. This recipe does not change process-global `RxApp` schedulers.

A model turn changes domain data or rule membership, updates current validation,
and then samples command admission. `ValidationContext.Valid` and
`ValidationStatusChange` describe current registered rules;
`GetIsValid()` synchronously checks them. `IsValid` and `Text` are scheduled
presentation properties. Their notifications can arrive in a later model turn,
so their getters are unsuitable as an immediate admission check with a queued
scheduler. A status payload carries its emitted validity and text together; it
must not borrow validity from a presentation property that is still queued.

## Executable coverage

The [collection model](../../examples/CollectionValidation/CollectionValidationRecipe.cs)
and [child](../../examples/CollectionValidation/RecipeChild.cs) contain all imports
for both flavors. The optional [ordering probe](../../examples/ValidationOrdering/Program.cs)
adds imports for the matching command and SDK scheduler provider. Run it from the
repository root, passing an SDK source checkout explicitly:

```sh
python3 eng/restore-fork-dependencies.py
dotnet run --project examples/ValidationOrdering/Primitives/ValidationOrdering.Primitives.csproj -c Release -p:RunicSdkRoot=/absolute/path/to/runic-sdk
dotnet run --project examples/ValidationOrdering/Reactive/ValidationOrdering.Reactive.csproj -c Release -p:RunicSdkRoot=/absolute/path/to/runic-sdk
```

On NixOS prefix each command with
`direnv exec /absolute/path/to/runic-sdk`. Both projects target .NET 10, reference
the matching Validation project, and compile the actual SDK `RunicModelContext`
and scheduler-adapter source without modifying that checkout. They are opt-in
examples outside the normal solution and package output. The provider and its
model context are real SDK implementation code; this is **not** an end-to-end
SDK bridge or browser test.

Adding an invalid rule to a settled valid context used to leave `GetIsValid()`,
`HasErrors` and command admission stale within the same model turn, because
membership was scheduled before domain aggregation. Membership and aggregate
streams are now synchronous and only presentation properties are scheduled.
Public `Validations` membership is delivered synchronously on the model owner;
apply a separate rendering scheduler at the presentation boundary if needed. The
probe checks same-turn rule addition/removal and child edits with each SDK adapter.

The shared [ValidationContextSchedulingTests](../../src/tests/ReactiveUI.Validation.Tests/ValidationContextSchedulingTests.cs)
use an immediate-only manual queue implementing the flavor's actual scheduler
contract. They pin add, remove, bulk removal, ongoing validation results,
message-only changes, pending presentation disposal, and reentrant rule removal.
They compare current raw streams and command availability with presentation
properties before and after the queue drains. This queue provides deterministic
library coverage; it is not described as the SDK model context.

## Domain validity and presentation

The command consumes `ValidationContext.Valid`. Guard current domain invariants
inside its body as well, particularly for direct or programmatic callers:

```csharp
var created = ReactiveCommand.Create(() =>
{
    if (!model.IsCurrentlyValid || !model.ValidationContext.GetIsValid())
    {
        return;
    }

    // Commit domain work in the owning model turn.
}, model.ValidationContext.Valid, scheduler);
```

`ICommand.Execute` does not itself enforce `CanExecute`. A Runic bridge samples
availability before it invokes a command; an internal caller must either do that
or rely on the body guard. For asynchronous work, read its initial request in a
short turn, perform I/O outside the turn, then recheck current invariants in the
turn that commits the result. An asynchronous validator's current result does
not prove future validity: explicitly represent pending state and decide whether
it blocks admission. These regressions marshal completed observable results to
the owner; they do not implement cancellation or an async-validation framework.

`Touched`/`Submitted` affect error display only. Hiding errors must not change
blocking rules or command admission. The collection's direct predicate evaluates
its current children; `GetIsValid()` additionally includes any other registered
blocking rules.

## Notifications and the bridge contract

An arbitrary `PropertyChanged` or `WhenAnyPropertyChanged` side effect can run
before a validation observer if it was registered first. There is no universal
Rx observer order that makes all-property side effects a validation boundary.
Read current errors from `ErrorsChanged`, observe explicit validation state, or
perform the domain check after the mutation in its model turn. This addresses the
ordering concerns in the dated review of
[#515](https://github.com/reactiveui/ReactiveUI.Validation/issues/515),
[#92](https://github.com/reactiveui/ReactiveUI.Validation/issues/92) and
[#95](https://github.com/reactiveui/ReactiveUI.Validation/issues/95).

`ReactiveValidationObject` updates `HasErrors` before raising property-named
`ErrorsChanged`. The
[ValidationOrderingTests](../../src/tests/ReactiveUI.Validation.Tests/ValidationOrderingTests.cs)
read `HasErrors` and `GetErrors(property)` inside that callback for valid-to-invalid
and invalid-to-valid child edits. They also pin synchronous current validity,
raw command availability and a guarded direct invocation before queued
presentation updates settle.

Read-only inspection of SDK `BridgeRuntime.cs`, `BridgeValidation.cs` and
`BridgeDataSubscriptions.cs` confirms the exported contract is
`INotifyDataErrorInfo`: the bridge reads `HasErrors`, entity-level
`GetErrors(null)`, and named-property errors, and republishes on `ErrorsChanged`.
`ValidationContext` alone is not an exported bridge validation contract. Current
`ReactiveValidationObject.GetErrors(null)` aggregates its invalid property rules;
it does not automatically publish bare non-property context rules as distinct
entity errors. Use property-associated rules or an explicit error-contract
adapter when such errors must be rendered. Generated metadata resolves CLR
property/member paths; a collection-level `Children` message is not an indexed
per-child message.

The probe verifies SDK context/adapter scheduling with the library. Bridge command
routing, generated frontend paths, notification batching and browser rendering
remain the SDK's separate verification responsibilities.
