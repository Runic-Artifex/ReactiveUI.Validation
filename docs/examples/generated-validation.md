# Generated validation and explicit Unsafe migration

**UNRELEASED working branch, 2026-10-06.** This recipe describes the generated
primary surface; the immutable `.790.17.15` release has the earlier runtime
behavior. New source/package/native verification belongs to the current
revision's gates, not that release's evidence.

## Consume the matching core package

Use `Runic.ReactiveUI.Validation` with ReactiveUI/Runic.DynamicData, or
`Runic.ReactiveUI.Validation.Reactive` with ReactiveUI.Reactive/
Runic.DynamicData.Reactive. Keep exactly one matching flavor. The
corresponding imports are `ReactiveUI.Validation.Extensions`, `.Contexts`,
`.Helpers`, `.Abstractions` and `.States`, or the same roots below
`ReactiveUI.Validation.Reactive`. The shipped dependency cohort remains
ReactiveUI 26.0.1 and released DynamicData 10.0.0-runic.5.

Both core packages embed `ReactiveUI.Validation.SourceGenerators.dll` and
their flavor-named `buildTransitive` props. The props allowlist
`ReactiveUI.Validation.Generated`; retain both analyzer and build assets in
the consumer. Recompile call sites using the pinned SDK 10.0.401/C#14. No
additional Validation generator package reference is needed. Inspect the real
compiler analyzer inputs/imported props if generation is absent.

## Inline normal calls

These fragments assume accessible user-declared notifying application types. A
property chain must consist of supported inline readable instance properties.
Observed chain owners must be reference types implementing
`INotifyPropertyChanged`; selectors used only as rule metadata do not require
observation. Store every returned helper/binding under an application owner
and dispose it at the matching lifetime boundary.

```csharp
var nameRule = model.ValidationRule(
    vm => vm.Name,
    static name => !string.IsNullOrWhiteSpace(name),
    "name-required");

var postcodeRule = model.ValidationRule(
    model.Advisory,
    vm => vm.Address!.Postcode,
    static postcode => postcode?.Length == 5,
    "postcode-required");

var nameText = editor.BindValidation(
    editor.ViewModel,
    vm => vm.Name,
    view => view.ErrorText);

var advisoryText = editor.BindValidationContext(
    editor.ViewModel,
    vm => vm.Advisory,
    view => view.AdvisoryText);

var richStatus = editor.BindValidationState(
    editor.ViewModel,
    vm => vm.AddressRule,
    view => view.Status,
    ProjectCompleteState);
```

`ProjectCompleteState` may return an enum or nullable custom struct. A helper
or property-state converter receives complete custom states, including code,
severity and revision; validity need not be inferred from message text.
Context aggregation keeps its normal aggregate-state contract. Named advisory
contexts remain independent from default blocking/error-export policy.

A null intermediate rule owner emits `default` (null for reference/nullable
values), which the predicate must handle. It does not preserve the old
postcode. Null model/helper/context selections instead emit valid aggregate
state or an empty property-state list. Typed/context property observations
seed actual membership and require initial states from active rules. Generated
normal property text bindings expose initial invalid text immediately without
a synthetic empty-list prelude for active rules. Explicit Unsafe property
callbacks retain the legacy prelude; migration changes that initial emission
sequence.

Strict property bindings match the exact full root path in both modes.
`strict: true` additionally excludes rules associated with more than that one
property. For a combined Email/Confirmation rule, retain one component with
both paths through the explicit observable API; two independent rules have
different membership and strict-match behavior.

For supported nested target selectors such as `view => view.Output!.Text`,
parent properties must have notifying reference-type owners and the final
ordinary setter must be accessible. Replacement receives the latest projected
output; a null target skips assignment and retains that output for the next
target. Callbacks and setters execute on the source notification thread.
Dispatch UI presentation explicitly; domain validity stays synchronous on its
model owner.

## Explicit generic observable bindings

The six safe observable methods now use traditional `this` extensions to avoid a
reproduced CS8714 error in SDK 10.0.401/Roslyn 5.9's nullable-generic C#14 static
bridge. Their CLR signatures and runtime behavior remain compatible. Inferred
calls are unchanged. If you previously supplied only the output type in source,
update both observable binding methods to supply source and output type:

```csharp
// Previous source spelling: sources.BindObservableValidationState<Presentation?>(...)
sources.BindObservableValidationState<Customer, Presentation?>(
    static customer => customer.AddressRule?.ValidationChanged,
    ProjectCompleteState,
    value => editor.Status = value);
```

`BindObservablePropertyValidationState<TSource, TOut>` needs the same two explicit
type arguments. `AddObservableRule<TValue>` keeps its single type argument. Using
inference avoids this source spelling change. The narrowly scoped SST1703 style
exception addresses the measured compiler bug; trimming/NativeAOT warnings remain
errors in the strict gates.

