# Generated validation and explicit Unsafe migration

**UNRELEASED; expanded source-stage migration, 2026-10-06.** The
[capability ledger](../generated-capabilities-progress.md) records landed code and
focused checks; expanded package/native acceptance remains pending. No capability
row is complete. The earlier restricted split's tested source is `2550376b230ccfb78beb8d3b4ced8866b65d809e`, distinct from this later
documentation record. The immutable `.790.17.15` release retains its earlier
runtime behavior. The [verification record](../upstream/reviews/2026-10-implementation.md#unreleased-generated-api-follow-up)
identifies the new local/CI packages and executed hosts separately.

## Consume the matching core package

Use `Runic.ReactiveUI.Validation` with ReactiveUI/Runic.DynamicData, or
`Runic.ReactiveUI.Validation.Reactive` with ReactiveUI.Reactive/
Runic.DynamicData.Reactive. Keep exactly one matching flavor. The
corresponding imports are `ReactiveUI.Validation.Extensions`, `.Contexts`,
`.Helpers`, `.Abstractions` and `.States`, or the same roots below
`ReactiveUI.Validation.Reactive`. The shipped dependency cohort remains
ReactiveUI 26.0.1 and released DynamicData 10.0.0-runic.30.

Both core packages embed `ReactiveUI.Validation.SourceGenerators.dll` and
their flavor-named `buildTransitive` props. The props allowlist
`ReactiveUI.Validation.Generated`; retain both analyzer and build assets in
the consumer. Recompile call sites using the pinned SDK 10.0.401/C#14. No
additional Validation generator package reference is needed. Inspect the real
compiler analyzer inputs/imported props if generation is absent.

## Inline normal calls

These quick-start fragments use accessible notifying application types and
inline readable property paths. Expanded semantic/typed routes follow below.
Observed chain owners must be reference types implementing
`INotifyPropertyChanged`; selectors used only as rule metadata do not require
observation. Binding view static types must be reference types implementing
`INotifyPropertyChanged`. Concrete struct views fail with `RUVG006`, because
notification boxing and captured setter copies cannot preserve ownership. A
stable box referenced through a supported notifying view interface is supported;
both shapes have both-flavor compiler regressions. Store every returned
helper/binding under an application owner and dispose it at the matching lifetime boundary.

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

Normal source calls now have semantic typed lowering and known producer projection.
Fields, conversions, computed reads and indexed storage are separated from their
notification and assignment contracts. Arbitrary method effects need supplied
dependencies; arbitrary target conversions need a supplied inverse/replacement.
Private/generic access uses legal bridges or typed factories. No notification is
inferred for a silent owner and no temporary struct copy becomes stable storage.
The [design](../generated-validation-design.md#selector-and-assignment-support)
explains exact boundaries and versioned producer profiles.

A stored selector, delegate or compiled peer can deliberately select registered
normal dispatch. Attach a finite `ValidationPlanRegistry` to the appropriate
source/view owner, register role-specific typed selector/target plans and retain
its attachment/registration leases. New source callers declare
`ValidationRuntimeDispatchAttribute`. Instance entries match one expression;
`RegisterSelectorPattern`/`RegisterTargetPattern` can match fresh expressions
against finite schemas. Factories bind each invocation's current receiver and
explicit typed arguments. Unknown closure fields are not reflected; supply a
legal typed extractor/provider. Ambiguity or missing registration fails
actionably. The compiled-peer ABI/native fixture is pending acceptance; replacing
an already built native executable's DLL cannot add this behavior.

A stored selector can keep the familiar rule call with an explicit typed
registration. This example assumes a notifying `Customer.Name` and a caller
method carrying the opt-in; imports include `.Capabilities`, `.Contexts`,
`.Extensions` and `System.Linq.Expressions` (plus `.States` for the later recipes):

```csharp
[ValidationRuntimeDispatch]
static void ValidateConfigured(Customer model, IValidationContext context,
    Expression<Func<Customer, string?>> configured)
{
    ValidationPath[] names = [ValidationPath.Legacy("Name")];
    var typed = ValidationSelector.Create(model, current =>
        ValidationAccessPlan.Create(
            () => ValidationRead.Present(current.Name, names),
            [ValidationDependency.PropertyChanged(() => current, nameof(current.Name))]));
    using var plans = new ValidationPlanRegistry(capacity: 1);
    using var attachment = plans.Attach(model);
    using var registration = plans.RegisterSelector(
        ValidationPlanRole.RuleValue, configured, typed);
    using var rule = model.ValidationRule(context, configured,
        static value => !string.IsNullOrWhiteSpace(value), "name-required");
    model.Name = "Ada";
}
```

The application promises that `configured` denotes the registered Name operation;
the supplied typed body executes that operation. This is instance registration,
not automatic interpretation or the fresh-expression precompiled pattern proof.
The helper owns its live rule; registration/attachment leases own future lookup
availability. A longer-lived rule must keep its helper alive beyond this method.

For immutable source aliases, generation can follow a proven fixed definition.
It must not treat a static abstract/virtual interface factory's default body as
the selected implementation, or a readonly field initializer as final when a
static constructor rewrites/escapes it. Those runtime choices use this explicit
typed registration/provider route. Supply the selected operation's typed body
at each invocation; do not cache one implementation under every same-shaped
expression. Passing a mutable local through an implicit `in` parameter can escape
it even without an `in` token. Constructed generic definitions preserve the
selected operators/conversions, checked/lifted/reference equality, `nameof(T)`
constant and finite block-return semantics. These distinctions have a 70-case
managed planner/boundary/generic proof; final package/native acceptance is pending.

For finite generated aliases, an exact registered match takes precedence over the
compiled typed descriptor. Only genuine absence uses that descriptor. Ambiguity,
mismatched matched plans and factory failures still fail; opaque runtime choices
require explicit registration.

Explicit Unsafe remains available for deliberately reflected expressions:

```csharp
Expression<Func<Customer, string?>> configuredSelector = GetSelector();
var configuredRule = model.ValidationRuleUnsafe(
    configuredSelector,
    static value => !string.IsNullOrWhiteSpace(value),
    "value-required");
```

It retains `RequiresUnreferencedCode`; it is not the warning-free typed route.
Supplied streams/full paths to `AddObservableRule` and direct callbacks through
observable binding APIs remain usable. Ordinary uncovered calls get final
`RUVG005`, indirect references `RUVG007`; explicit runtime opt-in does not supply
a registration or hide reflection. Existing compiled managed IL needs either
preserved-ABI typed registration or recompilation/migration.

## Typed recipes for explicit capabilities

These fragments use landed public names and assume caller-owned `context` and
application types. Their expanded actual-package/native proof is pending. Use
`ReactiveUI.Validation.Capabilities`, `.Contexts` and `.States`; the Reactive
flavor adds `.Reactive` after `.Validation` for all three imports.

A silent mutable owner can be held in a reference cell. The read delegate captures
current configuration legally; replacing the cell or explicitly invalidating it
publishes a new complete value/path snapshot. `Draft.Name` is an ordinary field,
not an INPC promise. The predicate includes the current captured minimum in its
selected value so equality cannot suppress a configuration change.

```csharp
var storage = ValidationCell.Create(new Draft());
var minimum = 3;
ValidationPath[] paths = [ValidationPath.Legacy("Name")];
var selector = ValidationSelector.Create(storage, cell =>
    ValidationAccessPlan.Create(
        () => ValidationRead.Present(
            (Name: cell.Value.Name, Minimum: minimum), paths),
        [ValidationDependency.PropertyChanged(() => cell, nameof(cell.Value))]));
using var rule = ValidationRuntime.RegisterRule(storage, context, selector,
    value => new ValidationState(
        value.Name?.Length >= value.Minimum, "name-too-short"));
storage.Value.Name = "Ada";
storage.Invalidate();
minimum = 4;
storage.Invalidate();
```

For an immutable record struct `Form(string? Name)`, a typed lens returns the
complete replacement rather than changing a boxed copy. The same contract works
for an immutable reference record with a supplied replacement callback.

```csharp
var form = ValidationCell.Create(new Form(null));
var name = new ValidationLens<Form, string?>(
    value => ValidationRead.Present(value.Name, paths),
    static (value, next) => value with { Name = next });
using var formRule = ValidationRuntime.RegisterRule(
    form, context, name.Selector(),
    static value => new ValidationState(!string.IsNullOrWhiteSpace(value), "name-required"));
var outputTarget = name.Target(); // typed copy-back factory, no subscription yet
form.Value = new Form("Ada");
```

Bind an application-owned `IObservable<string?>` with
`using var output = outputTarget.Bind(form).Bind(values)` to write through the
current cell. Replacements are constructed from current storage; keep the
transformation pure and return all enclosing struct/immutable values.

For a dynamic selected index, build the value and structural path from that index
in the same read. Register the collection/index-owner dependencies explicitly.
A custom provider is a typed application subscription, not library discovery:

```csharp
var changes = ValidationDependency.Create(
    () => table,
    static (owner, observer) => owner.SubscribeInvalidation(observer));
var indexed = ValidationSelector.Create(table, owner =>
    ValidationAccessPlan.Create(() =>
    {
        var index = owner.SelectedIndex;
        ValidationPath[] current = [ValidationPath.Structural(
            $"Rows[{index}].Name", index, EqualityComparer<int>.Default)];
        return index >= 0 && index < owner.Rows.Count
            ? ValidationRead.Present(owner.Rows[index].Name, current)
            : ValidationRead<string?>.Missing(current);
    }, [changes]));
```

Here `table.SubscribeInvalidation` is the application's adapter contract: it must
notify index/item/source changes and return cleanup for that registration. A
metadata-only property binding additionally supplies `readPaths` so matching does
not execute the leaf getter. Choose DefaultValue/Suppress/Fallback deliberately;
present null leaves remain distinct. Missing-owner Suppress must detach stale
inner selections without emitting presentation; final integrated proof remains
tracked separately.

`ValidationInitialSequence.LegacyEmpty`/`LegacyValid` are explicit presentation
compatibility options; normal syntax keeps actual initial state. `ValidationOutput`
adds output-type-first convenience, while `ValidationSnapshot.Read`/`Validate`
provides a synchronous scoped borrow for ref structs. Init/readonly access
selects actual existing storage using `GeneratedValidationAccessAttribute`, or
uses a supplied replacement; getter-only backing fields are never guessed.
Owned asynchronous/row policies use `ValidationAsync.ForLatest`,
`ValidationCollection.Observe` and `ValidationRowLease`. Dispatch presentation
through `ValidationScheduling`, keeping domain admission synchronous. These
APIs do not infer network/cancellation policy or native UI support.

Optional/default/params index arguments compile in the locked C#14 cohort.
Omitted enum or nullable-enum defaults retain an exact typed conversion.
Reordered named index arguments in an expression tree instead produce CS9307.
Use a legal typed delegate and explicit dependency/path plan for that case:

```csharp
// The typed delegate is legal; the same body in Expression<Func<...>> is CS9307.
Func<Table, string> read = owner => owner[second: owner.Second, first: owner.Key];
var namedIndex = ValidationSelector.Create(table, owner =>
    ValidationAccessPlan.Create(
        () => ValidationRead.Present(read(owner), paths),
        [ValidationDependency.Create(() => owner,
            static (current, observer) => current.SubscribeInvalidation(observer))]));
```

Here `paths` and `SubscribeInvalidation` are supplied application contracts, as
in the dynamic-index recipe. The boundary corpus executes the delegate and
typed read/metadata/write plans, checks argument order `Second` then `Key`, and
counts getter execution. This supplies usable typed behavior without claiming
that the compiler accepts the rejected expression tree.

A silent target replacement is resolved again on the next domain output; the
new output is assigned to the current target. Immediate replay of cached output
requires a declared invalidation/notification adapter. The same owned engine
retains `AfterRead` caches and handles reentry, without fabricating an event or
replaying an old output into a silently replaced target.

## Choosing a feasible typed route

These alternatives preserve explicit access, lifetime and notification contracts.
They do not promise automatic behavior that lacks a source/runtime contract.
Final package/native acceptance remains pending.

| Application pattern | Implemented public route and exact boundary |
| --- | --- |
| Silent POCO, field or captured configuration | `ValidationSelector.Create` plus typed read/dependencies; `ValidationCell.Invalidate` or an authored `ValidationDependency.Create` adapter reports changes. A dependency-free plan reads the initial snapshot; silent mutations have no inferred events. |
| Runtime selector/factory choice, static interface dispatch or rewritten readonly selector | `ValidationPlanRegistry`/`IValidationPlanProvider` with invocation-current typed factories. Immutable provenance may be generated; a default body/initializer is not proof of runtime selection. |
| Fresh expressions from unchanged precompiled IL | Finite structural `ValidationExpressionPattern` registrations with explicit typed arguments and scoped leases. Unknown closure fields need supplied extractors/provider exports; the library does not read them reflectively. Final historical-peer execution is still pending. |
| Generic/private/file-local or anonymous result shape | Typed factory in the caller's legal context, inferred output and lexical/provider bridge where possible. An external assembly's inaccessible members require its exported typed contract or an exact statically supported accessor; no universal object-erasure route exists. |
| Nested mutable structs or immutable replacement | `ValidationCell<T>` and `ValidationLens<TStorage,TValue>` return complete outward write-back from current storage. A by-value stack root/temporary unbox cannot become stable owned storage after the call returns. |
| Init setter, named readonly field or get-only property | Explicit `GeneratedValidationAccessAttribute` selects actual supported existing storage; otherwise supply a typed replacement/target factory. Getter-only backing storage is never guessed. Read MaybeNull/NotNull and setter AllowNull/DisallowNull contracts are independent, including Nullable<T>; accessor29 and binding80 managed proofs cover their exact flow/assignment behavior, with package/native acceptance pending. |
| Readable getter with a protected setter | A getter through a base-typed receiver does not grant setter access. Supply an authored target/setter in a legal derived context, preserving receiver legality rather than inventing access. |
| Ref-struct/borrowed stack data | `ValidationSnapshot.Read`/`Validate` borrows synchronously. A long-lived heap subscription cannot retain a ref-struct borrow; copy intended data into explicitly owned ordinary storage for live observation. |
| Generated controls/members and UI notification | Exact producer projection or an authored `ValidationViewAdapter`/typed getter/provider using final producer members. No ordering is created by bundling generators; known profile/compiler proof is not an executed native UI host. |

Legacy path compatibility is explicit: `ValidationPath.Legacy("Address.Name")`
selects that full ordinal name even against structural query metadata. Two
Structural paths with the same display name still use their distinct physical/
index/conversion identities and comparer domain; equality/hashing is unchanged.
Prefer Structural metadata when those distinctions matter. A legacy `"Name"`
does not match `"Address.Name"`, and strict matching still requires exclusivity.

## Ownership and historical executable cases

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

The earlier tested `2550376` gate checked all 18 distinct normal overload shapes and the absence of
Roslyn runtime assets/dependencies. Six isolated negative builds per flavor
cover a stored selector, computed selector, indexer, nonnotifying nested
target parent, nonnotifying rule parent and notifying struct owner. These are
diagnostic checks, separate from the seven behavioral case executions. Expanded
acceptance adds the eleven static factories, actual producer and typed/registered
cases, with final current-source execution still pending in the capability ledger.

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
establish execution on a platform. At tested `2550376`, local Linux execution
passes all six flavor/stage runs (42 behavioral cases) and 12 negative builds.
[CI 37386897109](https://github.com/Runic-Artifex/ReactiveUI.Validation/actions/runs/37386897109)
repeats managed, full-trim managed and actual native generated execution on both
Linux x64 and Windows x64, with 42 cases and 12 negative builds per RID. Every
emitted stage covers all 18 normal overloads; positive paths have zero
warnings/errors. Both RIDs use the same verified Ubuntu package artifact, whose
hashes differ from the local verification pair. See the
[structured evidence](../upstream/evidence/generated-api-implementation.json).
To reproduce those exact identities, use a clean checkout of the tested source;
a fresh gate at a later HEAD records its own source/package identity. The new
source-landed AOT flag still requires final flagged-package acceptance; retained
UI-platform or bridge/browser support does not follow.
