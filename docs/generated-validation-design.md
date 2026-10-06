# Generated validation, typed plans and explicit Unsafe APIs

The generated/Unsafe API split is **unreleased**: it is on the maintained branch
but not yet in a published package. This document describes its design. The
[capability reference](generated-capabilities.md) lists every supported shape
and its alternative; the [migration guide](examples/generated-validation.md)
shows code.

## Generated, typed and registered routes

Four predicate `ValidationRule` overloads, fourteen binding extensions
(`BindValidation`, `BindValidationContext`, `BindValidationState`) and the
`ValidationBinding.For*` static factories are *normal* methods. Each original
`Expression` overload has a typed `Func` counterpart with higher overload
priority, so inline lambdas bind to the `Func` form without building an
expression tree. The generator lowers eligible calls through typed access,
observation and assignment plans and emits interceptors in
`ReactiveUI.Validation.Generated`. Explicit typed factories have real callable
bodies where interception cannot express the required access or lifetime.

Normal methods also support deliberately registered runtime dispatch.
`ValidationPlanRegistry` has finite capacity, owner attachment and registration
leases. `IValidationPlanProvider` receives role-specific `ValidationPlanRequest`s
using the original API's generic slots; it never discovers members. Source
callers opt in with `ValidationRuntimeDispatchAttribute`, a compiler-visible
contract, not a warning suppression. An unregistered normal call fails
actionably; there is no hidden reflection fallback.

`ValidationRuleUnsafe`, `BindValidationUnsafe`, `BindValidationContextUnsafe`,
`BindValidationStateUnsafe` and the static-factory Unsafe routes keep reflected
observation/assignment with accurate `RequiresUnreferencedCode`.
Supplied-observable/metadata-only rules and the explicit observable APIs keep
their safe runtime contract.

## Compiler and package delivery

Both branded core packages embed the analyzer at
`analyzers/dotnet/roslyn5.9/cs/ReactiveUI.Validation.SourceGenerators.dll` and
flavor-named `buildTransitive` props that add `ReactiveUI.Validation.Generated`
to `InterceptorsNamespaces` and expose producer-version properties. Consumers
must keep both assets and compile with SDK 10.0.401 / C# 14 (Roslyn 5.9); no
separate generator package is needed. Primitives uses
`ReactiveUI.Validation.Capabilities`; Reactive uses
`ReactiveUI.Validation.Reactive.Capabilities`.

Generic interceptors keep the original method arity and constraints inside a
nongeneric containing type, because an interceptor cannot be hosted in a generic
type. Legal lexical partial bridges or typed providers reach private and open
generic contexts. Non-partial, external, anonymous or file-local contexts use
authored typed factories; generation does not erase types to `object`.