## Runtime selectors and precompiled consumers

A selector stored in a variable or passed as a parameter is not a supported
compile-time literal. Method calls, indexers, casts, non-notifying observation
owners, inaccessible/open generic call types and unsupported target shapes
produce actionable build errors. See the [diagnostic
contract](../generated-validation-design.md#selector-and-assignment-support).
For runtime expressions, explicitly use the Unsafe counterpart:

```csharp
Expression<Func<Customer, string?>> configuredSelector = GetSelector();
var configuredRule = model.ValidationRuleUnsafe(
    configuredSelector,
    static value => !string.IsNullOrWhiteSpace(value),
    "value-required");
```

The same choice is available as `BindValidationUnsafe`,
`BindValidationContextUnsafe` and `BindValidationStateUnsafe`. These execute
the retained runtime reflection path and retain `RequiresUnreferencedCode`
warnings. Their use is a deliberate trimming boundary, not a warning-free
migration. Alternatively, supply typed observable streams/full paths to
`AddObservableRule` and explicit selection streams/direct callbacks to
`BindObservableValidationState` or `BindObservablePropertyValidationState`.
Supplied-observable/metadata-only `ValidationRule` overloads retain their safe
runtime contract.

An already compiled normal call reaches the throwing normal stub if its
package is replaced. It cannot acquire an interceptor without recompilation.
Rebuild the consumer with the embedded analyzer/props for supported literal
calls, or change and rebuild the call against Unsafe. A final-compilation
analyzer reports a normal invocation with no interceptor as `RUVG005`,
including normal calls introduced by another generator; a normal
method-group/delegate reference produces `RUVG007`. Analyzer removal, indirect
normal calls or ignored diagnostics never activate a reflection fallback.

## Ownership and realistic executable cases

The [shared corpus](../../examples/GeneratedValidation/Program.cs) is compiled
by `examples/GeneratedValidation/Primitives/Examples.csproj` and
`examples/GeneratedValidation/Reactive/Examples.csproj` against actual
packages. It covers:

| Case | Contract |
| --- | --- |
| `initial-field-and-text` | Initial invalid state, direct notifying field updates and generated text binding. |
| `nested-null-and-replacement` | Nested rule replacement/null with default delivery and stale-source detachment. |
| `helper-model-rich-state` | Replaced/null model and helper, complete custom state and nullable typed presentation. |
| `context-replacement` | Independent selected contexts, replacement/null and captured registration ownership. |
| `property-state-membership` | Exact paths, strict matching, live rule membership and custom state projection. |
| `nested-target-handoff` | Text/typed output replay on parent replacement, equal-overriding reference identity, null-target retention and disposal. |
| `rows-owned-observable-results` | Caller-owned observable results, stable row lifetime and removal cleanup. |

Dispose a binding to detach its subscriptions; it does not dispose its
selected model, helper or context. Dispose each rule helper to remove its
component from the context captured at registration. Dispose helpers before
their contexts. Collection owners dispose removed row helpers and own request
cancellation; generation does not infer asynchronous work or cancellation from
a selector.

Generated notification sources own event registrations and use a serialized
pending-refresh drain to rebind complete chains after reentry. They detach on
replacement/null/disposal and initial getter/callback failure. Nested-target
binding support additionally installs owned pending subscription slots before
synchronous delivery, so obsolete returned handles cannot overwrite a later
selection. Initial failures retain the original error; cleanup failures are
reported alongside it. Caller-supplied observables keep their own
initial-failure cleanup obligations.

The gate checks all 18 distinct normal overload shapes and the absence of
Roslyn runtime assets/dependencies. Six isolated negative builds per flavor
cover a stored selector, computed selector, indexer, nonnotifying nested
target parent, nonnotifying rule parent and notifying struct owner. These are
diagnostic checks, separate from the seven behavioral case executions.

After bootstrapping the released dependency feed and checking the matching
environment, run the strict gate from the repository root:

```sh
direnv exec "$RUNIC_SDK" python3 eng/verify-generated-validation.py \
  --rid linux-x64 --mode all
```

By default the runner freshly packs current clean source into an isolated
feed. Use `--package-feed FEED` for an exact same-source prepacked pair, such
as the CI shipping artifact. `--output OUTPUT` selects retained evidence
(default `artifacts/verification/generated-gates`). `--mode` selects
`managed`, `trimmed`, `native` or `all`; `win-x64` runs on a matching Windows
native host. The runner verifies actual package identity and current clean
source, exact flavor/version graphs, emitted code, diagnostics and executed
output with warnings as errors. Native CI hosts must consume the same verified
Linux shipping artifact. A CLI option or prior passed release gate does not
establish execution on a platform. New CI/native evidence remains pending
until recorded for this revision. No blanket package `IsAotCompatible`,
retained UI-platform or bridge/browser support follows.
