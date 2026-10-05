# Validation generator design after the observable runtime path

Design date: **2026-10-05**, investigation base `e653e52`. The selected scope is
runtime APIs and realistic examples first. This document proposes a later
convenience generator; it does not add an analyzer package, generator-only stub
API, interceptor or production dependency. The [native investigation](aot-and-generators.md)
remains the source of current platform and released-package evidence.

## Decision and existing compiler evidence

Use application-created observables, static full property paths and typed
callbacks with the new `AddObservableRule`, `BindObservableValidationState` and
`BindObservablePropertyValidationState` APIs. Existing Binding generation already
reduces application observation boilerplate. A Validation generator should add
metadata and declared observation conveniences only after those APIs and their
package consumers are verified.

The [Binding compiler inputs](../investigations/BindingGenerators/evidence/Primitives-compiler.txt)
contain Binding **9.1.0** and ReactiveUI.SourceGenerators **4.2.0** with C#14 and
Binding's own interceptor namespace. Their
[emitted observation](../investigations/BindingGenerators/evidence/Primitives/WhenAnyValueDispatch.g.cs.txt)
contains direct property getters and notification mechanisms. Their
[emitted setter](../investigations/BindingGenerators/evidence/Primitives/BindToDispatch.g.cs.txt)
contains direct typed assignment. An application generator cannot change the
already compiled Validation package's internal Unsafe observation or reflected
setter. The released
[OAPH IL inspection](../investigations/BindingGenerators/evidence/released-oaph-il.txt)
also establishes that generator support already exists in this cohort; adding a
second generator is not a prerequisite for the safe observable APIs.

A small [managed compiler proof](../investigations/BindingGenerators/evidence/interceptor-annotation-proof.txt)
uses SDK **10.0.401**, C#14 and ILLink analyzers **10.0.12**. An unannotated
interceptor replaces a synthetic RDC/RUC-annotated `UnsafeApi.Read` call and executes the distinct
`safe-interceptor` result. The original call still produces **IL2026 and IL3050**.
This rules out promising zero warnings merely by intercepting existing annotated
Validation methods. Both diagnostics remain visible; the isolated proof records
warning severity to allow execution, with no suppression. It does not publish a
native executable or establish native behavior.