For C# 14 extension-form calls, constraints come from the bound logical extension
declaration and map by ordinal to the CLR shim's generic slots. This avoids
copying the shim's incorrect `notnull` metadata
([Roslyn #79896](https://github.com/dotnet/roslyn/issues/79896)). Static-form
calls use the constraints of the method the compiler bound.

An interceptor replaces the call but not the evaluation of its arguments.
Interception also cannot remove trimming/AOT warnings from an annotated public
entry point, which is why reflection lives in separately named Unsafe methods
rather than behind annotations on the normal ones. When an `Expression` argument
itself needs dynamic code (for example an expanded params indexer), the analyzer
reports `RUVG011` and NativeAOT reports IL3050; use the `Func` overload instead.

## Observable extension compatibility

The six safe observable APIs use traditional `this` extensions because the C# 14
synthesized static bridge produces a nullable-generic `CS8714` error. CLR
signatures and inferred calls are unaffected. Explicit observable binding calls
spell `<TSource, TOut>`; `AddObservableRule<TValue>` keeps its arity.
`ValidationOutput<TOutput>.FromStates` and `FromPropertyStates` provide
output-first factories with source inference. The scoped SST1703 exception does
not suppress IL warnings.

## Selector and assignment support

The semantic planner separates readable operations, notification dependencies,
path metadata and writable storage. Property and field paths, conversions,
computed expressions and index arguments use typed getters. A conversion has no
inferred inverse setter. Opaque method effects, silent owners and unknown
notification providers need explicit typed dependencies, manual invalidation or
a snapshot: the library cannot observe a notification that does not exist.

`ValidationSelector<TSource,TValue>` binds to `ValidationAccessPlan<TValue>`.
`ValidationRead<TValue>` carries value, owner availability and current paths in
one snapshot; a present null leaf differs from a missing parent.
`ValidationDependency.Create` takes a reference owner getter and a typed
subscription callback; property and collection adapters are explicit. Cold
`CreateObservation` factories give per-subscription caches, and `AfterRead`
handles dependencies on owners cached by that read. Metadata-only binding
selection reads paths without executing the leaf getter. Structural path changes
count even when values compare equal.

`ValidationPath.Structural` identifies member, index and conversion segments.
`ValidationPath.Legacy(fullPath)` deliberately selects ordinal display-name
membership, including against a structural path of that exact name. It does not
change structural equality or make `Name` match `Address.Name`.

`ValidationTarget<TSource,TOut>` binds to `ValidationWritePlan<TOut>` with stable
reference/slot identity and a typed assignment. Null targets skip assignment and
cache the latest output. A declared target replacement replays that output
immediately; every new domain output also re-resolves the current target, so a
silently replaced target receives the next output without a fabricated event.
`ValidationCell<T>` owns mutable structs or immutable roots by reference.
`ValidationLens<TStorage,TValue>` reads current storage and supplies a complete
replacement. A by-value stack struct cannot be retained by a binding; use cells,
lenses or the synchronous `ValidationSnapshot.Read`/`Validate`, which also accept
ref structs without creating subscriptions.

Private/protected access is checked in its original legal context: a readable
getter does not make a protected setter legal through a base-typed receiver.
`GeneratedValidationAccessAttribute` selects actual property, field or accessor
storage for static bridges, including existing init setters and named readonly
fields; it never infers a getter-only backing field. .NET 10 accessor bridges keep
exact declaring types, generic positions and constraints. `UnsafeAccessorType`
can erase an inaccessible reference owner with a nameable field type;
inaccessible value-type owners or field types need lexical getters, lenses or
replacement. Bridges preserve `MaybeNull`/`NotNull` read flow separately from
`AllowNull`/`DisallowNull` setter input, including `Nullable<T>`.

Shared storage uses `ValidationStoragePolicy` and disposable
`ValidationWriteReceipt`s. Custom plans attach `ValidationDependency.Writer(receipt)`
for queued-failure cleanup. Peer writes are serialized on the source owner and
read current storage before copy-back; direct mutation during replacement
construction is a conflict. Arbitrary concurrent mutation is not supported.

## Runtime selectors, captures and precompiled consumers

Selectors whose value is provably fixed are generated. Mutable runtime choices,
method groups and precompiled calls use typed factories or registered
catalog/provider dispatch. Instance registration identifies one expression;
structural pattern registration matches fresh expressions against predeclared
operations and binds the current receiver and typed arguments on every call.
Registration is bounded and scoped; ambiguity fails deterministically.

Fixed provenance must cover actual dispatch and storage. A static
abstract/virtual interface factory can run another implementation, a readonly
selector field can be reassigned or escape from its static constructor, and an
implicit `in` argument can expose a mutable local. These use registered routes.
Constructed generic definitions keep their operators, conversions,
checked/lifted/reference-equality semantics, `nameof(T)` and block returns.

For generated aliases, an exact registered plan wins; only genuine absence
selects the compiled typed descriptor. Ambiguity, mismatched plans and factory
failures propagate. Reordered named index arguments in an expression tree are
rejected by C# 14 (`CS9307`); the `Func` overload or an authored getter with
explicit dependencies is the alternative.

`ValidationExpressionPattern` inspects finite expression structure and member
identities. It never compiles or evaluates trees, reads closure fields through
reflection or constructs arbitrary generic types. Private compiler-generated
captures need a typed extractor, explicit closure binding or a peer-exported
provider; unknown captures fail actionably, and shape matching never freezes the
first invocation's receiver or capture.

Package replacement cannot add interceptors to old IL. Registered normal methods
serve unchanged precompiled managed callers through preserved CLR signatures. An
already built native executable cannot gain this by replacing a managed DLL; it
must be relinked. Recompilation, typed registration, supplied observables and
explicit Unsafe are the available choices.

## Peer-generated members

Roslyn generators see the consumer's source and fixed post-initialization
output, but not each other's normal source output. Bundling analyzers together
or changing registration order creates no ordering, and Roslyn's
pre-compilation output API cannot consume the syntax/semantic inputs this
pipeline needs. Declaring the contract in source always works:

```csharp
internal sealed partial class Customer : ReactiveValidationObject
{
    [Reactive] public partial string? Name { get; set; }
}
```

ReactiveUI.SourceGenerators implements the property body while Validation
already sees its name, type, nullability and accessors. The same holds for
Binding's `[ObservableAsProperty]` partial get-only declaration initialized with
`ToProperty`, and for models in separately compiled assemblies.

For undeclared members (`[Reactive]` fields, `[IReactiveObject]` notification
interfaces, commands, collections, marked XAML controls), the generator builds a
private declaration projection for the exact
[producer profiles](../src/ReactiveUI.Validation.SourceGenerators/Producers/ProducerProfiles.md)
(SourceGenerators 4.2.0, Binding 9.1.0, Avalonia 12.1.3, MAUI 10.0.110). This is
the same approach Binding uses internally: add a synthetic tree to a copy of
the compilation, read the original call sites against it, and **never emit the
tree**. Real producers own implementations, notifications, initialization and
their own diagnostics. Final checks compare actual nullability, accessibility,
accessors, storage and generic/inheritance contracts, not just names.
WPF/WinUI declarations already exist before compilation; MAUI/Avalonia named
fields need AdditionalTexts metadata. WinForms producers emit nongeneric
`IViewFor`, so `ValidationViewAdapter<TOwner,TModel>` supplies a typed model.
This is compiler integration, not native UI support.

Do not emit `WhenAnyValue` calls and expect Binding to rewrite them later; that
repeats the ordering problem. Validation emits direct runtime operations instead.

| Diagnostic | Action |
| --- | --- |
| RUVG001 / RUVG006 | Selection, dependency or storage cannot be generated: supply the indicated typed selector, target or provider (or make the enclosing type partial). |
| RUVG002 | No legal generic signature or bridge: use a lexical, provider or factory route. |
| RUVG003 / RUVG004 | Interception locations or runtime contract missing: check the SDK and matching analyzer/runtime assets. |
| RUVG005 / RUVG007 | Normal call or reference lacks interception or explicit runtime-dispatch opt-in. `nameof` stays allowed. |
| RUVG008 | Actual producer output differs from the projection: align producer versions or declare the member. |
| RUVG009 / RUVG010 | Unsupported producer profile or unresolved marked XAML. |
| RUVG011 (warning) | Retained `Expression` argument needs dynamic construction; use the `Func` overload or a typed selector. |

`ReactiveUIValidationProducerProjectionEnabled=false` disables projection only,
not final dispatch checks. Removing the analyzer neither installs reflection nor
supplies a missing registration.

## State, lifetime and scheduling

Rules preserve complete states, full paths and the registration context they
captured. The default context honours `IValidatableViewModel` through interface
dispatch, so shadowing members do not replace it. Helper disposal removes its
component without disposing borrowed contexts, models or sources. Blocking and
advisory contexts are independent; replacing a context does not move existing
registrations.

Normal presentation starts with actual initial states;
`ValidationInitialSequence.LegacyEmpty`/`LegacyValid` restore compatibility
preludes without inventing domain validity. `ValidationMissingOwnerPolicy`
offers DefaultValue, Suppress and Fallback. Bindings keep strict full-path
membership, complete custom states, source-thread callbacks, replacement replay
and owned failure cleanup. `ValidationAsync`, `ValidationCollection`,
`ValidationRowLease` and `ValidationScheduling` provide asynchronous,
collection and presentation policies that a selector cannot imply.

The .NET 10 core targets set `IsAotCompatible`. Unsafe APIs remain available with
accurate annotations; see [NativeAOT and generators](aot-and-generators.md).
