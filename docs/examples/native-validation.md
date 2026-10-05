# Explicit observable validation for native consumers

Use caller-created streams and ordinary delegates to register rules and present
validation without discovering model properties or target setters at runtime.
These APIs ship in [8.1.0-runic.0.790.17.15](https://github.com/Runic-Artifex/ReactiveUI.Validation/releases/tag/runic-v8.1.0-runic.0.790.17.15)
from exact source `f22d2bb42c30d66df19a59333ed4fa633b241940`. The local
source-pinned pair passes five cases per flavor in managed, standalone full-trim
and Linux x64 NativeAOT modes. Final CI passes actual Linux x64/Windows x64 native
cases using its own Ubuntu package pair. The release matrix independently
verifies both native RIDs against the pair it publishes, with zero positive-path
warnings/errors. The [implementation review](../upstream/reviews/2026-10-implementation.md#native-runtime-and-examples-follow-up)
records those distinct package hashes and the historical preliminary CI.
The immutable `8.1.0-runic.0.790.17` release does not contain these new APIs.

## Register rules and retain property metadata

The [rule extensions](../../src/ReactiveUI.Validation/Extensions/ObservableValidationRuleExtensions.cs)
provide these forms on both `IValidationContext` and `IValidatableViewModel`:

```csharp
ValidationHelper AddObservableRule(
    IObservable<IValidationState> states, IEnumerable<string> propertyPaths);

ValidationHelper AddObservableRule<TValue>(
    IObservable<TValue> values, Func<TValue, IValidationState> validate,
    IEnumerable<string> propertyPaths);
```

Supply an initial value or state and serialize changes on the model owner. A
complete custom state reaches the rule/helper and direct state callback unchanged;
its validity is independent of text. Context aggregation retains its existing
aggregate-state contract. This API requires no `IReactiveObject` constraint.

An empty `propertyPaths` sequence means a model-wide rule. A cross-field rule can
name several paths, such as `[nameof(Model.Email), nameof(Model.Confirmation)]`.
Full dot-separated paths are compared ordinally; `Billing.Postcode` and
`Shipping.Postcode` are different metadata. Duplicate paths count once. Null,
empty, whitespace-containing paths and paths with empty segments are rejected.
The library checks the path format, not whether a member exists. The application
owns accurate metadata and observation of any nested objects.

The view-model form captures the current default context once. The explicit
context form captures the supplied context. Replacing the model's context later
does not move the rule or redirect helper cleanup. Dispose the helper before its
captured context: it unregisters its own component and disposes the connection
owned by that component. It does not dispose the caller's observable or context. If registration or
initial helper activation fails, rollback removes that exact component from the
captured context and disposes its owned connection. The original exception is
preserved; a cleanup failure produces an `AggregateException` with the original
exception first.

## Follow selections and assign typed results

The [binding extensions](../../src/ReactiveUI.Validation/Extensions/ObservableValidationBindingExtensions.cs)
accept an `IObservable<TSource?>`, where `TSource` is a class. These extension-call
shapes omit the source receiver and generic type parameters inferred by C#:

```csharp
sources.BindObservableValidationState(
    selectStates, converter, onNext);

sources.BindObservablePropertyValidationState(
    selectContext, propertyPath, converter, onNext, strict: false);
```

`selectStates` is `Func<TSource, IObservable<IValidationState>?>`; its converter
is `Func<IValidationState, TOut>`. `selectContext` is
`Func<TSource, IValidationContext?>`; its converter is
`Func<IList<IValidationState>, TOut>`. Both assign via `Action<TOut>` and return
`IValidationBinding`.

`sources` must emit its initial selection and every replacement, including null.
A selector delegate reads the selection when invoked; it does not observe later
helper or context property changes. Emit again when such a selection changes.

| Condition | State binding | Property binding |
| --- | --- | --- |
| Null source | `ValidationState.Valid` | Empty state list |
| Null selected state stream or context | `ValidationState.Valid` | Empty state list |
| Non-null selection | States from the latest selected stream; that stream supplies an initial state | Actual states from every currently matching rule; each active rule supplies an initial state |
| No matching property rules | Not applicable | Empty state list |
| Replacement | Detaches the previous state subscription | Detaches the previous context/rule subscriptions |

Property bindings have no synthetic valid prelude for existing rules. With
`strict: true`, a rule matches only if it names that full path exclusively. With
`strict: false`, a multi-property rule containing the exact path also matches.
Treat the converter's state list as read-only. Rich custom class and struct
states, and repeated state emissions, are preserved through this route.

The callback performs a statically compiled assignment, for example
`value => view.Status = value`. It may project to a bool, enum, nullable value or
custom presentation value without deriving validity from text. Dispatch to the
UI scheduler in the application adapter when required. Validation does not
change the source notification scheduler or global ReactiveUI defaults.
Selector, stream, converter and callback errors propagate through the ordinary
observable subscription; these APIs add no recovery or scheduling policy.

Disposing a binding detaches subscriptions, including after replacement or null
selection. Repeated and reentrant disposal are supported. Bindings do not dispose
caller-owned models, helpers, contexts or streams. Row removal and asynchronous
work need an application cancellation/revision policy so stale work cannot
update a currently registered rule.

## Realistic migration cases

The [package corpus](../../examples/NativeValidation/README.md) uses notifying
application models and package-only consumers:

| Case | Explicit safe route |
| --- | --- |
| Reusable generic field | Supply `IObservable<TValue>`, a complete-state delegate and full paths; export per-property errors and dispose the helper. |
| Nullable editor and rich typed target | Observe nested address and postcode replacements explicitly, emit null when required data is absent, follow model/helper replacements and assign nullable presentation state directly. |
| Blocking/advisory cross-field rules | Register one rule carrying both full paths in the blocking context and keep advisory ownership separate. Strict selection excludes that multi-property rule. |
| Dynamic rows and asynchronous uniqueness | Deliver initial pending state and rich result revisions; dispose each row helper on removal so late removed results cannot alter current validation. |
| Existing observable foundation | Exercise audited constructors/generated OAPH, existing supplied-observable rules with metadata selectors, property errors and synchronous domain events/removal. |

The baseline counterpart pins immutable `.790.17` assets and intentionally fails
strict IL2026/IL3050 analysis. Its nullable-address managed result also exposes
the application's required-data policy gap: legacy nested observation retains
its last valid value. Strict compile failure is distinct from native runtime
failure. The safe counterpart explicitly emits null and intentionally replaces
two single-property registrations with one multi-property rule; the metadata
and membership change is visible. Row results use deterministic delivery, not a
network or timing test.

The bounded [application adapter checks](../../examples/NativeValidation/ApplicationAdapterChecks.cs)
run inside `generic-field`. They assert synchronous initial parent replacement
and null, old/latest/outer subscription detachment, owner disposal rejecting a
returned handle, disposed listeners skipped in notification snapshots, and
initial getter/callback failure propagating the original exception with handlers
detached. The application adapters install pending ownership before subscribing,
so an obsolete returning handle cannot replace the latest selection. A supplied
source that throws before returning its handle must clean up its own handlers.
Equivalent generated-observer tests remain deferred with the future generator.

## Package and support boundary

Primitives uses `ReactiveUI.Validation.*` with `DynamicData`; Reactive uses
`ReactiveUI.Validation.Reactive.*`, `DynamicData.Reactive` and System.Reactive.
Use the matching released Runic.DynamicData package. The Primitives consumer
graph stays free of System.Reactive and both graphs stay free of upstream
DynamicData packages.

The [NativeAOT status](../aot-and-generators.md) separates the new strict
safe-path gate from the dated warning-bearing release investigation. Legacy
expression observation and reflected targets retain their trimming contracts.
A typed action on a legacy `BindValidationState` overload leaves its reflective
source-selection path in place. Platform UI controls, bridge/browser behavior
and unexecuted RIDs do not follow from a console consumer result.