[Roslyn's interceptor design](https://github.com/dotnet/roslyn/blob/main/docs/features/interceptors.md)
defines compile-time call substitution and a dedicated allowed namespace. Its
[current API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.csharpextensions.getinterceptablelocation?view=roslyn-dotnet-5.0.0)
provides encoded locations rather than manually maintained source line numbers.
The managed proof tests the annotation behavior on the pinned toolchain instead
of inferring it from runtime reachability. Annotations justified by the producer audit remain; narrowing an overly broad
annotation is a separate runtime change.
[Microsoft's trimming guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/fixing-warnings)
treats warnings as evidence about supported inputs and recommends resolving the
underlying unsupported behavior.

## Runtime contract consumed by generated code

The exact additive contract has four rule overloads: state/value streams on
`IValidationContext`, and the same pair on `IValidatableViewModel`. The value
variant takes `Func<TValue,IValidationState>`; every variant takes explicit
`IEnumerable<string>` full paths. Generated calls use these APIs directly,
without expression overloads or a call to a runtime Unsafe method.

The caller supplies an initial value/state. Empty metadata means a model-wide
rule. `"Address.Postcode"` retains the full path; `"Postcode"` is a different
property. Paths are ordinal, dot-separated, nonempty and whitespace-free; an
empty segment is invalid. Multi-property metadata contains every associated full
path, with duplicates counted once. Strict property matching includes only a
rule exclusively associated with that one path. Registering two independent
rules is not a semantic replacement for one rule carrying two paths: exclusive
matching and membership differ.

The helper captures the registration context once, unregisters its own component
and disposes that component's subscriptions. It never owns the context or supplied
observable object. Dynamic row removal disposes the row's helper; application
ownership cancels a request if cancellation is required. No source generator can
infer a request's ownership or make a runtime-discovered collection static.

`BindObservableValidationState` accepts an outer nullable source stream, a
`Func<TSource,IObservable<IValidationState>?>`, a complete-state projection and
an ordinary `Action<TOut>`. The source may be a model, helper or context. A null
source/selected stream yields `ValidationState.Valid`. Active streams supply their
own initial state; no synthetic valid state precedes an active invalid rule.

`BindObservablePropertyValidationState` accepts an outer nullable source stream,
a direct context selector, full path, `Func<IList<IValidationState>,TOut>`, typed
callback and explicit strict flag. Null selection/no matching rules yields an
empty list. New membership and state changes remain live.

Selectors are delegates, not observations. The outer stream must report each
model, helper or selected context replacement. For example, a stream emitting
only `Editor.ViewModel` changes cannot discover `Customer.AddressRule` changing
inside the same model. Supply a stream of current helpers, or combine both
notification levels before calling the binding. Replacement switches and detaches
the old stream; binding disposal detaches all binding subscriptions. It does not
dispose the model/helper/context.

All custom `IValidationState` values pass through intact, including a boxed struct,
code, severity and revision. Generated code must not reconstruct a state from
text or filter updates only by validity/text. `Presentation?`, enums and other
custom target types are ordinary generic outputs. The supplied callback calls
the setter statically. UI dispatch belongs in that callback or a caller-chosen
presentation stream; domain validity remains synchronous on its owner.

## Realistic case mapping

The [NativeValidation corpus](../examples/NativeValidation/Program.cs) uses
notifying application types, rather than treating a non-notifying anonymous
object as a generator failure. Its expected-failure and migrated variants cover
the following contracts.

| Corpus case | Safe runtime implementation | Later generator contribution | Boundary |
| --- | --- | --- | --- |
| `generic-field` | Reusable field takes `IObservable<TValue>`, complete-state delegate and static paths; direct notification adapter observes `Customer.Email`. | Generate a declared Email observation and `"Email"` metadata at the model declaration. | An `Expression<Func<TModel,TValue>>` received as a parameter is not a literal selector available to the generator. No reflection fallback. |
| `nullable-editor-rich-target` | Observe `Customer.Address` then the current `Address.Postcode`; null address explicitly emits a value mapped to invalid state. Observe current editor model and its helper replacements; project to `Presentation?` and assign `Editor.Status` directly. | Generate the typed notification chain and full `"Address.Postcode"` path, with explicit null behavior. | A delegate reading `AddressRule` does not observe helper replacement. A generated nested chain that emits nothing on null leaves stale validation. |
| `blocking-advisory-cross-field` | Observe both Email and Confirmation, register a combined rule with both paths in the blocking context; register hint separately in advisory context. | Generate ordered full-path metadata and declared field observations; retain the supplied matching state/predicate. | Advisory rules do not automatically become `HasErrors` or command-admission rules. Strict matching excludes a multi-property rule. |
| `rows-async-uniqueness` | Register each supplied state stream; retain rich `UniquenessState` and row helper ownership, detach on removal and ignore late removed results. | At most generate static field metadata for a declared row model. | Request execution/cancellation, stable row identity and runtime membership remain application code. |

The corpus also needs direct property error export (`GetErrors`), `HasErrors`,
initial invalid state, replaced/null sources and cleanup evidence. A generator
is not successful merely because emitted C# compiles. Its generated observation
must reproduce the migrated corpus behavior with the same warning policy and
matching released package graph. Current native execution evidence belongs to
the package consumer gate, not this design document.

## Recommended future declaration and emitted shape

Prefer attributes on explicit partial methods over interception of legacy calls.
A declaration can require a supplied stream and ask the generator to provide only
validated metadata. The syntax below is a **proposed future declaration**, not
an installed API:

```csharp
// Future analyzer-owned attribute; no runtime stub fallback.
[ValidationPaths("Email", "Confirmation")]
private partial ValidationHelper AttachMatching(
    IValidationContext destination,
    IObservable<IValidationState> states);
```

The generated implementation uses the actual runtime API:

```csharp
private partial ValidationHelper AttachMatching(
    IValidationContext destination,
    IObservable<IValidationState> states)
{
    return destination.AddObservableRule(states, new[] { "Email", "Confirmation" });
}
```

For example, the following is the concrete generated-operation target for the
corpus, using its existing application adapters and the actual runtime APIs:

```csharp
var emailValues = new PropertyValues<string>(
    customer, nameof(Customer.Email), () => customer.Email);
var emailRule = customer.AddObservableRule(
    emailValues,
    static email => new ValidationState(email.Contains('@'), "email-required"),
    new[] { "Email" });
var postcodeRule = customer.AddObservableRule(
    new NestedPostcodes(customer),
    static postcode => new ValidationState(postcode?.Length == 5, "postcode-required"),
    new[] { "Address.Postcode" });
var presentation = new HelperSelections(editor).BindObservableValidationState(
    static helper => helper.ValidationChanged,
    Project,
    value => editor.Status = value);
```

`PropertyValues`, `NestedPostcodes`, `HelperSelections` and `Project` here are
application-owned corpus code, not assumed library APIs. A future observation
generator emits equivalent typed notification/subscription operations with the
handoff safeguards below, or accepts caller streams under their declared
observation contract. The snippet never asks another generator to transform its
emitted code. The model owner retains and disposes `emailRule`/`postcodeRule`;
the editor owner retains and disposes `presentation`.

For an observation declaration, the initial supported input should be real
user-declared readable properties and `INotifyPropertyChanged` on each observed
node. The generated method can use the same contract as the corpus's
`PropertyValues<T>` adapter: attach a notification handler, emit the direct getter
initially and on matching/empty property-name notifications, and detach on
subscription disposal. It may emit that adapter itself. It must not silently
infer notifications for a plain object. Nested chains switch subscriptions and
must explicitly emit the declared null value when a parent is null. Full paths
come from the original root, even though notification subscriptions attach to
individual segments.

Binding's retained nested-chain output returns an empty inner signal when a
parent is null. That output is valid evidence for the tested Binding behavior;
it does not meet this corpus's policy that a missing address becomes invalid.
The Validation observation declaration must specify the null policy explicitly,
or the caller must supply a null-aware observable. This is a distinct choice
from null *binding source*, whose documented projection is valid/empty.

### Synchronous subscription handoff and initial failure

The corpus now explicitly checks its application adapters' synchronous initial
handoff and failure cleanup in
[ApplicationAdapterChecks](../examples/NativeValidation/ApplicationAdapterChecks.cs),
called by the safe `generic-field` case. It covers reentrant Address/editor-model
replacement and null, latest/outer detachment, owner disposal during initial
delivery, and initial getter/callback exception cleanup. These checks validate the
application adapters; no generator is shipped, and equivalent generated-code
acceptance remains required before its observation infrastructure is supported.

Earlier `NestedPostcodes` and `HelperSelections` implementations used a naive
`inner = Subscribe(observer)` assignment after synchronous initial delivery. If
that delivery replaces
the parent, a reentered subscription can become current, then be overwritten by
the older subscription's returned handle. Reentry to a null parent can similarly
leave the old subscription attached. The corrected application adapters install
pending slots before subscribing and guard notifications by current slot identity
and disposal state. A generated adapter must likewise install an owned pending
assignment slot before subscribing. Replacements dispose that slot via a
serial owner; when an obsolete `Subscribe` finally returns, assigning its handle
to its already-disposed slot disposes it immediately instead of overwriting the
current slot. A generation token/disposed guard rejects notifications from a
superseded or disposed generation. This is a subscription ownership requirement
within the serialized owner model, not a promise of concurrent mutation support.

Initial failure requires source-side cleanup too: a subscription that attaches an
event handler and then throws during its getter/initial `OnNext` returns no handle
for its caller to dispose. Generated notification sources must construct cleanup
before initial delivery and detach their handlers if that delivery fails, then
propagate the original error. Generated nested sources must also dispose all
already-installed outer/inner pending slots on construction/subscription failure.
A pending slot alone cannot recover a leaked handler inside an arbitrary supplied
source whose `Subscribe` throws before returning; caller-created sources keep
their own exception-cleanup contract.

Deferred generated-adapter acceptance must reproduce the corpus handoff checks:

| Trigger during synchronous initial delivery | Required evidence |
| --- | --- |
| Initial postcode callback replaces Address; initial helper callback replaces editor ViewModel | Latest source remains owned; subsequent old-source changes emit nothing; disposal detaches both old and latest handlers. |
| Initial callback changes the parent to null | Explicit null policy is delivered; the returning old subscription is disposed; old-source changes emit nothing. |
| Initial callback disposes an owner whose pending subscription slot is already installed | Returned handles are disposed immediately; after `Subscribe` returns, no handler or later callback survives disposal. |
| Initial getter or callback throws after event attachment, including a nested subscription | Original error propagates; all task-owned outer/inner handlers are detached despite no returned subscription handle. |

A target generator is optional: a caller-written `value => editor.Status = value`
already has typed reachability and supports nullable/custom targets. Generating
that one assignment adds little value. Generating a notification chain and its
full metadata can remove substantially more repetitive code.

## Compiler pipeline, diagnostics and support limits

[Roslyn's source-generator design](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.md)
states that ordinary generators see the same input compilation and cannot access
one another's generated files. Emitting a fresh `WhenAnyValue(...)` call from a
Validation generator and assuming Binding will subsequently intercept it is not
a supported pipeline. Emit direct notification code, accept caller observables,
or refer to a generated member whose contract is declared in the original source
and verified in the final compilation. Do not depend on generator execution order.

The initial design supports user-declared properties. ReactiveUI `[Reactive]`
field-to-property output is a separate compatibility feature: it requires a
versioned understanding of the source declaration or an explicit user-authored
contract. The new generator must not claim semantic discovery of a property
that exists only in another generator's output. Private/nested types require
partial containing types for hosted implementations; generic declarations must
retain their type parameters and constraints.

A proposed generator must emit actionable errors for these inputs, with no
reflection or Unsafe fallback:

| Diagnostic category | Required explanation/action |
| --- | --- |
| Selector is a variable, parameter or runtime configuration | Supply a typed observable and full metadata, or move the static declaration to the model. |
| Method call, indexer, computed member or unsupported conversion in a selector | Declare supported readable property paths or provide a custom observable. |
| A chain node lacks a supported notification contract | Provide an observable reporting changes; a plain anonymous object is not notifying. |
| Missing/inaccessible property or type only generated by another generator | Declare the contract in user source or enable a specifically tested compatibility mapping. |
| Nullable parent without a null policy | Declare what null emits; do not preserve an old value silently. |
| Invalid path or inconsistent multi-property metadata | Identify the segment/path and require a complete ordinal root path. |
| Missing partial container or mismatched declaration signature | Identify the declaration that needs a partial container or supported signature. |
| Both flavor assemblies or unsupported runtime API version | Require exactly one matching flavor and the tested API cohort. |

Runtime-configured names, plugin-discovered models, arbitrary reflection-only
members and request lifetimes are not compile-time inputs. Supplying explicit
observables remains the supported escape route. Intentionally using a legacy
Unsafe/expression path retains its legitimate RUC/trimming warnings. The current
producer audit removes overly broad RDC annotations; the synthetic proof above
tests both annotation kinds independently, and the released-cohort evidence is
dated historical behavior.

## Alternative costs and acceptance criteria

| Approach | Benefit | Cost and decision |
| --- | --- | --- |
| Existing Binding-generated application observations plus safe Validation APIs | Uses current analyzers and direct setters; no new package. | Best starting point for supported literal observation shapes. Stored selectors/null policies need explicit adapters. |
| New interceptor for existing legacy Validation calls | Familiar syntax for compile-time literals. | Original RDC/RUC analyzer diagnostics persist on the pinned compiler; does not solve opaque selector parameters or compiled package internals. Defer. |
| New safe literal-rule interceptor surface | Can couple selector metadata and generated observations. | Adds a separate public API, namespace configuration and a required generation/failure contract; ungenerated fallback cannot safely reflect. Reassess only after the runtime contract is stable. |
| Attributes and explicit partial methods | No unsafe runtime stub; missing generation fails compilation; clear static inputs and emitted implementations. | New analyzer packaging, IDE diagnostics, both-flavor snapshots and compilation tests. Recommended form for a later narrowly scoped metadata/observation generator. |

A later implementation should start with full-path/multi-property metadata and
single declared notifying-property observation, then nullable chains after
replacement/null policy tests. Required acceptance includes both flavors,
private/nested partial hosts, generic types, custom struct state identity,
nullable enum/struct targets, metadata strictness, initial state and
model/helper/context replacement/disposal. Negative compilation tests must
assert diagnostic IDs and locations. Snapshot checks must verify emitted direct
getters/setters and the absence of calls to expression/Unsafe APIs. Final package
consumers must publish and execute the safe subset with no IL2026/IL3050/trim
suppression; producer analysis must continue to preserve warnings on genuinely
unsafe surfaces. Native platform claims require the corresponding actual gate.

## Reproducing the managed annotation proof

The [source bundle](../investigations/BindingGenerators/evidence/interceptor-annotation-proof-sources.json)
contains the exact standalone inputs, including the SDK pin and isolated
warning-severity settings. Extract it into a task-owned temporary directory,
reusing the locked SDK environment and dependency cache. Run:

```sh
direnv exec "$RUNIC_SDK" dotnet run --project "$PROOF_ROOT/Emitter/Emitter.csproj" \
  -- "$PROOF_ROOT/Consumer/Program.cs"
direnv exec "$RUNIC_SDK" dotnet run --project "$PROOF_ROOT/Consumer/Consumer.csproj"
```

The first command emits the modern interceptor location used in the bundled
consumer. The second retains the two original-call warnings and prints
`safe-interceptor`. Remove task-owned `bin`/`obj` after inspection; retain the
source and log when comparing a later compiler/analyzer version.
